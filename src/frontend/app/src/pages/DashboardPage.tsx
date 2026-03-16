import { useEffect, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock, StatGrid, StatusBadge } from "../components/OperationsUi";
import {
  formatCurrency,
  formatDateTime,
  formatMinutes,
  fromLocalDateTimeInput,
  toLocalDateTimeInput
} from "../lib/formatters";
import { translateLabel } from "../lib/localization";
import { getDashboardReport, type DashboardReport } from "../lib/platform-api";

const initialFilters = createDefaultFilters();

export function DashboardPage() {
  const { token } = useSession();
  const [report, setReport] = useState<DashboardReport | null>(null);
  const [filters, setFilters] = useState(initialFilters);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      return;
    }

    void loadDashboard(token, filters);
  }, [token]);

  async function loadDashboard(currentToken: string, currentFilters: typeof filters) {
    try {
      setIsLoading(true);
      setError(null);
      const nextReport = await getDashboardReport(currentToken, {
        fromUtc: fromLocalDateTimeInput(currentFilters.fromUtc) ?? undefined,
        toUtc: fromLocalDateTimeInput(currentFilters.toUtc) ?? undefined
      });
      setReport(nextReport);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar o dashboard.");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleApplyFilters(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    await loadDashboard(token, filters);
  }

  async function applyPreset(days: number) {
    if (!token) {
      return;
    }

    const preset = createFiltersForDays(days);
    setFilters(preset);
    await loadDashboard(token, preset);
  }

  const finance = report?.finance;
  const academics = report?.academics;
  const equipment = report?.equipment;
  const maintenanceAlerts = report?.maintenanceAlerts ?? [];
  const serviceErrors = report?.serviceErrors ?? [];

  const marginTone = (finance?.grossMargin ?? 0) >= 0 ? "cyan" : "amber";
  const mostUsedSource = finance?.revenueBySource[0]?.sourceType;
  const heaviestCostCenter = finance?.expenseByCategory[0]?.category;

  return (
    <div className="space-y-6">
      <PageHero
        eyebrow="Centro de comando"
        title="A escola inteira em uma leitura periódica de agenda, equipamento e caixa."
        description="O dashboard agora responde ao período escolhido e mostra séries operacionais e financeiras para apoiar decisões do dia a dia e da margem."
        stats={[
          { label: "Período inicial", value: formatDateTime(report?.fromUtc ?? fromLocalDateTimeInput(filters.fromUtc) ?? undefined) },
          { label: "Período final", value: formatDateTime(report?.toUtc ?? fromLocalDateTimeInput(filters.toUtc) ?? undefined) },
          { label: "Gerado em", value: formatDateTime(report?.generatedAtUtc) },
          { label: "Alertas", value: String(maintenanceAlerts.length) }
        ]}
      />

      <GlassCard
        title="Filtro analítico"
        description="Escolha a janela do dashboard para recalcular agenda, uso de equipamento e fluxo financeiro no mesmo período."
        aside={
          <div className="flex flex-wrap gap-2">
            <button
              className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-2 text-xs uppercase tracking-[0.24em] text-[var(--q-text)] transition hover:bg-[var(--q-info-bg)]"
              type="button"
              onClick={() => void applyPreset(7)}
            >
              Últimos 7 dias
            </button>
            <button
              className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-2 text-xs uppercase tracking-[0.24em] text-[var(--q-text)] transition hover:bg-[var(--q-info-bg)]"
              type="button"
              onClick={() => void applyPreset(14)}
            >
              Últimos 14 dias
            </button>
            <button
              className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-2 text-xs uppercase tracking-[0.24em] text-[var(--q-text)] transition hover:bg-[var(--q-info-bg)]"
              type="button"
              onClick={() => void applyPreset(30)}
            >
              Últimos 30 dias
            </button>
          </div>
        }
      >
        <form className="grid gap-3 md:grid-cols-[1fr_1fr_auto]" onSubmit={handleApplyFilters}>
          <label className="grid gap-2 text-sm text-[var(--q-text)]">
            <span>Data e hora inicial do dashboard</span>
            <input
              className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
              type="datetime-local"
              value={filters.fromUtc}
              onChange={(event) => setFilters((current) => ({ ...current, fromUtc: event.target.value }))}
            />
          </label>
          <label className="grid gap-2 text-sm text-[var(--q-text)]">
            <span>Data e hora final do dashboard</span>
            <input
              className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
              type="datetime-local"
              value={filters.toUtc}
              onChange={(event) => setFilters((current) => ({ ...current, toUtc: event.target.value }))}
            />
          </label>
          <div className="flex items-end gap-3">
            <button
              className="rounded-full border border-transparent bg-[var(--q-grad-brand)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95"
              type="submit"
            >
              Atualizar leitura
            </button>
          </div>
        </form>
      </GlassCard>

      {isLoading ? <LoadingBlock label="Carregando consolidado da escola" /> : null}
      {error ? <ErrorBlock message={error} /> : null}
      {!isLoading && !error && serviceErrors.length > 0 ? (
        <div className="rounded-[24px] border border-[var(--q-warning)]/40 bg-[var(--q-warning-bg)] px-5 py-4 text-sm text-[var(--q-text)]">
          Algumas partes do painel estão indisponíveis agora:
          <div className="mt-2 flex flex-wrap gap-2">
            {serviceErrors.map((item) => (
              <span
                key={`${item.service}-${item.statusCode}`}
                className="rounded-full border border-[var(--q-warning)]/40 bg-white/70 px-3 py-1 text-xs uppercase tracking-[0.14em] text-[var(--q-text)]"
              >
                {item.service}: {item.message}
              </span>
            ))}
          </div>
        </div>
      ) : null}

      {!isLoading && !error && report ? (
        <>
          <StatGrid
            items={[
              { label: "Alunos ativos", value: String(academics?.students ?? 0) },
              { label: "Taxa de realização", value: `${academics?.completionRate ?? 0}%`, tone: "emerald" },
              { label: "Uso de equipamento", value: formatMinutes(equipment?.usageMinutesInPeriod), tone: "amber" },
              { label: "Margem bruta", value: formatCurrency(finance?.grossMargin), tone: marginTone }
            ]}
          />

          <div className="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
            <GlassCard
              title="Ritmo da agenda"
              description="Série do período com total de aulas, realizadas e perdas por cancelamento ou no-show."
            >
              <MiniBarChart
                items={(academics?.lessonSeries ?? []).map((item) => ({
                  label: item.bucketLabel,
                  primary: item.totalLessons,
                  secondary: item.realizedLessons,
                  tertiary: item.cancelledLessons
                }))}
                tones={["bg-[var(--q-info)]", "bg-[var(--q-success)]", "bg-[var(--q-danger)]"]}
                legend={[
                  { label: "Aulas no dia", tone: "bg-[var(--q-info)]" },
                  { label: "Realizadas", tone: "bg-[var(--q-success)]" },
                  { label: "Perdidas", tone: "bg-[var(--q-danger)]" }
                ]}
              />

              <div className="mt-5 grid gap-3 md:grid-cols-4">
                <MetricBox label="Aulas no período" value={String(academics?.totalLessonsInPeriod ?? 0)} />
                <MetricBox label="Agendadas" value={String(academics?.scheduledLessons ?? 0)} />
                <MetricBox label="Realizadas" value={String(academics?.realizedLessons ?? 0)} />
                <MetricBox label="Aulas hoje" value={String(academics?.lessonsToday ?? 0)} />
              </div>
            </GlassCard>

            <GlassCard
              title="Fluxo financeiro"
              description="Série de entradas e saídas no mesmo período para revelar pressão de margem e ritmo comercial."
            >
              <MiniBarChart
                items={(finance?.cashflowSeries ?? []).map((item) => ({
                  label: item.bucketLabel,
                  primary: item.revenue,
                  secondary: item.expense,
                  tertiary: item.net
                }))}
                tones={["bg-[var(--q-success)]", "bg-[var(--q-warning)]", "bg-[var(--q-info)]"]}
                legend={[
                  { label: "Receita", tone: "bg-[var(--q-success)]" },
                  { label: "Despesa", tone: "bg-[var(--q-warning)]" },
                  { label: "Saldo", tone: "bg-[var(--q-info)]" }
                ]}
                currency
              />

              <div className="mt-5 space-y-3">
                <InsightRow
                  label="Custo de instrutores"
                  value={formatCurrency(finance?.instructorPayrollExpense)}
                  description={`${Math.round(((finance?.realizedInstructionMinutes ?? 0) / 60) * 10) / 10}h realizadas no período`}
                />
                <InsightRow
                  label="Maior origem de receita"
                  value={mostUsedSource ? translateLabel(mostUsedSource) : "-"}
                  description={
                    finance?.revenueBySource[0]
                      ? `${formatCurrency(finance.revenueBySource[0].totalAmount)} em ${finance.revenueBySource[0].entries} lançamentos`
                      : "Sem receita no período"
                  }
                />
                <InsightRow
                  label="Maior centro de custo"
                  value={heaviestCostCenter ? translateLabel(heaviestCostCenter) : "-"}
                  description={
                    finance?.expenseByCategory[0]
                      ? `${formatCurrency(finance.expenseByCategory[0].totalAmount)} em ${finance.expenseByCategory[0].entries} lançamentos`
                      : "Sem despesa no período"
                  }
                />
              </div>
            </GlassCard>
          </div>

          <div className="grid gap-4 xl:grid-cols-[0.95fr_1.05fr]">
            <GlassCard
              title="Saúde da operação"
              description="Uso do inventário, checkouts do período e distribuição atual das condições dos equipamentos."
            >
              <div className="grid gap-3 md:grid-cols-3">
                <MetricBox label="Checkouts no período" value={String(equipment?.checkoutsInPeriod ?? 0)} />
                <MetricBox label="Manutenções executadas" value={String(equipment?.maintenanceExecutedInPeriod ?? 0)} />
                <MetricBox label="Itens em atenção" value={String(equipment?.equipmentInAttention ?? 0)} />
              </div>

              <div className="mt-5">
                <MiniBarChart
                  items={(equipment?.activitySeries ?? []).map((item) => ({
                    label: item.bucketLabel,
                    primary: item.usageMinutes,
                    secondary: item.checkouts,
                    tertiary: item.maintenanceRecords
                  }))}
                  tones={["bg-[var(--q-warning)]", "bg-[var(--q-info)]", "bg-[var(--q-success)]"]}
                  legend={[
                    { label: "Uso em minutos", tone: "bg-[var(--q-warning)]" },
                    { label: "Checkouts", tone: "bg-[var(--q-info)]" },
                    { label: "Manutenções", tone: "bg-[var(--q-success)]" }
                  ]}
                />
              </div>

              <div className="mt-5 grid gap-3 md:grid-cols-2">
                {(equipment?.conditionBreakdown ?? []).map((item) => (
                  <div key={item.condition} className="rounded-[20px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                    <div className="flex items-center justify-between gap-3">
                      <StatusBadge value={item.condition} />
                      <span className="text-xl font-semibold text-[var(--q-text)]">{item.count}</span>
                    </div>
                  </div>
                ))}
              </div>
            </GlassCard>

            <GlassCard
              title="Alertas e prioridades"
              description="Itens mais próximos de impactar a operação se não houver ação preventiva."
            >
              <div className="space-y-3">
                {maintenanceAlerts.length === 0 ? (
                  <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
                    Nenhum alerta preventivo próximo do vencimento.
                  </div>
                ) : (
                  maintenanceAlerts.slice(0, 6).map((alert) => (
                    <div key={`${alert.id}-${alert.alertType}`} className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                      <div className="flex flex-wrap items-center justify-between gap-3">
                        <div>
                          <div className="text-sm font-medium text-[var(--q-text)]">{alert.name}</div>
                          <div className="mt-1 text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                            {alert.type}
                          </div>
                        </div>
                        <StatusBadge value={alert.alertType} />
                      </div>
                      <div className="mt-3 text-sm text-[var(--q-text-2)]">
                        {typeof alert.remainingMinutes === "number"
                          ? `Serviço em ${formatMinutes(alert.remainingMinutes)} de uso restante.`
                          : null}
                        {typeof alert.remainingDays === "number"
                          ? ` Faltam ${alert.remainingDays} dias para a próxima janela preventiva.`
                          : null}
                        {alert.condition ? ` Condição atual: ${translateLabel(alert.condition)}.` : null}
                      </div>
                    </div>
                  ))
                )}
              </div>
            </GlassCard>
          </div>

          <div className="grid gap-4 xl:grid-cols-[1fr_1fr]">
            <GlassCard
              title="Performance de instrutores"
              description="Leitura de volume e qualidade de execução por instrutor no período filtrado."
            >
              <div className="overflow-x-auto">
                <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
                  <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                    <tr>
                      <th className="pb-3">Instrutor</th>
                      <th className="pb-3">Total</th>
                      <th className="pb-3">Realizadas</th>
                      <th className="pb-3">Remarcadas</th>
                      <th className="pb-3">Perdidas</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(academics?.instructorPerformance ?? []).slice(0, 8).map((item) => (
                      <tr key={item.instructorId} className="border-t border-[var(--q-border)]">
                        <td className="py-3 font-medium text-[var(--q-text)]">{item.instructorName}</td>
                        <td className="py-3">{item.total}</td>
                        <td className="py-3">{item.realized}</td>
                        <td className="py-3">{item.rescheduled}</td>
                        <td className="py-3">{item.cancelled + item.noShow}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </GlassCard>

            <GlassCard
              title="Guardrails do domínio"
              description="Regras centrais preservadas pelo backend e refletidas neste dashboard."
            >
              <div className="space-y-3">
                {(academics?.invariants ?? []).map((item) => (
                  <div key={item} className="rounded-[22px] border border-[var(--q-info)]/20 bg-[var(--q-info-bg)] p-4 text-sm leading-6 text-[var(--q-text)]">
                    {item}
                  </div>
                ))}
              </div>
            </GlassCard>
          </div>
        </>
      ) : null}
    </div>
  );
}

function createDefaultFilters() {
  return createFiltersForDays(14);
}

function createFiltersForDays(days: number) {
  const now = new Date();
  const start = new Date(now);
  start.setDate(now.getDate() - (days - 1));
  start.setHours(0, 0, 0, 0);

  return {
    fromUtc: toLocalDateTimeInput(start.toISOString()),
    toUtc: toLocalDateTimeInput(now.toISOString())
  };
}

function MetricBox({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
      <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">{label}</div>
      <div className="mt-3 text-2xl font-semibold text-[var(--q-text)]">{value}</div>
    </div>
  );
}

function InsightRow({
  label,
  value,
  description
}: {
  label: string;
  value: string;
  description: string;
}) {
  return (
    <div className="rounded-[20px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
      <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">{label}</div>
      <div className="mt-2 text-lg font-semibold text-[var(--q-text)]">{value}</div>
      <div className="mt-1 text-sm text-[var(--q-text-2)]">{description}</div>
    </div>
  );
}

function MiniBarChart({
  items,
  legend,
  tones = ["bg-[var(--q-info)]", "bg-[var(--q-success)]", "bg-[var(--q-danger)]"],
  currency = false
}: {
  items: Array<{ label: string; primary: number; secondary: number; tertiary: number }>;
  legend: Array<{ label: string; tone: string }>;
  tones?: [string, string, string];
  currency?: boolean;
}) {
  if (items.length === 0) {
    return (
      <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-8 text-sm text-[var(--q-text-2)]">
        Sem pontos suficientes no período para montar a série.
      </div>
    );
  }

  const highestValue = Math.max(
    ...items.flatMap((item) => [Math.abs(item.primary), Math.abs(item.secondary), Math.abs(item.tertiary)]),
    1
  );

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap gap-3">
        {legend.map((item) => (
          <div key={item.label} className="flex items-center gap-2 text-xs uppercase tracking-[0.2em] text-[var(--q-text-2)]">
            <span className={`h-3 w-3 rounded-full ${item.tone}`} />
            <span>{item.label}</span>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-2 gap-3 md:grid-cols-4 xl:grid-cols-7">
        {items.map((item) => (
          <div key={item.label} className="rounded-[20px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-3">
            <div className="flex h-36 items-end justify-center gap-2">
              <Bar value={item.primary} highestValue={highestValue} tone={tones[0]} />
              <Bar value={item.secondary} highestValue={highestValue} tone={tones[1]} />
              <Bar value={item.tertiary} highestValue={highestValue} tone={tones[2]} />
            </div>
            <div className="mt-3 text-xs uppercase tracking-[0.22em] text-[var(--q-muted)]">{item.label}</div>
            <div className="mt-2 space-y-1 text-xs text-[var(--q-text-2)]">
              <div>{currency ? formatCurrency(item.primary) : item.primary}</div>
              <div>{currency ? formatCurrency(item.secondary) : item.secondary}</div>
              <div>{currency ? formatCurrency(item.tertiary) : item.tertiary}</div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function Bar({
  value,
  highestValue,
  tone
}: {
  value: number;
  highestValue: number;
  tone: string;
}) {
  const height = Math.max(10, Math.round((Math.abs(value) / highestValue) * 120));

  return <div className={`w-5 rounded-t-full ${tone}`} style={{ height }} title={String(value)} />;
}
