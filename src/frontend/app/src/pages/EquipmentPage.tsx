import { useEffect, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock, StatusBadge } from "../components/OperationsUi";
import { formatDateTime, formatMinutes } from "../lib/formatters";
import {
  createEquipment,
  createStorage,
  getEquipment,
  getEquipmentHistory,
  getStorages,
  type EquipmentHistory,
  type EquipmentItem,
  type Storage
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

const conditionOptions = [
  { value: 1, label: "Excelente" },
  { value: 2, label: "Boa" },
  { value: 3, label: "Atenção" },
  { value: 4, label: "Precisa de reparo" },
  { value: 5, label: "Fora de serviço" }
];

const initialStorageForm = {
  name: "",
  locationNote: ""
};

const initialEquipmentForm = {
  storageId: "",
  name: "",
  type: "1",
  tagCode: "",
  brand: "",
  model: "",
  sizeLabel: "",
  currentCondition: "2"
};

export function EquipmentPage() {
  const { token } = useSession();
  const [storages, setStorages] = useState<Storage[]>([]);
  const [equipment, setEquipment] = useState<EquipmentItem[]>([]);
  const [selectedEquipmentId, setSelectedEquipmentId] = useState<string>("");
  const [history, setHistory] = useState<EquipmentHistory | null>(null);
  const [storageForm, setStorageForm] = useState(initialStorageForm);
  const [equipmentForm, setEquipmentForm] = useState(initialEquipmentForm);
  const [isLoading, setIsLoading] = useState(true);
  const [isSavingStorage, setIsSavingStorage] = useState(false);
  const [isSavingEquipment, setIsSavingEquipment] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      return;
    }

    void loadData(token);
  }, [token]);

  useEffect(() => {
    if (!token || !selectedEquipmentId) {
      setHistory(null);
      return;
    }

    void loadHistory(token, selectedEquipmentId);
  }, [selectedEquipmentId, token]);

  async function loadData(currentToken: string) {
    try {
      setIsLoading(true);
      setError(null);
      const [storagesData, equipmentData] = await Promise.all([
        getStorages(currentToken),
        getEquipment(currentToken)
      ]);

      setStorages(storagesData);
      setEquipment(equipmentData);

      if (!selectedEquipmentId && equipmentData.length > 0) {
        setSelectedEquipmentId(equipmentData[0].id);
      }
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar os equipamentos.");
    } finally {
      setIsLoading(false);
    }
  }

  async function loadHistory(currentToken: string, equipmentId: string) {
    try {
      setError(null);
      setHistory(await getEquipmentHistory(currentToken, equipmentId));
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar o histórico do item.");
    }
  }

  async function handleStorageSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSavingStorage(true);
      setError(null);
      await createStorage(token, {
        name: storageForm.name,
        locationNote: storageForm.locationNote || undefined
      });
      setStorageForm(initialStorageForm);
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar o depósito.");
    } finally {
      setIsSavingStorage(false);
    }
  }

  async function handleEquipmentSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSavingEquipment(true);
      setError(null);
      await createEquipment(token, {
        storageId: equipmentForm.storageId,
        name: equipmentForm.name,
        type: Number(equipmentForm.type),
        tagCode: equipmentForm.tagCode || undefined,
        brand: equipmentForm.brand || undefined,
        model: equipmentForm.model || undefined,
        sizeLabel: equipmentForm.sizeLabel || undefined,
        currentCondition: Number(equipmentForm.currentCondition)
      });
      setEquipmentForm(initialEquipmentForm);
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar o equipamento.");
    } finally {
      setIsSavingEquipment(false);
    }
  }

  const activeEquipment = equipment.filter((item) => item.isActive);
  const inAttention = equipment.filter((item) => item.condition !== "Excellent" && item.condition !== "Good").length;

  return (
    <div className="space-y-6">
      <PageHero
        eyebrow="Equipamentos"
        title="Inventário vivo, conectado ao uso real das aulas e ao histórico de manutenção."
        description="O equipamento passa a ser tratado como ativo operacional, com depósito, condição, uso acumulado e rastreabilidade por aula."
        stats={[
          { label: "Itens", value: String(equipment.length) },
          { label: "Ativos", value: String(activeEquipment.length) },
          { label: "Atenção", value: String(inAttention) },
          { label: "Depósitos", value: String(storages.length) }
        ]}
      />

      {isLoading ? <LoadingBlock label="Carregando inventário" /> : null}
      {error ? <ErrorBlock message={error} /> : null}

      <div className="grid gap-4 xl:grid-cols-[0.82fr_1.18fr]">
        <div className="space-y-4">
          <GlassCard title="Novo depósito" description="Crie bases de guarda e preparação do material da escola.">
            <form className="grid gap-3" onSubmit={handleStorageSubmit}>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Nome do depósito"
                value={storageForm.name}
                onChange={(event) => setStorageForm((current) => ({ ...current, name: event.target.value }))}
                required
              />
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Localização ou observação"
                value={storageForm.locationNote}
                onChange={(event) => setStorageForm((current) => ({ ...current, locationNote: event.target.value }))}
              />
              <button
                className="rounded-full border border-transparent bg-[var(--q-grad-brand)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95"
                type="submit"
                disabled={isSavingStorage}
              >
                {isSavingStorage ? "Salvando" : "Criar depósito"}
              </button>
            </form>
          </GlassCard>

          <GlassCard title="Novo equipamento" description="Cadastre ativos com tipo, código interno e condição inicial.">
            <form className="grid gap-3" onSubmit={handleEquipmentSubmit}>
              <select
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                value={equipmentForm.storageId}
                onChange={(event) => setEquipmentForm((current) => ({ ...current, storageId: event.target.value }))}
                required
              >
                <option value="">Selecione o depósito</option>
                {storages.map((storage) => (
                  <option key={storage.id} value={storage.id}>
                    {storage.name}
                  </option>
                ))}
              </select>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Nome do item"
                value={equipmentForm.name}
                onChange={(event) => setEquipmentForm((current) => ({ ...current, name: event.target.value }))}
                required
              />
              <div className="grid gap-3 md:grid-cols-2">
                <select
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  value={equipmentForm.type}
                  onChange={(event) => setEquipmentForm((current) => ({ ...current, type: event.target.value }))}
                >
                  {equipmentTypeOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
                <select
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  value={equipmentForm.currentCondition}
                  onChange={(event) => setEquipmentForm((current) => ({ ...current, currentCondition: event.target.value }))}
                >
                  {conditionOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  placeholder="Tag / código"
                  value={equipmentForm.tagCode}
                  onChange={(event) => setEquipmentForm((current) => ({ ...current, tagCode: event.target.value }))}
                />
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  placeholder="Marca"
                  value={equipmentForm.brand}
                  onChange={(event) => setEquipmentForm((current) => ({ ...current, brand: event.target.value }))}
                />
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  placeholder="Modelo"
                  value={equipmentForm.model}
                  onChange={(event) => setEquipmentForm((current) => ({ ...current, model: event.target.value }))}
                />
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  placeholder="Tamanho"
                  value={equipmentForm.sizeLabel}
                  onChange={(event) => setEquipmentForm((current) => ({ ...current, sizeLabel: event.target.value }))}
                />
              </div>
              <button
                className="rounded-full border border-emerald-400/20 bg-emerald-400/10 px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-emerald-100 transition hover:bg-emerald-400/20"
                type="submit"
                disabled={isSavingEquipment}
              >
                {isSavingEquipment ? "Salvando" : "Cadastrar equipamento"}
              </button>
            </form>
          </GlassCard>
        </div>

        <GlassCard title="Inventário da escola" description="Selecione um item para inspecionar uso por aula e manutenções registradas.">
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
              <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                <tr>
                  <th className="pb-3">Item</th>
                  <th className="pb-3">Tipo</th>
                  <th className="pb-3">Depósito</th>
                  <th className="pb-3">Uso</th>
                  <th className="pb-3">Condição</th>
                </tr>
              </thead>
              <tbody>
                {equipment.map((item) => (
                  <tr
                    key={item.id}
                    className={`cursor-pointer border-t border-[var(--q-border)] ${selectedEquipmentId === item.id ? "bg-[var(--q-info-bg)]" : ""}`}
                    onClick={() => setSelectedEquipmentId(item.id)}
                  >
                    <td className="py-3">
                      <div className="font-medium text-[var(--q-text)]">{item.name}</div>
                      <div className="mt-1 text-xs text-[var(--q-muted)]">{item.tagCode || "Sem tag"}</div>
                    </td>
                    <td className="py-3">{item.type}</td>
                    <td className="py-3">{item.storageName}</td>
                    <td className="py-3">{formatMinutes(item.totalUsageMinutes)}</td>
                    <td className="py-3">
                      <StatusBadge value={item.condition} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </GlassCard>
      </div>

      <GlassCard title="Histórico do item" description="Linha do tempo de uso por aula e intervenções de manutenção por equipamento.">
        {history ? (
          <div className="grid gap-4 xl:grid-cols-[1fr_1fr]">
            <div>
              <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                <div className="text-sm font-medium text-[var(--q-text)]">{history.equipment.name}</div>
                <div className="mt-1 text-sm text-[var(--q-text-2)]">
                  {history.equipment.type} • {history.equipment.storageName}
                </div>
                <div className="mt-3 flex flex-wrap gap-2">
                  <StatusBadge value={history.equipment.condition} />
                  <StatusBadge value={history.equipment.isActive ? "Active" : "Inactive"} />
                </div>
              </div>

              <div className="mt-4 space-y-3">
                {history.usage.map((item) => (
                  <div key={item.id} className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                    <div className="text-sm font-medium text-[var(--q-text)]">{formatMinutes(item.usageMinutes)} registrados</div>
                    <div className="mt-1 text-sm text-[var(--q-text-2)]">Aula: {item.lessonId ?? "-"}</div>
                    <div className="mt-1 text-sm text-[var(--q-text-2)]">Condição final: {item.conditionAfter}</div>
                    <div className="mt-3 text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                      {formatDateTime(item.recordedAtUtc)}
                    </div>
                  </div>
                ))}
              </div>
            </div>

            <div className="space-y-3">
              {history.maintenance.map((item) => (
                <div key={item.id} className="rounded-[22px] border border-[var(--q-warning)]/25 bg-[var(--q-warning-bg)] p-4">
                  <div className="text-sm font-medium text-[var(--q-text)]">{item.description}</div>
                  <div className="mt-1 text-sm text-[var(--q-text-2)]">
                    Uso no serviço: {formatMinutes(item.usageMinutesAtService)}
                  </div>
                  <div className="mt-1 text-sm text-[var(--q-text-2)]">Responsável: {item.performedBy || "Não informado"}</div>
                  <div className="mt-3 text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                    {formatDateTime(item.serviceDateUtc)}
                  </div>
                </div>
              ))}
              {history.maintenance.length === 0 ? (
                <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
                  Nenhuma manutenção registrada para este item.
                </div>
              ) : null}
            </div>
          </div>
        ) : (
          <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
            Selecione um item para carregar o histórico.
          </div>
        )}
      </GlassCard>
    </div>
  );
}
