import { useEffect, useState } from "react";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock, StatusBadge } from "../components/OperationsUi";
import { SchoolBaseMapPicker } from "../components/SchoolBaseMapPicker";
import { useSession } from "../auth/SessionContext";
import {
  deleteSystemSchool,
  getSystemSchoolDetails,
  getSystemSchools,
  updateSystemSchool,
  type SystemSchoolDetails,
  type SystemSchoolSummary,
  type UpdateSystemSchoolPayload
} from "../lib/platform-api";

const emptyForm: UpdateSystemSchoolPayload = {
  legalName: "",
  displayName: "",
  cnpj: "",
  baseBeachName: "",
  baseLatitude: undefined,
  baseLongitude: undefined,
  logoDataUrl: "",
  postalCode: "",
  street: "",
  streetNumber: "",
  addressComplement: "",
  neighborhood: "",
  city: "",
  state: "",
  ownerFullName: "",
  ownerCpf: "",
  ownerPhone: "",
  ownerPostalCode: "",
  ownerStreet: "",
  ownerStreetNumber: "",
  ownerAddressComplement: "",
  ownerNeighborhood: "",
  ownerCity: "",
  ownerState: "",
  ownerIsActive: true,
  status: "Active",
  timezone: "America/Sao_Paulo",
  currencyCode: "BRL"
};

export function SystemSchoolsDirectoryPage() {
  const { token } = useSession();
  const [schools, setSchools] = useState<SystemSchoolSummary[]>([]);
  const [selectedSchoolId, setSelectedSchoolId] = useState<string | null>(null);
  const [selectedSchool, setSelectedSchool] = useState<SystemSchoolDetails | null>(null);
  const [form, setForm] = useState<UpdateSystemSchoolPayload>(emptyForm);
  const [search, setSearch] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingDetails, setIsLoadingDetails] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [schoolPostalCodeFeedback, setSchoolPostalCodeFeedback] = useState<string | null>(null);
  const [ownerPostalCodeFeedback, setOwnerPostalCodeFeedback] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      return;
    }

    void loadSchools(token);
  }, [token]);

  async function loadSchools(currentToken: string) {
    try {
      setIsLoading(true);
      setError(null);
      const items = await getSystemSchools(currentToken);
      setSchools(items);
      if (!selectedSchoolId && items.length > 0) {
        await handleSelectSchool(items[0].id, currentToken);
      } else if (selectedSchoolId && !items.some((item) => item.id === selectedSchoolId)) {
        setSelectedSchoolId(null);
        setSelectedSchool(null);
        setForm(emptyForm);
      }
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar as escolas.");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleSelectSchool(schoolId: string, currentToken = token) {
    if (!currentToken) {
      return;
    }

    try {
      setIsLoadingDetails(true);
      setError(null);
      setSuccessMessage(null);
      const school = await getSystemSchoolDetails(currentToken, schoolId);
      setSelectedSchoolId(schoolId);
      setSelectedSchool(school);
      setForm({
        legalName: school.legalName,
        displayName: school.displayName,
        cnpj: school.cnpj ?? "",
        baseBeachName: school.baseBeachName ?? "",
        baseLatitude: school.baseLatitude,
        baseLongitude: school.baseLongitude,
        logoDataUrl: school.logoDataUrl ?? "",
        postalCode: school.postalCode ?? "",
        street: school.street ?? "",
        streetNumber: school.streetNumber ?? "",
        addressComplement: school.addressComplement ?? "",
        neighborhood: school.neighborhood ?? "",
        city: school.city ?? "",
        state: school.state ?? "",
        ownerFullName: school.owner?.fullName ?? "",
        ownerCpf: school.owner?.cpf ?? "",
        ownerPhone: school.owner?.phone ?? "",
        ownerPostalCode: school.owner?.postalCode ?? "",
        ownerStreet: school.owner?.street ?? "",
        ownerStreetNumber: school.owner?.streetNumber ?? "",
        ownerAddressComplement: school.owner?.addressComplement ?? "",
        ownerNeighborhood: school.owner?.neighborhood ?? "",
        ownerCity: school.owner?.city ?? "",
        ownerState: school.owner?.state ?? "",
        ownerIsActive: school.owner?.isActive ?? true,
        status: school.status,
        timezone: school.timezone,
        currencyCode: school.currencyCode
      });
      setSchoolPostalCodeFeedback(null);
      setOwnerPostalCodeFeedback(null);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar os detalhes da escola.");
    } finally {
      setIsLoadingDetails(false);
    }
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token || !selectedSchoolId) {
      return;
    }

    try {
      setIsSaving(true);
      setError(null);
      setSuccessMessage(null);
      await updateSystemSchool(token, selectedSchoolId, form);
      setSuccessMessage("Escola atualizada com sucesso.");
      await Promise.all([loadSchools(token), handleSelectSchool(selectedSchoolId, token)]);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível atualizar a escola.");
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDelete() {
    if (!token || !selectedSchoolId || !selectedSchool) {
      return;
    }

    const confirmed = window.confirm(
      `Deseja realmente excluir a escola ${selectedSchool.displayName}? Essa ação remove os dados do tenant.`
    );

    if (!confirmed) {
      return;
    }

    try {
      setIsDeleting(true);
      setError(null);
      setSuccessMessage(null);
      await deleteSystemSchool(token, selectedSchoolId);
      setSuccessMessage("Escola excluída com sucesso.");
      setSelectedSchoolId(null);
      setSelectedSchool(null);
      setForm(emptyForm);
      await loadSchools(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível excluir a escola.");
    } finally {
      setIsDeleting(false);
    }
  }

  async function handleToggleSchoolStatus() {
    if (!token || !selectedSchoolId || isSaving || isDeleting) {
      return;
    }

    const nextStatus = form.status === "Suspended" ? "Active" : "Suspended";
    const nextStatusLabel = nextStatus === "Active" ? "reativada" : "inativada";

    try {
      setIsSaving(true);
      setError(null);
      setSuccessMessage(null);
      await updateSystemSchool(token, selectedSchoolId, {
        ...form,
        status: nextStatus
      });
      setSuccessMessage(`Escola ${nextStatusLabel} com sucesso.`);
      await Promise.all([loadSchools(token), handleSelectSchool(selectedSchoolId, token)]);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível atualizar o status da escola.");
    } finally {
      setIsSaving(false);
    }
  }

  async function handleLogoChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) {
      setForm((current) => ({ ...current, logoDataUrl: "" }));
      return;
    }

    if (!file.type.startsWith("image/")) {
      setError("Selecione um arquivo de imagem válido para a logo da escola.");
      return;
    }

    if (file.size > 2 * 1024 * 1024) {
      setError("A logo deve ter no máximo 2 MB.");
      return;
    }

    const logoDataUrl = await readFileAsDataUrl(file);
    setError(null);
    setForm((current) => ({ ...current, logoDataUrl }));
  }

  const normalizedSearch = search.trim().toLowerCase();
  const filteredSchools = normalizedSearch
    ? schools.filter((item) =>
        item.displayName.toLowerCase().includes(normalizedSearch) ||
        item.legalName.toLowerCase().includes(normalizedSearch))
    : schools;

  return (
    <div className="space-y-6">
      <PageHero
        eyebrow="Administração do sistema"
        title="Consulte, atualize e exclua escolas já provisionadas na plataforma."
        description="Use esta área para revisar os tenants cadastrados, ajustar branding e manter os dados institucionais atualizados."
        stats={[
          { label: "Escolas", value: String(schools.length) },
          { label: "Busca ativa", value: normalizedSearch ? "Filtrada" : "Completa" },
          { label: "Selecionada", value: selectedSchool?.displayName ?? "-" },
          { label: "Ação destrutiva", value: "Protegida" }
        ]}
      />

      {isLoading ? <LoadingBlock label="Carregando escolas" /> : null}
      {error ? <ErrorBlock message={error} /> : null}
      {successMessage ? (
        <div className="rounded-[24px] border border-[var(--q-success)] bg-[var(--q-success-bg)] px-5 py-4 text-sm text-[var(--q-text)]">
          {successMessage}
        </div>
      ) : null}

      <div className="grid gap-4 xl:grid-cols-[0.78fr_1.22fr]">
        <GlassCard title="Consulta de escolas" description="Busque uma escola e abra a ficha completa para edição ou exclusão.">
          <div className="space-y-4">
            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Buscar escola</span>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Digite o nome da escola"
              />
            </label>

            <div className="space-y-3">
              {filteredSchools.map((school) => (
                <button
                  type="button"
                  key={school.id}
                  onClick={() => void handleSelectSchool(school.id)}
                  className={`w-full rounded-[22px] border px-4 py-4 text-left transition ${
                    selectedSchoolId === school.id
                      ? "border-[var(--q-info)]/40 bg-[var(--q-info-bg)]"
                      : "border-[var(--q-border)] bg-[var(--q-surface-2)] hover:bg-[var(--q-info-bg)]/45"
                  }`}
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <div className="text-sm font-semibold text-[var(--q-text)]">{school.displayName}</div>
                      <div className="mt-1 truncate text-xs text-[var(--q-text-2)]">{school.legalName}</div>
                    </div>
                    <StatusBadge value={school.status} />
                  </div>
                  <div className="mt-3 text-xs text-[var(--q-muted)]">
                    Owner inicial: {school.ownerName ?? "Não identificado"}
                  </div>
                </button>
              ))}

              {!isLoading && filteredSchools.length === 0 ? (
                <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
                  Nenhuma escola encontrada com esse filtro.
                </div>
              ) : null}
            </div>
          </div>
        </GlassCard>

        <GlassCard
          title="Ficha da escola"
          description={selectedSchool ? "Ajuste os dados da escola selecionada." : "Selecione uma escola para editar."}
        >
          {!selectedSchoolId ? (
            <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-6 text-sm text-[var(--q-text-2)]">
              Escolha uma escola na lista para abrir a ficha de edição.
            </div>
          ) : isLoadingDetails ? (
            <LoadingBlock label="Carregando ficha da escola" />
          ) : (
            <form className="space-y-6" onSubmit={handleSubmit}>
              <section className="space-y-4">
                <h3 className="text-sm font-semibold uppercase tracking-[0.16em] text-[var(--q-muted)]">Dados da escola</h3>
                <div className="grid gap-4 md:grid-cols-2">
                  <FormField label="Razão social">
                    <input className={inputClassName} value={form.legalName} onChange={(event) => setForm((current) => ({ ...current, legalName: event.target.value }))} required />
                  </FormField>
                  <FormField label="Nome de exibição">
                    <input className={inputClassName} value={form.displayName} onChange={(event) => setForm((current) => ({ ...current, displayName: event.target.value }))} required />
                  </FormField>
                </div>
                <div className="grid gap-4 md:grid-cols-[1fr_0.8fr]">
                  <FormField label="CNPJ da escola">
                    <input className={inputClassName} value={form.cnpj ?? ""} onChange={(event) => setForm((current) => ({ ...current, cnpj: formatCnpj(event.target.value) }))} placeholder="Opcional" />
                  </FormField>
                  <FormField label="Praia base da escola">
                    <input className={inputClassName} value={form.baseBeachName} onChange={(event) => setForm((current) => ({ ...current, baseBeachName: event.target.value }))} required />
                  </FormField>
                </div>
              </section>

              <section className="space-y-4">
                <h3 className="text-sm font-semibold uppercase tracking-[0.16em] text-[var(--q-muted)]">Endereço da escola</h3>
                <div className="grid gap-4 md:grid-cols-[0.42fr_1fr_0.34fr]">
                  <FormField label="CEP">
                    <input className={inputClassName} value={form.postalCode} onChange={(event) => setForm((current) => ({ ...current, postalCode: formatPostalCode(event.target.value) }))} onBlur={() => void lookupPostalCode(form.postalCode, "school", setForm, setSchoolPostalCodeFeedback)} placeholder="00000-000" required />
                  </FormField>
                  <FormField label="Logradouro">
                    <input className={inputClassName} value={form.street} onChange={(event) => setForm((current) => ({ ...current, street: event.target.value }))} required />
                  </FormField>
                  <FormField label="Número">
                    <input className={inputClassName} value={form.streetNumber} onChange={(event) => setForm((current) => ({ ...current, streetNumber: event.target.value }))} required />
                  </FormField>
                </div>
                {schoolPostalCodeFeedback ? <Feedback text={schoolPostalCodeFeedback} /> : null}
                <div className="grid gap-4 md:grid-cols-[1fr_1fr]">
                  <FormField label="Complemento">
                    <input className={inputClassName} value={form.addressComplement ?? ""} onChange={(event) => setForm((current) => ({ ...current, addressComplement: event.target.value }))} />
                  </FormField>
                  <FormField label="Bairro">
                    <input className={inputClassName} value={form.neighborhood} onChange={(event) => setForm((current) => ({ ...current, neighborhood: event.target.value }))} required />
                  </FormField>
                </div>
                <div className="grid gap-4 md:grid-cols-[0.86fr_120px]">
                  <FormField label="Cidade">
                    <input className={inputClassName} value={form.city} onChange={(event) => setForm((current) => ({ ...current, city: event.target.value }))} required />
                  </FormField>
                  <FormField label="Estado">
                    <input className={`${inputClassName} text-center uppercase`} value={form.state} onChange={(event) => setForm((current) => ({ ...current, state: event.target.value.toUpperCase().slice(0, 2) }))} maxLength={2} required />
                  </FormField>
                </div>
              </section>

              <section className="space-y-4">
                <h3 className="text-sm font-semibold uppercase tracking-[0.16em] text-[var(--q-muted)]">Localização da base no mapa</h3>
                <SchoolBaseMapPicker
                  value={{ latitude: form.baseLatitude, longitude: form.baseLongitude }}
                  onChange={(coordinates) => setForm((current) => ({ ...current, baseLatitude: coordinates.latitude, baseLongitude: coordinates.longitude }))}
                />
              </section>

              <section className="space-y-4">
                <h3 className="text-sm font-semibold uppercase tracking-[0.16em] text-[var(--q-muted)]">Proprietário inicial</h3>
                <div className="grid gap-4 md:grid-cols-2">
                  <FormField label="Nome completo">
                    <input className={inputClassName} value={form.ownerFullName} onChange={(event) => setForm((current) => ({ ...current, ownerFullName: event.target.value }))} required />
                  </FormField>
                  <FormField label="Telefone">
                    <input className={inputClassName} value={form.ownerPhone ?? ""} onChange={(event) => setForm((current) => ({ ...current, ownerPhone: formatPhone(event.target.value) }))} placeholder="(00) 00000-0000" />
                  </FormField>
                </div>
                <div className="grid gap-4 md:grid-cols-[0.9fr_auto]">
                  <FormField label="CPF">
                    <input className={inputClassName} value={form.ownerCpf} onChange={(event) => setForm((current) => ({ ...current, ownerCpf: formatCpf(event.target.value) }))} required />
                  </FormField>
                  <label className="flex items-end gap-3 text-sm text-[var(--q-text)]">
                    <input
                      type="checkbox"
                      checked={form.ownerIsActive}
                      onChange={(event) => setForm((current) => ({ ...current, ownerIsActive: event.target.checked }))}
                    />
                    <span>Proprietário ativo</span>
                  </label>
                </div>
              </section>

              <section className="space-y-4">
                <h3 className="text-sm font-semibold uppercase tracking-[0.16em] text-[var(--q-muted)]">Endereço do proprietário</h3>
                <div className="grid gap-4 md:grid-cols-[0.42fr_1fr_0.34fr]">
                  <FormField label="CEP">
                    <input className={inputClassName} value={form.ownerPostalCode} onChange={(event) => setForm((current) => ({ ...current, ownerPostalCode: formatPostalCode(event.target.value) }))} onBlur={() => void lookupPostalCode(form.ownerPostalCode, "owner", setForm, setOwnerPostalCodeFeedback)} placeholder="00000-000" required />
                  </FormField>
                  <FormField label="Logradouro">
                    <input className={inputClassName} value={form.ownerStreet} onChange={(event) => setForm((current) => ({ ...current, ownerStreet: event.target.value }))} required />
                  </FormField>
                  <FormField label="Número">
                    <input className={inputClassName} value={form.ownerStreetNumber} onChange={(event) => setForm((current) => ({ ...current, ownerStreetNumber: event.target.value }))} required />
                  </FormField>
                </div>
                {ownerPostalCodeFeedback ? <Feedback text={ownerPostalCodeFeedback} /> : null}
                <div className="grid gap-4 md:grid-cols-[1fr_1fr]">
                  <FormField label="Complemento">
                    <input className={inputClassName} value={form.ownerAddressComplement ?? ""} onChange={(event) => setForm((current) => ({ ...current, ownerAddressComplement: event.target.value }))} />
                  </FormField>
                  <FormField label="Bairro">
                    <input className={inputClassName} value={form.ownerNeighborhood} onChange={(event) => setForm((current) => ({ ...current, ownerNeighborhood: event.target.value }))} required />
                  </FormField>
                </div>
                <div className="grid gap-4 md:grid-cols-[0.86fr_120px]">
                  <FormField label="Cidade">
                    <input className={inputClassName} value={form.ownerCity} onChange={(event) => setForm((current) => ({ ...current, ownerCity: event.target.value }))} required />
                  </FormField>
                  <FormField label="Estado">
                    <input className={`${inputClassName} text-center uppercase`} value={form.ownerState} onChange={(event) => setForm((current) => ({ ...current, ownerState: event.target.value.toUpperCase().slice(0, 2) }))} maxLength={2} required />
                  </FormField>
                </div>
              </section>

              <section className="space-y-4">
                <h3 className="text-sm font-semibold uppercase tracking-[0.16em] text-[var(--q-muted)]">Branding e configuração</h3>
                <div className="grid gap-4 md:grid-cols-[1fr_180px]">
                  <FormField label="Logo da escola">
                    <input
                      className={`${inputClassName} file:mr-3 file:rounded-full file:border-0 file:bg-[var(--q-info-bg)] file:px-3 file:py-2 file:text-sm file:text-[var(--q-text)]`}
                      type="file"
                      accept="image/png,image/jpeg,image/webp,image/svg+xml"
                      onChange={(event) => void handleLogoChange(event)}
                    />
                  </FormField>
                  <div className="grid gap-2 text-sm text-[var(--q-text)]">
                    <span>Prévia</span>
                    <div className="flex min-h-[104px] items-center justify-center rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                      <img
                        src={form.logoDataUrl || "/branding/logo.png"}
                        alt="Prévia da logo da escola"
                        className="h-auto w-[120px] object-contain"
                      />
                    </div>
                  </div>
                </div>
                <div className="grid gap-4 md:grid-cols-[180px_1fr_160px]">
                  <FormField label="Situação">
                    <select
                      className={inputClassName}
                      value={form.status}
                      onChange={(event) => setForm((current) => ({ ...current, status: event.target.value }))}
                    >
                      <option value="Active">Ativa</option>
                      <option value="Suspended">Inativa</option>
                    </select>
                  </FormField>
                  <FormField label="Fuso horário">
                    <input className={inputClassName} value={form.timezone ?? ""} onChange={(event) => setForm((current) => ({ ...current, timezone: event.target.value }))} />
                  </FormField>
                  <FormField label="Moeda">
                    <input className={`${inputClassName} uppercase`} value={form.currencyCode ?? ""} onChange={(event) => setForm((current) => ({ ...current, currencyCode: event.target.value.toUpperCase() }))} />
                  </FormField>
                </div>
              </section>

              <div className="flex flex-wrap items-center gap-3">
                <button
                  type="submit"
                  disabled={isSaving || isDeleting}
                  style={{ backgroundImage: "var(--q-grad-brand)" }}
                  className="rounded-2xl bg-[var(--q-navy)] px-5 py-3 text-sm font-medium text-white shadow-[0_18px_40px_rgba(36,75,132,0.18)] transition hover:opacity-95 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {isSaving ? "Salvando..." : "Salvar alterações"}
                </button>
                <button
                  type="button"
                  disabled={isSaving || isDeleting}
                  onClick={() => void handleToggleSchoolStatus()}
                  className={`rounded-2xl border px-5 py-3 text-sm font-medium transition disabled:cursor-not-allowed disabled:opacity-60 ${
                    form.status === "Suspended"
                      ? "border-[var(--q-success)]/35 bg-[var(--q-success-bg)] text-[var(--q-text)] hover:opacity-90"
                      : "border-[var(--q-warning)]/40 bg-[var(--q-warning-bg)] text-[var(--q-text)] hover:opacity-90"
                  }`}
                >
                  {form.status === "Suspended" ? "Reativar escola" : "Inativar escola"}
                </button>
                <button
                  type="button"
                  disabled={isSaving || isDeleting}
                  onClick={() => void handleDelete()}
                  className="rounded-2xl border border-[var(--q-danger)]/40 bg-[var(--q-danger-bg)] px-5 py-3 text-sm font-medium text-[var(--q-danger)] transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {isDeleting ? "Excluindo..." : "Excluir escola"}
                </button>
              </div>
            </form>
          )}
        </GlassCard>
      </div>
    </div>
  );
}

function FormField({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="grid gap-2 text-sm text-[var(--q-text)]">
      <span>{label}</span>
      {children}
    </label>
  );
}

function Feedback({ text }: { text: string }) {
  return <div className="text-xs text-[var(--q-text-2)]">{text}</div>;
}

const inputClassName =
  "w-full rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none";

function formatPhone(value: string) {
  const digits = value.replace(/\D/g, "").slice(0, 11);
  if (digits.length <= 2) return digits.length ? `(${digits}` : "";
  if (digits.length <= 7) return `(${digits.slice(0, 2)}) ${digits.slice(2)}`;
  return `(${digits.slice(0, 2)}) ${digits.slice(2, 7)}-${digits.slice(7)}`;
}

function formatPostalCode(value: string) {
  const digits = value.replace(/\D/g, "").slice(0, 8);
  if (digits.length <= 5) return digits;
  return `${digits.slice(0, 5)}-${digits.slice(5)}`;
}

function formatCpf(value: string) {
  const digits = value.replace(/\D/g, "").slice(0, 11);
  if (digits.length <= 3) return digits;
  if (digits.length <= 6) return `${digits.slice(0, 3)}.${digits.slice(3)}`;
  if (digits.length <= 9) return `${digits.slice(0, 3)}.${digits.slice(3, 6)}.${digits.slice(6)}`;
  return `${digits.slice(0, 3)}.${digits.slice(3, 6)}.${digits.slice(6, 9)}-${digits.slice(9)}`;
}

function formatCnpj(value: string) {
  const digits = value.replace(/\D/g, "").slice(0, 14);
  if (digits.length <= 2) return digits;
  if (digits.length <= 5) return `${digits.slice(0, 2)}.${digits.slice(2)}`;
  if (digits.length <= 8) return `${digits.slice(0, 2)}.${digits.slice(2, 5)}.${digits.slice(5)}`;
  if (digits.length <= 12) return `${digits.slice(0, 2)}.${digits.slice(2, 5)}.${digits.slice(5, 8)}/${digits.slice(8)}`;
  return `${digits.slice(0, 2)}.${digits.slice(2, 5)}.${digits.slice(5, 8)}/${digits.slice(8, 12)}-${digits.slice(12)}`;
}

async function lookupPostalCode(
  postalCode: string,
  target: "school" | "owner",
  setForm: React.Dispatch<React.SetStateAction<UpdateSystemSchoolPayload>>,
  setFeedback: (value: string | null) => void
) {
  const digits = postalCode.replace(/\D/g, "");
  if (digits.length !== 8) {
    return;
  }

  try {
    setFeedback(null);
    const response = await fetch(`https://viacep.com.br/ws/${digits}/json/`);
    if (!response.ok) {
      throw new Error("postal-code-lookup-failed");
    }

    const data = (await response.json()) as {
      erro?: boolean;
      logradouro?: string;
      complemento?: string;
      bairro?: string;
      localidade?: string;
      uf?: string;
    };

    if (data.erro) {
      setFeedback(target === "school" ? "CEP da escola não encontrado." : "CEP do proprietário não encontrado.");
      return;
    }

    setForm((current) => {
      if (target === "school") {
        return {
          ...current,
          street: data.logradouro?.trim() || current.street,
          addressComplement: data.complemento?.trim() || current.addressComplement,
          neighborhood: data.bairro?.trim() || current.neighborhood,
          city: data.localidade?.trim() || current.city,
          state: data.uf?.trim().toUpperCase() || current.state
        };
      }

      return {
        ...current,
        ownerStreet: data.logradouro?.trim() || current.ownerStreet,
        ownerAddressComplement: data.complemento?.trim() || current.ownerAddressComplement,
        ownerNeighborhood: data.bairro?.trim() || current.ownerNeighborhood,
        ownerCity: data.localidade?.trim() || current.ownerCity,
        ownerState: data.uf?.trim().toUpperCase() || current.ownerState
      };
    });

    setFeedback(target === "school"
      ? "Endereço da escola preenchido automaticamente pelo CEP."
      : "Endereço do proprietário preenchido automaticamente pelo CEP.");
  } catch {
    setFeedback(target === "school"
      ? "Não foi possível consultar o CEP da escola agora."
      : "Não foi possível consultar o CEP do proprietário agora.");
  }
}

function readFileAsDataUrl(file: File) {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result ?? ""));
    reader.onerror = () => reject(new Error("Não foi possível carregar a imagem da logo."));
    reader.readAsDataURL(file);
  });
}
