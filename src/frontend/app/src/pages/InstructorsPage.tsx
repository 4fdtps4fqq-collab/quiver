import { useEffect, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock, StatusBadge } from "../components/OperationsUi";
import { formatCurrency } from "../lib/formatters";
import { createInstructor, getInstructors, type Instructor } from "../lib/platform-api";

const initialForm = {
  fullName: "",
  email: "",
  phone: "",
  specialties: "",
  hourlyRate: ""
};

export function InstructorsPage() {
  const { token } = useSession();
  const [instructors, setInstructors] = useState<Instructor[]>([]);
  const [form, setForm] = useState(initialForm);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      return;
    }

    void loadInstructors(token);
  }, [token]);

  async function loadInstructors(currentToken: string) {
    try {
      setIsLoading(true);
      setError(null);
      setInstructors(await getInstructors(currentToken));
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar os instrutores.");
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
      await createInstructor(token, {
        fullName: form.fullName,
        email: form.email || undefined,
        phone: form.phone || undefined,
        specialties: form.specialties || undefined,
        hourlyRate: parseCurrencyInput(form.hourlyRate),
        identityUserId: null
      });
      setForm(initialForm);
      await loadInstructors(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar o instrutor.");
    } finally {
      setIsSaving(false);
    }
  }

  const activeInstructors = instructors.filter((item) => item.isActive);
  const withPhone = instructors.filter((item) => item.phone).length;
  const averageHourlyRate =
    instructors.length === 0
      ? 0
      : instructors.reduce((sum, item) => sum + item.hourlyRate, 0) / instructors.length;

  return (
    <div className="space-y-6">
      <PageHero
        eyebrow="Instrutores"
        title="Base de instrutores preparada para escala, especialidade e performance."
        description="A nova camada operacional conecta os dados básicos do instrutor à leitura de agenda e à capacidade da escola."
        stats={[
          { label: "Ativos", value: String(activeInstructors.length) },
          { label: "Total", value: String(instructors.length) },
          { label: "Com telefone", value: String(withPhone) },
          { label: "Hora média", value: formatCurrency(averageHourlyRate) }
        ]}
      />

      {isLoading ? <LoadingBlock label="Carregando instrutores" /> : null}
      {error ? <ErrorBlock message={error} /> : null}

      <div className="grid gap-4 xl:grid-cols-[0.85fr_1.15fr]">
        <GlassCard title="Novo instrutor" description="Cadastro direto da operação com dados de contato, especialidade principal e valor da hora/aula.">
          <form className="grid gap-3" onSubmit={handleSubmit}>
            <input
              className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
              placeholder="Nome completo"
              value={form.fullName}
              onChange={(event) => setForm((current) => ({ ...current, fullName: event.target.value }))}
              required
            />
            <input
              className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
              placeholder="E-mail"
              type="email"
              value={form.email}
              onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
            />
            <input
              className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
              placeholder="Telefone"
              value={form.phone}
              onChange={(event) => setForm((current) => ({ ...current, phone: event.target.value }))}
            />
            <input
              className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
              placeholder="Valor da hora/aula"
              value={form.hourlyRate}
              onChange={(event) => setForm((current) => ({ ...current, hourlyRate: event.target.value }))}
              inputMode="decimal"
              required
            />
            <textarea
              className="min-h-28 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
              placeholder="Especialidades, níveis ou observações"
              value={form.specialties}
              onChange={(event) => setForm((current) => ({ ...current, specialties: event.target.value }))}
            />
            <button
              className="rounded-full border border-transparent bg-[var(--q-grad-brand)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95"
              type="submit"
              disabled={isSaving}
            >
              {isSaving ? "Salvando" : "Cadastrar instrutor"}
            </button>
          </form>
        </GlassCard>

        <GlassCard title="Equipe ativa" description="Leitura rápida para apoiar agenda, alocação de aulas e visão de capacidade.">
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
              <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                <tr>
                  <th className="pb-3">Instrutor</th>
                  <th className="pb-3">Contato</th>
                  <th className="pb-3">Especialidades</th>
                  <th className="pb-3">Hora/aula</th>
                  <th className="pb-3">Status</th>
                </tr>
              </thead>
              <tbody>
                {instructors.map((instructor) => (
                  <tr key={instructor.id} className="border-t border-[var(--q-border)]">
                    <td className="py-3 font-medium text-[var(--q-text)]">{instructor.fullName}</td>
                    <td className="py-3 text-[var(--q-text-2)]">
                      <div>{instructor.email || "Sem e-mail"}</div>
                      <div className="mt-1 text-xs text-[var(--q-muted)]">{instructor.phone || "Sem telefone"}</div>
                    </td>
                    <td className="py-3">{instructor.specialties || "Generalista"}</td>
                    <td className="py-3 font-medium text-[var(--q-text)]">{formatCurrency(instructor.hourlyRate)}</td>
                    <td className="py-3">
                      <StatusBadge value={instructor.isActive ? "Active" : "Inactive"} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </GlassCard>
      </div>
    </div>
  );
}

function parseCurrencyInput(value: string) {
  const normalized = value.replace(/\./g, "").replace(",", ".").trim();
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : 0;
}
