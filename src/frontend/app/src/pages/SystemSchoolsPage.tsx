import { useEffect, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock } from "../components/OperationsUi";
import { SchoolBaseMapPicker } from "../components/SchoolBaseMapPicker";
import {
  createSystemSchool,
  getSystemSchools,
  type CreateSystemSchoolPayload
} from "../lib/platform-api";

const initialForm: CreateSystemSchoolPayload = {
  legalName: "",
  displayName: "",
  cnpj: "",
  baseBeachName: "",
  baseLatitude: undefined,
  baseLongitude: undefined,
  postalCode: "",
  street: "",
  streetNumber: "",
  addressComplement: "",
  neighborhood: "",
  city: "",
  state: "",
  ownerFullName: "",
  ownerEmail: "",
  ownerCpf: "",
  ownerPhone: "",
  ownerPostalCode: "",
  ownerStreet: "",
  ownerStreetNumber: "",
  ownerAddressComplement: "",
  ownerNeighborhood: "",
  ownerCity: "",
  ownerState: "",
  timezone: "America/Sao_Paulo",
  currencyCode: "BRL",
  logoDataUrl: ""
};

export function SystemSchoolsPage() {
  const { token } = useSession();
  const [form, setForm] = useState(initialForm);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [schoolPostalCodeFeedback, setSchoolPostalCodeFeedback] = useState<string | null>(null);
  const [ownerPostalCodeFeedback, setOwnerPostalCodeFeedback] = useState<string | null>(null);
  const [isLookingUpSchoolPostalCode, setIsLookingUpSchoolPostalCode] = useState(false);
  const [isLookingUpOwnerPostalCode, setIsLookingUpOwnerPostalCode] = useState(false);
  const [isLookingUpSchoolCoordinates, setIsLookingUpSchoolCoordinates] = useState(false);
  const [schoolCoordinatesFeedback, setSchoolCoordinatesFeedback] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      return;
    }

    void loadSchools(token);
  }, [token]);

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

  async function loadSchools(currentToken: string) {
    try {
      setIsLoading(true);
      setError(null);
      await getSystemSchools(currentToken);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar as escolas.");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSaving(true);
      setError(null);
      setSuccessMessage(null);
      const result = await createSystemSchool(token, form);
      setForm(initialForm);
      setSchoolPostalCodeFeedback(null);
      setOwnerPostalCodeFeedback(null);
      setSchoolCoordinatesFeedback(null);
      setSuccessMessage(
        result.deliveryMode === "File" && result.outboxFilePath
          ? `Escola criada. A senha temporária do proprietário foi gravada no outbox local em ${result.outboxFilePath}.`
          : "Escola criada. A senha temporária do proprietário foi enviada para o e-mail informado."
      );
      await loadSchools(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível cadastrar a escola.");
    } finally {
      setIsSaving(false);
    }
  }

  function formatPhone(value: string) {
    const digits = value.replace(/\D/g, "").slice(0, 11);

    if (digits.length <= 2) {
      return digits.length ? `(${digits}` : "";
    }

    if (digits.length <= 7) {
      return `(${digits.slice(0, 2)}) ${digits.slice(2)}`;
    }

    return `(${digits.slice(0, 2)}) ${digits.slice(2, 7)}-${digits.slice(7)}`;
  }

  function formatPostalCode(value: string) {
    const digits = value.replace(/\D/g, "").slice(0, 8);

    if (digits.length <= 5) {
      return digits;
    }

    return `${digits.slice(0, 5)}-${digits.slice(5)}`;
  }

  function formatCpf(value: string) {
    const digits = value.replace(/\D/g, "").slice(0, 11);

    if (digits.length <= 3) {
      return digits;
    }

    if (digits.length <= 6) {
      return `${digits.slice(0, 3)}.${digits.slice(3)}`;
    }

    if (digits.length <= 9) {
      return `${digits.slice(0, 3)}.${digits.slice(3, 6)}.${digits.slice(6)}`;
    }

    return `${digits.slice(0, 3)}.${digits.slice(3, 6)}.${digits.slice(6, 9)}-${digits.slice(9)}`;
  }

  function formatCnpj(value: string) {
    const digits = value.replace(/\D/g, "").slice(0, 14);

    if (digits.length <= 2) {
      return digits;
    }

    if (digits.length <= 5) {
      return `${digits.slice(0, 2)}.${digits.slice(2)}`;
    }

    if (digits.length <= 8) {
      return `${digits.slice(0, 2)}.${digits.slice(2, 5)}.${digits.slice(5)}`;
    }

    if (digits.length <= 12) {
      return `${digits.slice(0, 2)}.${digits.slice(2, 5)}.${digits.slice(5, 8)}/${digits.slice(8)}`;
    }

    return `${digits.slice(0, 2)}.${digits.slice(2, 5)}.${digits.slice(5, 8)}/${digits.slice(8, 12)}-${digits.slice(12)}`;
  }

  function normalizeUf(stateCode?: string, stateName?: string) {
    if (stateCode?.trim()) {
      return stateCode.trim().toUpperCase().slice(0, 2);
    }

    if (!stateName?.trim()) {
      return "";
    }

    const normalized = stateName
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase()
      .trim();

    const stateMap: Record<string, string> = {
      acre: "AC",
      alagoas: "AL",
      amapa: "AP",
      amazonas: "AM",
      bahia: "BA",
      ceara: "CE",
      distrito_federal: "DF",
      espirito_santo: "ES",
      goias: "GO",
      maranhao: "MA",
      mato_grosso: "MT",
      mato_grosso_do_sul: "MS",
      minas_gerais: "MG",
      para: "PA",
      paraiba: "PB",
      parana: "PR",
      pernambuco: "PE",
      piaui: "PI",
      rio_de_janeiro: "RJ",
      rio_grande_do_norte: "RN",
      rio_grande_do_sul: "RS",
      rondonia: "RO",
      roraima: "RR",
      santa_catarina: "SC",
      sao_paulo: "SP",
      sergipe: "SE",
      tocantins: "TO"
    };

    return stateMap[normalized.replace(/\s+/g, "_")] ?? "";
  }

  async function lookupPostalCode(postalCode: string, target: "school" | "owner") {
    const digits = postalCode.replace(/\D/g, "");
    if (digits.length !== 8) {
      return;
    }

    if (target === "school") {
      setIsLookingUpSchoolPostalCode(true);
      setSchoolPostalCodeFeedback(null);
    } else {
      setIsLookingUpOwnerPostalCode(true);
      setOwnerPostalCodeFeedback(null);
    }

    try {
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
        if (target === "school") {
          setSchoolPostalCodeFeedback("CEP da escola não encontrado.");
        } else {
          setOwnerPostalCodeFeedback("CEP do proprietário não encontrado.");
        }
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

      if (target === "school") {
        setSchoolPostalCodeFeedback("Endereço da escola preenchido automaticamente pelo CEP.");
      } else {
        setOwnerPostalCodeFeedback("Endereço do proprietário preenchido automaticamente pelo CEP.");
      }
    } catch {
      if (target === "school") {
        setSchoolPostalCodeFeedback("Não foi possível consultar o CEP da escola agora.");
      } else {
        setOwnerPostalCodeFeedback("Não foi possível consultar o CEP do proprietário agora.");
      }
    } finally {
      if (target === "school") {
        setIsLookingUpSchoolPostalCode(false);
      } else {
        setIsLookingUpOwnerPostalCode(false);
      }
    }
  }

  async function lookupAddressByCoordinates(latitude: number, longitude: number) {
    try {
      setIsLookingUpSchoolCoordinates(true);
      setSchoolCoordinatesFeedback(null);

      const response = await fetch(
        `https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=${latitude}&lon=${longitude}&zoom=18&addressdetails=1`
      );

      if (!response.ok) {
        throw new Error("reverse-geocode-failed");
      }

      const data = (await response.json()) as {
        address?: {
          postcode?: string;
          road?: string;
          pedestrian?: string;
          footway?: string;
          neighbourhood?: string;
          suburb?: string;
          city?: string;
          town?: string;
          village?: string;
          municipality?: string;
          state?: string;
          state_code?: string;
        };
      };

      const address = data.address;
      if (!address) {
        setSchoolCoordinatesFeedback("Não foi possível determinar o endereço da escola pelo mapa.");
        return;
      }

      setForm((current) => ({
        ...current,
        postalCode: current.postalCode || formatPostalCode(address.postcode ?? ""),
        street: current.street || address.road || address.pedestrian || address.footway || "",
        neighborhood: current.neighborhood || address.neighbourhood || address.suburb || "",
        city: current.city || address.city || address.town || address.village || address.municipality || "",
        state: current.state || normalizeUf(address.state_code, address.state)
      }));
      setSchoolCoordinatesFeedback("Endereço da escola estimado automaticamente a partir do ponto no mapa.");
    } catch {
      setSchoolCoordinatesFeedback("Não foi possível estimar o endereço da escola pelo mapa agora.");
    } finally {
      setIsLookingUpSchoolCoordinates(false);
    }
  }

  return (
    <div className="space-y-6">
      <PageHero
        eyebrow="Administração do sistema"
        title="Cadastre novas escolas da plataforma em uma área central, independente de tenant."
        description="Toda nova escola nasce aqui. O sistema gera uma senha temporária para o proprietário e exige a troca no primeiro acesso."
        stats={[
          { label: "Provisionamento", value: "Central" },
          { label: "Senha inicial", value: "Automática" },
          { label: "Troca obrigatória", value: "Ativa" },
          { label: "Tenant", value: "Isolado" }
        ]}
      />

      {isLoading ? <LoadingBlock label="Carregando administração central" /> : null}
      {error ? <ErrorBlock message={error} /> : null}
      {successMessage ? (
        <div className="rounded-[24px] border border-[var(--q-success)] bg-[var(--q-success-bg)] px-5 py-4 text-sm text-[var(--q-text)]">
          {successMessage}
        </div>
      ) : null}

      <div className="w-full">
        <GlassCard
          title="Nova escola"
          description="Cadastre a escola, a praia base e os dados completos do proprietário inicial."
        >
          <form className="space-y-6" onSubmit={handleSubmit}>
            <section className="space-y-4">
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-semibold uppercase tracking-[0.16em] text-[var(--q-muted)]">
                  Dados da escola
                </h3>
              </div>
              <div className="grid gap-4 md:grid-cols-2">
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Razão social</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.legalName}
                    onChange={(event) => setForm((current) => ({ ...current, legalName: event.target.value }))}
                    placeholder="Nome jurídico da escola"
                    required
                  />
                </label>
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Nome de exibição</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.displayName}
                    onChange={(event) => setForm((current) => ({ ...current, displayName: event.target.value }))}
                    placeholder="Nome que aparecerá na plataforma"
                    required
                  />
                </label>
              </div>
              <div className="grid gap-4 md:grid-cols-[1fr_0.8fr]">
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>CNPJ da escola</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.cnpj ?? ""}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, cnpj: formatCnpj(event.target.value) }))
                    }
                    placeholder="Opcional"
                  />
                </label>
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Praia base da escola</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.baseBeachName}
                    onChange={(event) => setForm((current) => ({ ...current, baseBeachName: event.target.value }))}
                    placeholder="Praia onde a escola opera"
                    required
                  />
                </label>
              </div>
            </section>

            <section className="space-y-4">
              <h3 className="text-sm font-semibold uppercase tracking-[0.16em] text-[var(--q-muted)]">
                Endereço da escola
              </h3>
              <div className="grid gap-4 md:grid-cols-[0.42fr_1fr_0.34fr]">
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>CEP</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.postalCode}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, postalCode: formatPostalCode(event.target.value) }))
                    }
                    onBlur={() => void lookupPostalCode(form.postalCode, "school")}
                    placeholder="00000-000"
                    required
                  />
                </label>
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Logradouro</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.street}
                    onChange={(event) => setForm((current) => ({ ...current, street: event.target.value }))}
                    placeholder="Rua, avenida ou acesso"
                    required
                  />
                </label>
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Número</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.streetNumber}
                    onChange={(event) => setForm((current) => ({ ...current, streetNumber: event.target.value }))}
                    placeholder="Número"
                    required
                  />
                </label>
              </div>
              {schoolPostalCodeFeedback ? (
                <div className="text-xs text-[var(--q-text-2)]">
                  {isLookingUpSchoolPostalCode ? "Consultando CEP da escola..." : schoolPostalCodeFeedback}
                </div>
              ) : null}
              <div className="grid gap-4 md:grid-cols-[1fr_1fr]">
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Complemento</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.addressComplement ?? ""}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, addressComplement: event.target.value }))
                    }
                    placeholder="Opcional"
                  />
                </label>
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Bairro</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.neighborhood}
                    onChange={(event) => setForm((current) => ({ ...current, neighborhood: event.target.value }))}
                    placeholder="Bairro"
                    required
                  />
                </label>
              </div>
              <div className="grid gap-4 md:grid-cols-[0.86fr_120px]">
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Cidade</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.city}
                    onChange={(event) => setForm((current) => ({ ...current, city: event.target.value }))}
                    placeholder="Cidade"
                    required
                  />
                </label>
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Estado</span>
                  <input
                    className="w-full rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-3 py-3 text-center uppercase outline-none"
                    value={form.state}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, state: event.target.value.toUpperCase().slice(0, 2) }))
                    }
                    placeholder="UF"
                    maxLength={2}
                    required
                  />
                </label>
              </div>
            </section>

            <section className="space-y-4">
              <h3 className="text-sm font-semibold uppercase tracking-[0.16em] text-[var(--q-muted)]">
                Localização da base no mapa
              </h3>
              <div className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text-2)]">
                Se a escola ou a praia base não tiver CEP confiável, marque o ponto diretamente no mapa.
              </div>
              <SchoolBaseMapPicker
                value={{
                  latitude: form.baseLatitude,
                  longitude: form.baseLongitude
                }}
                onChange={(coordinates) => {
                  setForm((current) => ({
                    ...current,
                    baseLatitude: coordinates.latitude,
                    baseLongitude: coordinates.longitude
                  }));

                  if (
                    typeof coordinates.latitude === "number" &&
                    typeof coordinates.longitude === "number" &&
                    !form.postalCode.trim()
                  ) {
                    void lookupAddressByCoordinates(coordinates.latitude, coordinates.longitude);
                    return;
                  }

                  setSchoolCoordinatesFeedback(null);
                }}
              />
              {schoolCoordinatesFeedback ? (
                <div className="text-xs text-[var(--q-text-2)]">
                  {isLookingUpSchoolCoordinates
                    ? "Estimando endereço da escola pelo ponto selecionado no mapa..."
                    : schoolCoordinatesFeedback}
                </div>
              ) : null}
            </section>

            <section className="space-y-4">
              <h3 className="text-sm font-semibold uppercase tracking-[0.16em] text-[var(--q-muted)]">
                Proprietário inicial
              </h3>
              <div className="grid gap-4 md:grid-cols-2">
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Nome completo</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.ownerFullName}
                    onChange={(event) => setForm((current) => ({ ...current, ownerFullName: event.target.value }))}
                    placeholder="Responsável inicial da escola"
                    required
                  />
                </label>
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>E-mail</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    type="email"
                    value={form.ownerEmail}
                    onChange={(event) => setForm((current) => ({ ...current, ownerEmail: event.target.value }))}
                    placeholder="Login inicial do proprietário"
                    required
                  />
                </label>
              </div>
              <div className="grid gap-4 md:grid-cols-[0.9fr_1fr]">
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>CPF</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.ownerCpf}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, ownerCpf: formatCpf(event.target.value) }))
                    }
                    placeholder="000.000.000-00"
                    required
                  />
                </label>
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Telefone</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.ownerPhone ?? ""}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, ownerPhone: formatPhone(event.target.value) }))
                    }
                    placeholder="(00) 00000-0000"
                  />
                </label>
              </div>
              <div className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Senha inicial</span>
                <div className="rounded-2xl border border-dashed border-[var(--q-divider)] bg-[var(--q-surface-2)] px-4 py-3 text-[var(--q-text-2)]">
                  Gerada automaticamente e enviada ao proprietário por e-mail.
                </div>
              </div>
            </section>

            <section className="space-y-4">
              <h3 className="text-sm font-semibold uppercase tracking-[0.16em] text-[var(--q-muted)]">
                Endereço do proprietário
              </h3>
              <div className="grid gap-4 md:grid-cols-[0.42fr_1fr_0.34fr]">
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>CEP</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.ownerPostalCode}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, ownerPostalCode: formatPostalCode(event.target.value) }))
                    }
                    onBlur={() => void lookupPostalCode(form.ownerPostalCode, "owner")}
                    placeholder="00000-000"
                    required
                  />
                </label>
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Logradouro</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.ownerStreet}
                    onChange={(event) => setForm((current) => ({ ...current, ownerStreet: event.target.value }))}
                    placeholder="Rua, avenida ou acesso"
                    required
                  />
                </label>
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Número</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.ownerStreetNumber}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, ownerStreetNumber: event.target.value }))
                    }
                    placeholder="Número"
                    required
                  />
                </label>
              </div>
              {ownerPostalCodeFeedback ? (
                <div className="text-xs text-[var(--q-text-2)]">
                  {isLookingUpOwnerPostalCode ? "Consultando CEP do proprietário..." : ownerPostalCodeFeedback}
                </div>
              ) : null}
              <div className="grid gap-4 md:grid-cols-[1fr_1fr]">
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Complemento</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.ownerAddressComplement ?? ""}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, ownerAddressComplement: event.target.value }))
                    }
                    placeholder="Opcional"
                  />
                </label>
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Bairro</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.ownerNeighborhood}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, ownerNeighborhood: event.target.value }))
                    }
                    placeholder="Bairro"
                    required
                  />
                </label>
              </div>
              <div className="grid gap-4 md:grid-cols-[0.86fr_120px]">
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Cidade</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.ownerCity}
                    onChange={(event) => setForm((current) => ({ ...current, ownerCity: event.target.value }))}
                    placeholder="Cidade"
                    required
                  />
                </label>
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Estado</span>
                  <input
                    className="w-full rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-3 py-3 text-center uppercase outline-none"
                    value={form.ownerState}
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        ownerState: event.target.value.toUpperCase().slice(0, 2)
                      }))
                    }
                    placeholder="UF"
                    maxLength={2}
                    required
                  />
                </label>
              </div>
            </section>

            <section className="space-y-4">
              <h3 className="text-sm font-semibold uppercase tracking-[0.16em] text-[var(--q-muted)]">
                Configurações iniciais
              </h3>
              <div className="grid gap-4 md:grid-cols-2">
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Fuso horário</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none"
                    value={form.timezone ?? ""}
                    onChange={(event) => setForm((current) => ({ ...current, timezone: event.target.value }))}
                    placeholder="America/Sao_Paulo"
                  />
                </label>
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Moeda</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 uppercase outline-none"
                    value={form.currencyCode ?? ""}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, currencyCode: event.target.value.toUpperCase() }))
                    }
                    placeholder="BRL"
                  />
                </label>
              </div>
              <div className="grid gap-4 md:grid-cols-[1fr_180px]">
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Logo da escola</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 outline-none file:mr-3 file:rounded-full file:border-0 file:bg-[var(--q-info-bg)] file:px-3 file:py-2 file:text-sm file:text-[var(--q-text)]"
                    type="file"
                    accept="image/png,image/jpeg,image/webp,image/svg+xml"
                    onChange={(event) => void handleLogoChange(event)}
                  />
                </label>
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
            </section>

            <button
              type="submit"
              disabled={isSaving}
              style={{ backgroundImage: "var(--q-grad-brand)" }}
              className="rounded-2xl bg-[var(--q-navy)] px-5 py-3 text-sm font-medium text-white shadow-[0_18px_40px_rgba(36,75,132,0.18)] transition hover:opacity-95 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isSaving ? "Cadastrando escola..." : "Cadastrar escola"}
            </button>
          </form>
        </GlassCard>
      </div>
    </div>
  );
}

function readFileAsDataUrl(file: File) {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result ?? ""));
    reader.onerror = () => reject(new Error("Não foi possível carregar a imagem da logo."));
    reader.readAsDataURL(file);
  });
}
