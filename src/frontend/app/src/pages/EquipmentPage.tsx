import { type ReactNode, useEffect, useMemo, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock, StatusBadge } from "../components/OperationsUi";
import { formatCurrency, formatDateTime, formatMinutes } from "../lib/formatters";
import {
  createEquipment,
  createEquipmentKit,
  createStorage,
  getEquipment,
  getEquipmentHistory,
  getEquipmentKits,
  getStorages,
  type EquipmentHistory,
  type EquipmentItem,
  type EquipmentKit,
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

const ownershipOptions = [
  { value: 1, label: "Equipamento da escola" },
  { value: 2, label: "Equipamento de terceiro" }
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
  category: "",
  tagCode: "",
  brand: "",
  model: "",
  sizeLabel: "",
  currentCondition: "2",
  ownershipType: "1",
  ownerDisplayName: ""
};

const initialKitForm = {
  name: "",
  description: "",
  equipmentIds: [] as string[]
};

export function EquipmentPage() {
  const { token } = useSession();
  const [storages, setStorages] = useState<Storage[]>([]);
  const [equipment, setEquipment] = useState<EquipmentItem[]>([]);
  const [kits, setKits] = useState<EquipmentKit[]>([]);
  const [selectedEquipmentId, setSelectedEquipmentId] = useState<string>("");
  const [history, setHistory] = useState<EquipmentHistory | null>(null);
  const [storageForm, setStorageForm] = useState(initialStorageForm);
  const [equipmentForm, setEquipmentForm] = useState(initialEquipmentForm);
  const [kitForm, setKitForm] = useState(initialKitForm);
  const [isLoading, setIsLoading] = useState(true);
  const [isSavingStorage, setIsSavingStorage] = useState(false);
  const [isSavingEquipment, setIsSavingEquipment] = useState(false);
  const [isSavingKit, setIsSavingKit] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const attentionEquipment = useMemo(
    () => equipment.filter((item) => item.availabilityStatus !== "Available").length,
    [equipment]
  );

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
      const [storagesData, equipmentData, kitsData] = await Promise.all([
        getStorages(currentToken),
        getEquipment(currentToken),
        getEquipmentKits(currentToken)
      ]);

      setStorages(storagesData);
      setEquipment(equipmentData);
      setKits(kitsData);

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
        category: equipmentForm.category || undefined,
        tagCode: equipmentForm.tagCode || undefined,
        brand: equipmentForm.brand || undefined,
        model: equipmentForm.model || undefined,
        sizeLabel: equipmentForm.sizeLabel || undefined,
        currentCondition: Number(equipmentForm.currentCondition),
        ownershipType: Number(equipmentForm.ownershipType),
        ownerDisplayName: Number(equipmentForm.ownershipType) === 2 ? equipmentForm.ownerDisplayName || undefined : undefined
      });
      setEquipmentForm(initialEquipmentForm);
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar o equipamento.");
    } finally {
      setIsSavingEquipment(false);
    }
  }

  async function handleKitSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSavingKit(true);
      setError(null);
      await createEquipmentKit(token, {
        name: kitForm.name,
        description: kitForm.description || undefined,
        equipmentIds: kitForm.equipmentIds
      });
      setKitForm(initialKitForm);
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar o kit.");
    } finally {
      setIsSavingKit(false);
    }
  }

  return (
    <div className="space-y-6">
      <PageHero
        eyebrow="Equipamentos"
        title="Disponibilidade real, kits operacionais e ciclo de vida do equipamento em um só painel."
        description="A base da escola agora enxerga quem é dono do item, se ele está reservado na agenda, em qual kit participa e quanto já gerou de custo ou receita em manutenção."
        stats={[
          { label: "Itens", value: String(equipment.length) },
          { label: "Kits", value: String(kits.length) },
          { label: "Em atenção", value: String(attentionEquipment) },
          { label: "Depósitos", value: String(storages.length) }
        ]}
      />

      {isLoading ? <LoadingBlock label="Carregando inventário" /> : null}
      {error ? <ErrorBlock message={error} /> : null}

      <div className="grid gap-4 xl:grid-cols-[0.92fr_1.08fr]">
        <div className="space-y-4">
          <GlassCard title="Novo depósito">
            <form className="grid gap-3" onSubmit={handleStorageSubmit}>
              <Field
                placeholder="Nome do depósito"
                value={storageForm.name}
                onChange={(value) => setStorageForm((current) => ({ ...current, name: value }))}
                required
              />
              <Field
                placeholder="Localização ou observação"
                value={storageForm.locationNote}
                onChange={(value) => setStorageForm((current) => ({ ...current, locationNote: value }))}
              />
              <PrimaryButton disabled={isSavingStorage}>
                {isSavingStorage ? "Salvando" : "Criar depósito"}
              </PrimaryButton>
            </form>
          </GlassCard>

          <GlassCard title="Ficha do equipamento">
            <form className="grid gap-3" onSubmit={handleEquipmentSubmit}>
              <SelectField
                value={equipmentForm.storageId}
                onChange={(value) => setEquipmentForm((current) => ({ ...current, storageId: value }))}
                options={[{ value: "", label: "Selecione o depósito" }, ...storages.map((storage) => ({ value: storage.id, label: storage.name }))]}
                required
              />
              <div className="grid gap-3 md:grid-cols-2">
                <Field
                  placeholder="Nome do item"
                  value={equipmentForm.name}
                  onChange={(value) => setEquipmentForm((current) => ({ ...current, name: value }))}
                  required
                />
                <Field
                  placeholder="Categoria livre"
                  value={equipmentForm.category}
                  onChange={(value) => setEquipmentForm((current) => ({ ...current, category: value }))}
                />
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                <SelectField
                  value={equipmentForm.type}
                  onChange={(value) => setEquipmentForm((current) => ({ ...current, type: value }))}
                  options={equipmentTypeOptions.map((option) => ({ value: String(option.value), label: option.label }))}
                />
                <SelectField
                  value={equipmentForm.currentCondition}
                  onChange={(value) => setEquipmentForm((current) => ({ ...current, currentCondition: value }))}
                  options={conditionOptions.map((option) => ({ value: String(option.value), label: option.label }))}
                />
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                <SelectField
                  value={equipmentForm.ownershipType}
                  onChange={(value) => setEquipmentForm((current) => ({ ...current, ownershipType: value }))}
                  options={ownershipOptions.map((option) => ({ value: String(option.value), label: option.label }))}
                />
                <Field
                  placeholder="Proprietário / parceiro"
                  value={equipmentForm.ownerDisplayName}
                  onChange={(value) => setEquipmentForm((current) => ({ ...current, ownerDisplayName: value }))}
                  disabled={equipmentForm.ownershipType !== "2"}
                />
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                <Field
                  placeholder="Tag / código"
                  value={equipmentForm.tagCode}
                  onChange={(value) => setEquipmentForm((current) => ({ ...current, tagCode: value }))}
                />
                <Field
                  placeholder="Marca"
                  value={equipmentForm.brand}
                  onChange={(value) => setEquipmentForm((current) => ({ ...current, brand: value }))}
                />
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                <Field
                  placeholder="Modelo"
                  value={equipmentForm.model}
                  onChange={(value) => setEquipmentForm((current) => ({ ...current, model: value }))}
                />
                <Field
                  placeholder="Tamanho"
                  value={equipmentForm.sizeLabel}
                  onChange={(value) => setEquipmentForm((current) => ({ ...current, sizeLabel: value }))}
                />
              </div>
              <PrimaryButton disabled={isSavingEquipment}>
                {isSavingEquipment ? "Salvando" : "Cadastrar equipamento"}
              </PrimaryButton>
            </form>
          </GlassCard>

          <GlassCard title="Kit operacional">
            <form className="grid gap-3" onSubmit={handleKitSubmit}>
              <Field
                placeholder="Nome do kit"
                value={kitForm.name}
                onChange={(value) => setKitForm((current) => ({ ...current, name: value }))}
                required
              />
              <Field
                placeholder="Descrição"
                value={kitForm.description}
                onChange={(value) => setKitForm((current) => ({ ...current, description: value }))}
              />
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Itens do kit</span>
                <div className="grid max-h-52 gap-2 overflow-y-auto rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-3">
                  {equipment.map((item) => (
                    <label key={item.id} className="flex items-center gap-3 text-sm text-[var(--q-text)]">
                      <input
                        type="checkbox"
                        checked={kitForm.equipmentIds.includes(item.id)}
                        onChange={(event) =>
                          setKitForm((current) => ({
                            ...current,
                            equipmentIds: event.target.checked
                              ? [...current.equipmentIds, item.id]
                              : current.equipmentIds.filter((value) => value !== item.id)
                          }))
                        }
                      />
                      <span>{item.name}</span>
                      <span className="text-xs uppercase tracking-[0.18em] text-[var(--q-muted)]">{item.type}</span>
                    </label>
                  ))}
                </div>
              </label>
              <PrimaryButton disabled={isSavingKit}>
                {isSavingKit ? "Salvando" : "Criar kit"}
              </PrimaryButton>
            </form>
          </GlassCard>
        </div>

        <div className="space-y-4">
          <GlassCard title="Inventário da escola">
            <div className="overflow-x-auto">
              <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
                <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                  <tr>
                    <th className="pb-3">Item</th>
                    <th className="pb-3">Propriedade</th>
                    <th className="pb-3">Kit</th>
                    <th className="pb-3">Disponibilidade</th>
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
                        <div className="mt-1 text-xs text-[var(--q-muted)]">
                          {item.type}{item.category ? ` · ${item.category}` : ""}{item.tagCode ? ` · ${item.tagCode}` : ""}
                        </div>
                      </td>
                      <td className="py-3">
                        <div className="font-medium text-[var(--q-text)]">{item.ownershipType === "ThirdParty" ? "Terceiro" : "Escola"}</div>
                        <div className="mt-1 text-xs text-[var(--q-muted)]">{item.ownerDisplayName || "Patrimônio próprio"}</div>
                      </td>
                      <td className="py-3">{item.kitName || "-"}</td>
                      <td className="py-3"><StatusBadge value={item.availabilityStatus || "Available"} /></td>
                      <td className="py-3"><StatusBadge value={item.condition} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </GlassCard>

          <GlassCard title="Kits cadastrados">
            <div className="grid gap-3">
              {kits.length === 0 ? (
                <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
                  Nenhum kit operacional cadastrado.
                </div>
              ) : kits.map((kit) => (
                <div key={kit.id} className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <div className="text-sm font-medium text-[var(--q-text)]">{kit.name}</div>
                      <div className="mt-1 text-sm text-[var(--q-text-2)]">{kit.description || "Sem descrição"}</div>
                    </div>
                    <StatusBadge value={kit.isActive ? "Active" : "Inactive"} />
                  </div>
                  <div className="mt-3 flex flex-wrap gap-2">
                    {kit.items.map((item) => (
                      <span key={`${kit.id}-${item.equipmentId}`} className="rounded-full border border-[var(--q-border)] px-3 py-1 text-xs text-[var(--q-text)]">
                        {item.name}
                      </span>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </GlassCard>
        </div>
      </div>

      <GlassCard title="Vida útil do equipamento">
        {history ? (
          <div className="grid gap-4 xl:grid-cols-[0.78fr_1.22fr]">
            <div className="space-y-4">
              <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                <div className="text-lg font-semibold text-[var(--q-text)]">{history.equipment.name}</div>
                <div className="mt-1 text-sm text-[var(--q-text-2)]">
                  {history.equipment.type} · {history.equipment.storageName}
                </div>
                <div className="mt-3 flex flex-wrap gap-2">
                  <StatusBadge value={history.equipment.condition} />
                  <StatusBadge value={history.equipment.ownershipType} />
                  {history.equipment.kitName ? <StatusBadge value={history.equipment.kitName} /> : null}
                </div>
              </div>

              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-1">
                <MetricCard label="Uso acumulado" value={formatMinutes(history.lifecycle.usageMinutes)} />
                <MetricCard label="Serviços" value={String(history.lifecycle.servicesCount)} />
                <MetricCard label="Reservas" value={String(history.lifecycle.reservationsCount)} />
                <MetricCard label="Custo gerado" value={formatCurrency(history.lifecycle.maintenanceExpense)} />
                <MetricCard label="Receita gerada" value={formatCurrency(history.lifecycle.maintenanceRevenue)} />
              </div>
            </div>

            <div className="space-y-3">
              {history.lifecycle.timeline.map((item, index) => (
                <div key={`${item.atUtc}-${index}`} className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface)] p-4">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div className="text-sm font-medium text-[var(--q-text)]">{item.title}</div>
                    <StatusBadge value={item.kind} />
                  </div>
                  <div className="mt-1 text-sm text-[var(--q-text-2)]">{item.detail}</div>
                  <div className="mt-3 text-xs uppercase tracking-[0.2em] text-[var(--q-muted)]">{formatDateTime(item.atUtc)}</div>
                </div>
              ))}
            </div>
          </div>
        ) : (
          <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
            Selecione um item para carregar a linha do tempo de vida útil.
          </div>
        )}
      </GlassCard>
    </div>
  );
}

function Field({
  placeholder,
  value,
  onChange,
  required = false,
  disabled = false
}: {
  placeholder: string;
  value: string;
  onChange: (value: string) => void;
  required?: boolean;
  disabled?: boolean;
}) {
  return (
    <input
      className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none disabled:opacity-70"
      placeholder={placeholder}
      value={value}
      onChange={(event) => onChange(event.target.value)}
      required={required}
      disabled={disabled}
    />
  );
}

function SelectField({
  value,
  onChange,
  options,
  required = false,
  disabled = false
}: {
  value: string;
  onChange: (value: string) => void;
  options: Array<{ value: string; label: string }>;
  required?: boolean;
  disabled?: boolean;
}) {
  return (
    <select
      className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none disabled:opacity-70"
      value={value}
      onChange={(event) => onChange(event.target.value)}
      required={required}
      disabled={disabled}
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

function MetricCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface)] p-4">
      <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">{label}</div>
      <div className="mt-3 text-2xl font-semibold text-[var(--q-text)]">{value}</div>
    </div>
  );
}
