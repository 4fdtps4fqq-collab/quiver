import { useMemo, useState } from "react";
import { GlassCard, StatusBadge } from "./OperationsUi";
import { formatCurrency } from "../lib/formatters";
import type { Instructor } from "../lib/platform-api";

const initialInstructorForm = {
  id: "",
  fullName: "",
  email: "",
  phone: "",
  specialties: "",
  hourlyRate: "",
  isActive: true
};

type InstructorFormState = typeof initialInstructorForm;

type SchoolInstructorManagementProps = {
  instructors: Instructor[];
  isSaving: boolean;
  onCreate: (payload: {
    fullName: string;
    email?: string;
    phone?: string;
    specialties?: string;
    hourlyRate: number;
  }) => Promise<void>;
  onUpdate: (
    instructorId: string,
    payload: {
      fullName: string;
      email?: string;
      phone?: string;
      specialties?: string;
      hourlyRate: number;
      isActive: boolean;
    }
  ) => Promise<void>;
};

export function SchoolInstructorManagement({
  instructors,
  isSaving,
  onCreate,
  onUpdate
}: SchoolInstructorManagementProps) {
  const [search, setSearch] = useState("");
  const [form, setForm] = useState<InstructorFormState>(initialInstructorForm);

  const filteredInstructors = useMemo(() => {
    const normalized = normalizeText(search);
    if (!normalized) {
      return instructors;
    }

    return instructors.filter((item) => {
      const fullText = `${item.fullName} ${item.email ?? ""} ${item.phone ?? ""} ${item.specialties ?? ""}`;
      return normalizeText(fullText).includes(normalized);
    });
  }, [instructors, search]);

  const activeInstructors = instructors.filter((item) => item.isActive).length;
  const withPhone = instructors.filter((item) => item.phone).length;
  const averageHourlyRate =
    instructors.length === 0
      ? 0
      : instructors.reduce((sum, item) => sum + item.hourlyRate, 0) / instructors.length;

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const payload = {
      fullName: form.fullName.trim(),
      email: normalizeOptional(form.email),
      phone: normalizeOptional(form.phone),
      specialties: normalizeOptional(form.specialties),
      hourlyRate: parseCurrencyInput(form.hourlyRate),
      isActive: form.isActive
    };

    if (!payload.fullName) {
      return;
    }

    if (form.id) {
      await onUpdate(form.id, payload);
    } else {
      await onCreate(payload);
    }

    clearForm();
  }

  function loadInstructor(instructor: Instructor) {
    setForm({
      id: instructor.id,
      fullName: instructor.fullName,
      email: instructor.email ?? "",
      phone: instructor.phone ?? "",
      specialties: instructor.specialties ?? "",
      hourlyRate: formatCurrencyInput(instructor.hourlyRate),
      isActive: instructor.isActive
    });
  }

  function clearForm() {
    setForm(initialInstructorForm);
  }

  return (
    <div className="grid gap-4 xl:grid-cols-[0.92fr_1.08fr]">
      <GlassCard
        title="Ficha do instrutor"
        description="Cadastre ou atualize instrutores da escola com contato, especialidades e valor da hora/aula."
      >
        <form className="space-y-6" onSubmit={handleSubmit}>
          <div className="grid gap-6 md:grid-cols-2">
            <label className="grid gap-2 text-sm text-[var(--q-text)] md:col-span-2">
              <span>Nome completo</span>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Nome do instrutor"
                value={form.fullName}
                onChange={(event) => setForm((current) => ({ ...current, fullName: event.target.value }))}
                required
              />
            </label>

            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>E-mail</span>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="E-mail de contato"
                type="email"
                value={form.email}
                onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
              />
            </label>

            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Telefone</span>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Telefone do instrutor"
                value={form.phone}
                onChange={(event) => setForm((current) => ({ ...current, phone: event.target.value }))}
              />
            </label>

            <label className="grid gap-2 text-sm text-[var(--q-text)] md:col-span-2">
              <span>Especialidades</span>
              <textarea
                className="min-h-28 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Habilidades, modalidades ou observações operacionais"
                value={form.specialties}
                onChange={(event) => setForm((current) => ({ ...current, specialties: event.target.value }))}
              />
            </label>

            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Valor da hora/aula</span>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Ex.: 180,00"
                value={form.hourlyRate}
                onChange={(event) => setForm((current) => ({ ...current, hourlyRate: event.target.value }))}
                inputMode="decimal"
                required
              />
            </label>

            {form.id ? (
              <label className="flex items-center gap-3 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)]">
                <input
                  checked={form.isActive}
                  onChange={(event) => setForm((current) => ({ ...current, isActive: event.target.checked }))}
                  type="checkbox"
                />
                Instrutor ativo
              </label>
            ) : (
              <div className="rounded-2xl border border-dashed border-[var(--q-divider)] px-4 py-3 text-sm text-[var(--q-text-2)]">
                O cadastro inicial entra como ativo.
              </div>
            )}
          </div>

          <div className="flex flex-wrap gap-3">
            <button
              className="rounded-full border border-transparent px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95"
              style={{
                backgroundImage: "var(--q-grad-brand)",
                backgroundColor: "var(--q-navy)",
                boxShadow: "0 18px 32px rgba(11, 60, 93, 0.18)"
              }}
              type="submit"
              disabled={isSaving}
            >
              {isSaving ? "Salvando" : form.id ? "Atualizar" : "Salvar"}
            </button>
            <button
              className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface)] px-5 py-3 text-sm font-medium uppercase tracking-[0.2em] text-[var(--q-text)] transition hover:bg-[var(--q-surface-2)]"
              type="button"
              onClick={clearForm}
            >
              Limpar
            </button>
          </div>
        </form>
      </GlassCard>

      <GlassCard
        title="Base de instrutores"
        description="Consulte a equipe da escola, encontre rapidamente um instrutor e abra a ficha para edição."
      >
        <div className="space-y-5">
          <div className="grid gap-3 md:grid-cols-4">
            <MetricCard label="Ativos" value={String(activeInstructors)} />
            <MetricCard label="Total" value={String(instructors.length)} />
            <MetricCard label="Com telefone" value={String(withPhone)} />
            <MetricCard label="Hora média" value={formatCurrency(averageHourlyRate)} />
          </div>

          <label className="grid gap-2 text-sm text-[var(--q-text)]">
            <span>Buscar instrutor</span>
            <input
              className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
              placeholder="Digite nome, e-mail, telefone ou especialidade"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </label>

          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
              <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                <tr>
                  <th className="pb-3">Instrutor</th>
                  <th className="pb-3">Contato</th>
                  <th className="pb-3">Hora/aula</th>
                  <th className="pb-3">Status</th>
                  <th className="pb-3">Ação</th>
                </tr>
              </thead>
              <tbody>
                {filteredInstructors.map((instructor) => (
                  <tr key={instructor.id} className="border-t border-[var(--q-border)] align-middle">
                    <td className="py-4 font-medium text-[var(--q-text)]">{instructor.fullName}</td>
                    <td className="py-4">
                      <div className="text-[var(--q-text)]">{instructor.phone || "Sem telefone"}</div>
                      <div className="mt-1 text-xs text-[var(--q-muted)]">{instructor.email || "Sem e-mail"}</div>
                    </td>
                    <td className="py-4 font-medium text-[var(--q-text)]">{formatCurrency(instructor.hourlyRate)}</td>
                    <td className="py-4">
                      <StatusBadge value={instructor.isActive ? "Active" : "Inactive"} />
                    </td>
                    <td className="py-4">
                      <button
                        className="rounded-full border border-[var(--q-info)]/25 bg-[var(--q-info-bg)] px-4 py-2 text-sm font-medium text-[var(--q-info)] transition hover:opacity-90"
                        type="button"
                        onClick={() => loadInstructor(instructor)}
                      >
                        Editar
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {filteredInstructors.length === 0 ? (
            <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
              Nenhum instrutor encontrado com esse filtro.
            </div>
          ) : null}
        </div>
      </GlassCard>
    </div>
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

function normalizeOptional(value: string) {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : undefined;
}

function normalizeText(value: string) {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .trim();
}

function parseCurrencyInput(value: string) {
  const normalized = value.replace(/\./g, "").replace(",", ".").trim();
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : 0;
}

function formatCurrencyInput(value: number) {
  return value.toFixed(2).replace(".", ",");
}
