import { type ReactNode, useEffect, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock, StatusBadge } from "../components/OperationsUi";
import { formatCurrency, formatDateTime, formatMinutes } from "../lib/formatters";
import {
  createMaintenanceRecord,
  getEquipment,
  getMaintenanceAlerts,
  getMaintenanceRecords,
  getMaintenanceRules,
  getMaintenanceSummary,
  upsertMaintenanceRule,
  type EquipmentItem,
  type MaintenanceAlert,
  type MaintenanceRecord,
  type MaintenanceRule,
  type MaintenanceSummary
} from "../lib/platform-api";

const equipmentTypeOptions = [
  { value: 1, label: "Kite" },
  { value: 2, label: "Prancha" },
  { value: 3, label: "Barra" },
  { value: 4, label: "Trapézio" },
  { value: 5, label: "Capacete" },
  { value: 6, label: "Colete" },
  { value: 7, label: "Wing" },
  { value: 8, label: "Foil" }
];

const serviceCategoryOptions = [
  { value: 1, label: "Preventiva" },
  { value: 2, label: "Corretiva" },
  { value: 3, label: "Inspeção" },
  { value: 4, label: "Limpeza" },
  { value: 5, label: "Upgrade" }
];

const conditionOptions = [
  { value: 1, label: "Excelente" },
  { value: 2, label: "Boa" },
  { value: 3, label: "Atenção" },
  { value: 4, label: "Precisa de reparo" },
  { value: 5, label: "Fora de serviço" }
];

const initialRuleForm = {
  equipmentType: "1",
  planName: "",
  serviceCategory: "1",
  serviceEveryMinutes: "",
  serviceEveryDays: "",
  warningLeadMinutes: "300",
  criticalLeadMinutes: "0",
  warningLeadDays: "15",
  criticalLeadDays: "0",
  checklist: "",
  notes: "",
  isActive: true
};

const initialRecordForm = {
  equipmentId: "",
  serviceDateUtc: "",
  description: "",
  cost: "",
  performedBy: "",
  counterpartyName: "",
  serviceCategory: "1",
  conditionAfterService: "2"
};

export function MaintenancePage() {
  const { token } = useSession();
  const [equipment, setEquipment] = useState<EquipmentItem[]>([]);
  const [rules, setRules] = useState<MaintenanceRule[]>([]);
  const [records, setRecords] = useState<MaintenanceRecord[]>([]);
  const [alerts, setAlerts] = useState<MaintenanceAlert[]>([]);
  const [summary, setSummary] = useState<MaintenanceSummary | null>(null);
  const [ruleForm, setRuleForm] = useState(initialRuleForm);
  const [recordForm, setRecordForm] = useState(initialRecordForm);
  const [isLoading, setIsLoading] = useState(true);
  const [isSavingRule, setIsSavingRule] = useState(false);
  const [isSavingRecord, setIsSavingRecord] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      return;
    }

    void loadData(token);
  }, [token]);

  async function loadData(currentToken: string) {
    try {
      setIsLoading(true);
      setError(null);
      const [equipmentData, rulesData, recordsData, alertsData, summaryData] = await Promise.all([
        getEquipment(currentToken),
        getMaintenanceRules(currentToken),
        getMaintenanceRecords(currentToken),
        getMaintenanceAlerts(currentToken),
        getMaintenanceSummary(currentToken)
      ]);

      setEquipment(equipmentData);
      setRules(rulesData);
      setRecords(recordsData);
      setAlerts(alertsData);
      setSummary(summaryData);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar a manutenção.");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleRuleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSavingRule(true);
      setError(null);
      await upsertMaintenanceRule(token, {
        equipmentType: Number(ruleForm.equipmentType),
        planName: ruleForm.planName,
        serviceCategory: Number(ruleForm.serviceCategory),
        serviceEveryMinutes: ruleForm.serviceEveryMinutes ? Number(ruleForm.serviceEveryMinutes) : null,
        serviceEveryDays: ruleForm.serviceEveryDays ? Number(ruleForm.serviceEveryDays) : null,
        warningLeadMinutes: ruleForm.warningLeadMinutes ? Number(ruleForm.warningLeadMinutes) : null,
        criticalLeadMinutes: ruleForm.criticalLeadMinutes ? Number(ruleForm.criticalLeadMinutes) : null,
        warningLeadDays: ruleForm.warningLeadDays ? Number(ruleForm.warningLeadDays) : null,
        criticalLeadDays: ruleForm.criticalLeadDays ? Number(ruleForm.criticalLeadDays) : null,
        checklist: ruleForm.checklist || undefined,
        notes: ruleForm.notes || undefined,
        isActive: ruleForm.isActive
      });
      setRuleForm(initialRuleForm);
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar a regra.");
    } finally {
      setIsSavingRule(false);
    }
  }

  async function handleRecordSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSavingRecord(true);
      setError(null);
      await createMaintenanceRecord(token, {
        equipmentId: recordForm.equipmentId,
        serviceDateUtc: new Date(recordForm.serviceDateUtc).toISOString(),
        description: recordForm.description,
        cost: recordForm.cost ? Number(recordForm.cost) : null,
        performedBy: recordForm.performedBy || undefined,
        counterpartyName: recordForm.counterpartyName || undefined,
        serviceCategory: Number(recordForm.serviceCategory),
        conditionAfterService: Number(recordForm.conditionAfterService)
      });
      setRecordForm(initialRecordForm);
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível registrar a manutenção.");
    } finally {
      setIsSavingRecord(false);
    }
  }

  return (
    <div className="space-y-6">
      <PageHero
        eyebrow="Manutenção"
        title="Planos, alertas inteligentes e financeiro da oficina em uma visão só."
        description="A manutenção agora separa patrimônio da escola e equipamento de terceiros, mede impacto financeiro e mostra a urgência com severidade."
        stats={[
          { label: "Alertas", value: String(alerts.length) },
          { label: "Registros", value: String(summary?.records ?? records.length) },
          { label: "Custos", value: formatCurrency(summary?.expenseAmount ?? 0) },
          { label: "Receitas", value: formatCurrency(summary?.revenueAmount ?? 0) }
        ]}
      />

      {isLoading ? <LoadingBlock label="Carregando manutenção" /> : null}
      {error ? <ErrorBlock message={error} /> : null}

      <div className="grid gap-4 xl:grid-cols-[0.9fr_1.1fr]">
        <div className="space-y-4">
          <GlassCard title="Plano de manutenção">
            <form className="grid gap-3" onSubmit={handleRuleSubmit}>
              <div className="grid gap-3 md:grid-cols-2">
                <SelectField
                  value={ruleForm.equipmentType}
                  onChange={(value) => setRuleForm((current) => ({ ...current, equipmentType: value }))}
                  options={equipmentTypeOptions.map((option) => ({ value: String(option.value), label: option.label }))}
                />
                <SelectField
                  value={ruleForm.serviceCategory}
                  onChange={(value) => setRuleForm((current) => ({ ...current, serviceCategory: value }))}
                  options={serviceCategoryOptions.map((option) => ({ value: String(option.value), label: option.label }))}
                />
              </div>
              <Field
                placeholder="Nome do plano"
                value={ruleForm.planName}
                onChange={(value) => setRuleForm((current) => ({ ...current, planName: value }))}
              />
              <div className="grid gap-3 md:grid-cols-2">
                <Field
                  placeholder="Serviço a cada X minutos"
                  value={ruleForm.serviceEveryMinutes}
                  onChange={(value) => setRuleForm((current) => ({ ...current, serviceEveryMinutes: value }))}
                  type="number"
                />
                <Field
                  placeholder="Serviço a cada X dias"
                  value={ruleForm.serviceEveryDays}
                  onChange={(value) => setRuleForm((current) => ({ ...current, serviceEveryDays: value }))}
                  type="number"
                />
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                <Field
                  placeholder="Alerta por minutos"
                  value={ruleForm.warningLeadMinutes}
                  onChange={(value) => setRuleForm((current) => ({ ...current, warningLeadMinutes: value }))}
                  type="number"
                />
                <Field
                  placeholder="Crítico por minutos"
                  value={ruleForm.criticalLeadMinutes}
                  onChange={(value) => setRuleForm((current) => ({ ...current, criticalLeadMinutes: value }))}
                  type="number"
                />
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                <Field
                  placeholder="Alerta por dias"
                  value={ruleForm.warningLeadDays}
                  onChange={(value) => setRuleForm((current) => ({ ...current, warningLeadDays: value }))}
                  type="number"
                />
                <Field
                  placeholder="Crítico por dias"
                  value={ruleForm.criticalLeadDays}
                  onChange={(value) => setRuleForm((current) => ({ ...current, criticalLeadDays: value }))}
                  type="number"
                />
              </div>
              <TextAreaField
                placeholder="Checklist do plano"
                value={ruleForm.checklist}
                onChange={(value) => setRuleForm((current) => ({ ...current, checklist: value }))}
              />
              <TextAreaField
                placeholder="Observações operacionais"
                value={ruleForm.notes}
                onChange={(value) => setRuleForm((current) => ({ ...current, notes: value }))}
              />
              <PrimaryButton disabled={isSavingRule}>
                {isSavingRule ? "Salvando" : "Salvar plano"}
              </PrimaryButton>
            </form>
          </GlassCard>

          <GlassCard title="Lançar serviço">
            <form className="grid gap-3" onSubmit={handleRecordSubmit}>
              <SelectField
                value={recordForm.equipmentId}
                onChange={(value) => setRecordForm((current) => ({ ...current, equipmentId: value }))}
                options={[{ value: "", label: "Selecione o equipamento" }, ...equipment.map((item) => ({ value: item.id, label: `${item.name} · ${item.ownershipType === "ThirdParty" ? "Terceiro" : "Escola"}` }))]}
                required
              />
              <div className="grid gap-3 md:grid-cols-2">
                <Field
                  placeholder="Data e hora do serviço"
                  value={recordForm.serviceDateUtc}
                  onChange={(value) => setRecordForm((current) => ({ ...current, serviceDateUtc: value }))}
                  type="datetime-local"
                  required
                />
                <SelectField
                  value={recordForm.serviceCategory}
                  onChange={(value) => setRecordForm((current) => ({ ...current, serviceCategory: value }))}
                  options={serviceCategoryOptions.map((option) => ({ value: String(option.value), label: option.label }))}
                />
              </div>
              <TextAreaField
                placeholder="Descrição do serviço"
                value={recordForm.description}
                onChange={(value) => setRecordForm((current) => ({ ...current, description: value }))}
                required
              />
              <div className="grid gap-3 md:grid-cols-2">
                <Field
                  placeholder="Valor do serviço"
                  value={recordForm.cost}
                  onChange={(value) => setRecordForm((current) => ({ ...current, cost: value }))}
                  type="number"
                />
                <Field
                  placeholder="Executado por"
                  value={recordForm.performedBy}
                  onChange={(value) => setRecordForm((current) => ({ ...current, performedBy: value }))}
                />
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                <Field
                  placeholder="Parceiro / cliente"
                  value={recordForm.counterpartyName}
                  onChange={(value) => setRecordForm((current) => ({ ...current, counterpartyName: value }))}
                />
                <SelectField
                  value={recordForm.conditionAfterService}
                  onChange={(value) => setRecordForm((current) => ({ ...current, conditionAfterService: value }))}
                  options={conditionOptions.map((option) => ({ value: String(option.value), label: option.label }))}
                />
              </div>
              <PrimaryButton disabled={isSavingRecord}>
                {isSavingRecord ? "Salvando" : "Registrar serviço"}
              </PrimaryButton>
            </form>
          </GlassCard>
        </div>

        <div className="space-y-4">
          <GlassCard title="Alertas ativos">
            <div className="space-y-3">
              {alerts.length === 0 ? (
                <EmptyState message="Nenhum alerta preventivo ativo." />
              ) : (
                alerts.map((alert) => (
                  <div key={`${alert.id}-${alert.alertType}`} className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <div className="text-sm font-medium text-[var(--q-text)]">{alert.name}</div>
                        <div className="mt-1 text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                          {alert.type} · {alert.serviceCategory}
                        </div>
                      </div>
                      <StatusBadge value={alert.severity} />
                    </div>
                    <div className="mt-3 text-sm text-[var(--q-text-2)]">
                      {typeof alert.remainingMinutes === "number" ? `Serviço em ${formatMinutes(alert.remainingMinutes)} de uso restante. ` : null}
                      {typeof alert.remainingDays === "number" ? `Faltam ${alert.remainingDays} dias para o vencimento. ` : null}
                      {alert.condition ? `Condição atual: ${alert.condition}. ` : null}
                      {alert.recommendedAction}
                    </div>
                  </div>
                ))
              )}
            </div>
          </GlassCard>

          <GlassCard title="Resumo financeiro da manutenção">
            <div className="grid gap-3 md:grid-cols-2">
              <MetricCard label="Custos da escola" value={formatCurrency(summary?.expenseAmount ?? 0)} tone="warning" />
              <MetricCard label="Receitas de terceiros" value={formatCurrency(summary?.revenueAmount ?? 0)} tone="info" />
            </div>
            <div className="mt-4 grid gap-4 xl:grid-cols-[0.9fr_1.1fr]">
              <div className="space-y-3">
                <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">Por categoria</div>
                {(summary?.byCategory ?? []).map((item) => (
                  <div key={item.category} className="rounded-[20px] border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm">
                    <div className="font-medium text-[var(--q-text)]">{item.category}</div>
                    <div className="mt-1 text-[var(--q-text-2)]">{item.records} registro(s) · {formatCurrency(item.amount)}</div>
                  </div>
                ))}
              </div>
              <div className="space-y-3">
                <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">Por equipamento</div>
                {(summary?.byEquipment ?? []).slice(0, 6).map((item) => (
                  <div key={item.equipmentId} className="rounded-[20px] border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm">
                    <div className="font-medium text-[var(--q-text)]">{item.equipmentName}</div>
                    <div className="mt-1 text-[var(--q-text-2)]">{item.records} registro(s) · {formatCurrency(item.amount)}</div>
                  </div>
                ))}
              </div>
            </div>
          </GlassCard>

          <GlassCard title="Histórico de serviços">
            <div className="space-y-3">
              {records.length === 0 ? (
                <EmptyState message="Nenhuma manutenção registrada." />
              ) : records.map((record) => (
                <div key={record.id} className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface)] p-4">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <div className="text-sm font-medium text-[var(--q-text)]">{record.equipmentName}</div>
                      <div className="mt-1 text-sm text-[var(--q-text-2)]">{record.description}</div>
                    </div>
                    <StatusBadge value={record.financialEffect} />
                  </div>
                  <div className="mt-3 text-sm text-[var(--q-text-2)]">
                    {record.serviceCategory} · {record.equipmentOwnershipType === "ThirdParty" ? "Equipamento de terceiro" : "Patrimônio da escola"} · {formatCurrency(record.cost ?? 0)}
                  </div>
                  <div className="mt-1 text-sm text-[var(--q-text-2)]">
                    Uso no serviço: {formatMinutes(record.usageMinutesAtService)} · Executado por: {record.performedBy || "Não informado"}
                  </div>
                  <div className="mt-1 text-sm text-[var(--q-text-2)]">
                    Contraparte: {record.counterpartyName || "Não informada"}
                  </div>
                  <div className="mt-3 text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                    {formatDateTime(record.serviceDateUtc)}
                  </div>
                </div>
              ))}
            </div>
          </GlassCard>
        </div>
      </div>
    </div>
  );
}

function Field({
  placeholder,
  value,
  onChange,
  type = "text",
  required = false
}: {
  placeholder: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  required?: boolean;
}) {
  return (
    <input
      className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
      placeholder={placeholder}
      value={value}
      onChange={(event) => onChange(event.target.value)}
      type={type}
      required={required}
    />
  );
}

function TextAreaField({
  placeholder,
  value,
  onChange,
  required = false
}: {
  placeholder: string;
  value: string;
  onChange: (value: string) => void;
  required?: boolean;
}) {
  return (
    <textarea
      className="min-h-24 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
      placeholder={placeholder}
      value={value}
      onChange={(event) => onChange(event.target.value)}
      required={required}
    />
  );
}

function SelectField({
  value,
  onChange,
  options,
  required = false
}: {
  value: string;
  onChange: (value: string) => void;
  options: Array<{ value: string; label: string }>;
  required?: boolean;
}) {
  return (
    <select
      className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
      value={value}
      onChange={(event) => onChange(event.target.value)}
      required={required}
    >
      {options.map((option) => (
        <option key={`${option.value}-${option.label}`} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  );
}

function PrimaryButton({ children, disabled = false }: { children: ReactNode; disabled?: boolean }) {
  return (
    <button
      className="rounded-full border border-transparent bg-[var(--q-grad-brand)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95 disabled:opacity-70"
      type="submit"
      disabled={disabled}
    >
      {children}
    </button>
  );
}

function MetricCard({ label, value, tone }: { label: string; value: string; tone: "warning" | "info" }) {
  const toneClass = tone === "warning"
    ? "border-[var(--q-warning)]/35 bg-[var(--q-warning-bg)]"
    : "border-[var(--q-info)]/35 bg-[var(--q-info-bg)]";

  return (
    <div className={`rounded-[22px] border p-4 ${toneClass}`}>
      <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">{label}</div>
      <div className="mt-3 text-2xl font-semibold text-[var(--q-text)]">{value}</div>
    </div>
  );
}

function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
      {message}
    </div>
  );
}
