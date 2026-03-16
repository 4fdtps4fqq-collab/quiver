import { useEffect, useState } from "react";
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
  upsertMaintenanceRule,
  type EquipmentItem,
  type MaintenanceAlert,
  type MaintenanceRecord,
  type MaintenanceRule
} from "../lib/platform-api";

const equipmentTypeOptions = [
  { value: 1, label: "Kite" },
  { value: 2, label: "Prancha" },
  { value: 3, label: "Barra" },
  { value: 4, label: "Trapezio" },
  { value: 5, label: "Capacete" },
  { value: 6, label: "Colete" },
  { value: 7, label: "Wing" },
  { value: 8, label: "Foil" }
];

const conditionOptions = [
  { value: 1, label: "Excelente" },
  { value: 2, label: "Boa" },
  { value: 3, label: "Atencao" },
  { value: 4, label: "Precisa de reparo" },
  { value: 5, label: "Fora de serviço" }
];

const initialRuleForm = {
  equipmentType: "1",
  serviceEveryMinutes: "",
  serviceEveryDays: "",
  isActive: true
};

const initialRecordForm = {
  equipmentId: "",
  serviceDateUtc: "",
  description: "",
  cost: "",
  performedBy: "",
  conditionAfterService: "2"
};

export function MaintenancePage() {
  const { token } = useSession();
  const [equipment, setEquipment] = useState<EquipmentItem[]>([]);
  const [rules, setRules] = useState<MaintenanceRule[]>([]);
  const [records, setRecords] = useState<MaintenanceRecord[]>([]);
  const [alerts, setAlerts] = useState<MaintenanceAlert[]>([]);
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
      const [equipmentData, rulesData, recordsData, alertsData] = await Promise.all([
        getEquipment(currentToken),
        getMaintenanceRules(currentToken),
        getMaintenanceRecords(currentToken),
        getMaintenanceAlerts(currentToken)
      ]);

      setEquipment(equipmentData);
      setRules(rulesData);
      setRecords(recordsData);
      setAlerts(alertsData);
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
        serviceEveryMinutes: ruleForm.serviceEveryMinutes ? Number(ruleForm.serviceEveryMinutes) : null,
        serviceEveryDays: ruleForm.serviceEveryDays ? Number(ruleForm.serviceEveryDays) : null,
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

  const maintenanceCost = records.reduce((sum, item) => sum + (item.cost ?? 0), 0);

  return (
    <div className="space-y-6">
      <PageHero
        eyebrow="Manutenção"
        title="Manutenção preventiva e corretiva com risco operacional visível em tempo real."
        description="Regras por tipo, alertas por uso e histórico por item ajudam a reduzir surpresas na operação e tornam o custo justificável."
        stats={[
          { label: "Alertas", value: String(alerts.length) },
          { label: "Regras", value: String(rules.length) },
          { label: "Registros", value: String(records.length) },
          { label: "Custo total", value: formatCurrency(maintenanceCost) }
        ]}
      />

      {isLoading ? <LoadingBlock label="Carregando manutenção" /> : null}
      {error ? <ErrorBlock message={error} /> : null}

      <div className="grid gap-4 xl:grid-cols-[0.9fr_1.1fr]">
        <div className="space-y-4">
          <GlassCard title="Regra preventiva" description="Configure a janela de serviço por uso acumulado ou por dias corridos.">
            <form className="grid gap-3" onSubmit={handleRuleSubmit}>
              <select
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                value={ruleForm.equipmentType}
                onChange={(event) => setRuleForm((current) => ({ ...current, equipmentType: event.target.value }))}
              >
                {equipmentTypeOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Serviço a cada X minutos"
                type="number"
                min="0"
                value={ruleForm.serviceEveryMinutes}
                onChange={(event) => setRuleForm((current) => ({ ...current, serviceEveryMinutes: event.target.value }))}
              />
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Serviço a cada X dias"
                type="number"
                min="0"
                value={ruleForm.serviceEveryDays}
                onChange={(event) => setRuleForm((current) => ({ ...current, serviceEveryDays: event.target.value }))}
              />
              <button
                className="rounded-full border border-transparent bg-[var(--q-grad-brand)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95"
                type="submit"
                disabled={isSavingRule}
              >
                {isSavingRule ? "Salvando" : "Salvar regra"}
              </button>
            </form>
          </GlassCard>

          <GlassCard title="Registro de manutenção" description="Feche uma intervenção ajustando a condição atual do equipamento.">
            <form className="grid gap-3" onSubmit={handleRecordSubmit}>
              <select
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                value={recordForm.equipmentId}
                onChange={(event) => setRecordForm((current) => ({ ...current, equipmentId: event.target.value }))}
                required
              >
                <option value="">Selecione o item</option>
                {equipment.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name} - {item.type}
                  </option>
                ))}
              </select>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                type="datetime-local"
                value={recordForm.serviceDateUtc}
                onChange={(event) => setRecordForm((current) => ({ ...current, serviceDateUtc: event.target.value }))}
                required
              />
              <textarea
                className="min-h-24 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Descrição do serviço"
                value={recordForm.description}
                onChange={(event) => setRecordForm((current) => ({ ...current, description: event.target.value }))}
                required
              />
              <div className="grid gap-3 md:grid-cols-2">
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  placeholder="Custo"
                  type="number"
                  min="0"
                  step="0.01"
                  value={recordForm.cost}
                  onChange={(event) => setRecordForm((current) => ({ ...current, cost: event.target.value }))}
                />
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  placeholder="Responsável"
                  value={recordForm.performedBy}
                  onChange={(event) => setRecordForm((current) => ({ ...current, performedBy: event.target.value }))}
                />
              </div>
              <select
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                value={recordForm.conditionAfterService}
                onChange={(event) =>
                  setRecordForm((current) => ({ ...current, conditionAfterService: event.target.value }))
                }
              >
                {conditionOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
              <button
                className="rounded-full border border-[var(--q-success)]/25 bg-[var(--q-success-bg)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-success)] transition hover:opacity-95"
                type="submit"
                disabled={isSavingRecord}
              >
                {isSavingRecord ? "Salvando" : "Registrar manutenção"}
              </button>
            </form>
          </GlassCard>
        </div>

        <div className="space-y-4">
          <GlassCard title="Alertas ativos" description="Sinais de manutenção por uso, data ou condição do item.">
            <div className="space-y-3">
              {alerts.length === 0 ? (
                <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
                  Nenhum alerta preventivo ativo.
                </div>
              ) : (
                alerts.map((alert) => (
                  <div key={`${alert.id}-${alert.alertType}`} className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <div className="text-sm font-medium text-[var(--q-text)]">{alert.name}</div>
                        <div className="mt-1 text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">{alert.type}</div>
                      </div>
                      <StatusBadge value={alert.alertType} />
                    </div>
                    <div className="mt-3 text-sm text-[var(--q-text-2)]">
                      {typeof alert.remainingMinutes === "number"
                        ? `Serviço em ${formatMinutes(alert.remainingMinutes)} de uso restante.`
                        : null}
                      {typeof alert.remainingDays === "number"
                        ? ` Faltam ${alert.remainingDays} dias para a próxima manutenção.`
                        : null}
                      {alert.condition ? ` Condição atual: ${alert.condition}.` : null}
                    </div>
                  </div>
                ))
              )}
            </div>
          </GlassCard>

          <GlassCard title="Regras configuradas" description="Política de manutenção por tipo de ativo.">
            <div className="overflow-x-auto">
              <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
                <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                  <tr>
                    <th className="pb-3">Tipo</th>
                    <th className="pb-3">Minutos</th>
                    <th className="pb-3">Dias</th>
                    <th className="pb-3">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {rules.map((rule) => (
                    <tr key={rule.id} className="border-t border-[var(--q-border)]">
                      <td className="py-3 font-medium text-[var(--q-text)]">{rule.equipmentType}</td>
                      <td className="py-3">{rule.serviceEveryMinutes ?? "-"}</td>
                      <td className="py-3">{rule.serviceEveryDays ?? "-"}</td>
                      <td className="py-3">
                        <StatusBadge value={rule.isActive ? "Active" : "Inactive"} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </GlassCard>

          <GlassCard title="Histórico de serviços" description="Últimas manutenções registradas pela equipe.">
            <div className="space-y-3">
              {records.map((record) => (
                <div key={record.id} className="rounded-[22px] border border-[var(--q-warning)]/25 bg-[var(--q-warning-bg)] p-4">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <div className="text-sm font-medium text-[var(--q-text)]">{record.equipmentName}</div>
                      <div className="mt-1 text-sm text-[var(--q-text-2)]">{record.description}</div>
                    </div>
                    <div className="text-sm font-medium text-[#B58100]">{formatCurrency(record.cost ?? 0)}</div>
                  </div>
                  <div className="mt-3 text-sm text-[var(--q-text-2)]">
                    Uso no serviço: {formatMinutes(record.usageMinutesAtService)} • Responsável: {record.performedBy || "Não informado"}
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
