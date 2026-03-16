import { useEffect, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock } from "../components/OperationsUi";
import {
  createSchoolInvitation,
  createStudent,
  getStudentFinancialStatuses,
  getStudents,
  updateStudent,
  type Student,
  type StudentFinancialStatusSummary
} from "../lib/platform-api";
import { translateLabel } from "../lib/localization";

const initialForm = {
  fullName: "",
  email: "",
  phone: "",
  postalCode: "",
  street: "",
  streetNumber: "",
  addressComplement: "",
  neighborhood: "",
  city: "",
  state: "",
  birthDate: "",
  medicalNotes: "",
  emergencyContactName: "",
  emergencyContactPhone: "",
  isActive: true
};

export function StudentsPage() {
  const { token, school } = useSession();
  const [students, setStudents] = useState<Student[]>([]);
  const [financialStatuses, setFinancialStatuses] = useState<Record<string, StudentFinancialStatusSummary>>({});
  const [delinquentStudents, setDelinquentStudents] = useState(0);
  const [form, setForm] = useState(initialForm);
  const [editingStudentId, setEditingStudentId] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [isLookingUpPostalCode, setIsLookingUpPostalCode] = useState(false);
  const [postalCodeFeedback, setPostalCodeFeedback] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      return;
    }

    void loadStudents(token);
  }, [token]);

  async function loadStudents(currentToken: string) {
    try {
      setIsLoading(true);
      setError(null);
      setNotice(null);
      const [studentsResult, statusesResult] = await Promise.allSettled([
        getStudents(currentToken),
        getStudentFinancialStatuses(currentToken)
      ]);

      if (studentsResult.status === "rejected") {
        throw studentsResult.reason;
      }

      setStudents(studentsResult.value);

      if (statusesResult.status === "fulfilled") {
        setFinancialStatuses(
          Object.fromEntries(statusesResult.value.items.map((item) => [item.studentId, item]))
        );
        setDelinquentStudents(statusesResult.value.delinquentStudents);
      } else {
        setFinancialStatuses({});
        setDelinquentStudents(0);
      }
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar os alunos.");
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
      setNotice(null);

      const payload = {
        fullName: form.fullName,
        email: form.email || undefined,
        phone: form.phone || undefined,
        postalCode: form.postalCode || undefined,
        street: form.street || undefined,
        streetNumber: form.streetNumber || undefined,
        addressComplement: form.addressComplement || undefined,
        neighborhood: form.neighborhood || undefined,
        city: form.city || undefined,
        state: form.state || undefined,
        birthDate: form.birthDate || null,
        medicalNotes: form.medicalNotes || undefined,
        emergencyContactName: form.emergencyContactName || undefined,
        emergencyContactPhone: form.emergencyContactPhone || undefined
      };

      if (editingStudentId) {
        const currentStudent = students.find((item) => item.id === editingStudentId);
        await updateStudent(token, editingStudentId, {
          ...payload,
          identityUserId: currentStudent?.identityUserId ?? null,
          isActive: form.isActive
        });
      } else {
        await createStudent(token, payload);
      }

      setForm(initialForm);
      setEditingStudentId(null);
      setPostalCodeFeedback(null);
      await loadStudents(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar o aluno.");
    } finally {
      setIsSaving(false);
    }
  }

  const activeStudents = students.filter((item) => item.isActive);
  const inactiveStudents = students.filter((item) => !item.isActive);
  const linkedPortalUsers = students.filter((item) => item.identityUserId).length;
  const studentsWithAddress = students.filter((item) => item.city || item.postalCode).length;
  const averageProgress =
    students.length === 0
      ? 0
      : students.reduce((sum, item) => sum + (item.progressPercent ?? 0), 0) / students.length;
  const totalUpcomingLessons = students.reduce((sum, item) => sum + (item.upcomingLessons ?? 0), 0);
  const normalizedSearch = search.trim().toLowerCase();
  const filteredStudents = normalizedSearch
    ? students.filter((student) => {
      const fullName = student.fullName.toLowerCase();
      const phone = (student.phone ?? "").toLowerCase();
      return fullName.includes(normalizedSearch) || phone.includes(normalizedSearch);
    })
    : students;
  const selectedStudent = editingStudentId
    ? students.find((item) => item.id === editingStudentId) ?? null
    : null;

  function handleEdit(student: Student) {
    setEditingStudentId(student.id);
    setForm({
      fullName: student.fullName ?? "",
      email: student.email ?? "",
      phone: student.phone ?? "",
      postalCode: student.postalCode ?? "",
      street: student.street ?? "",
      streetNumber: student.streetNumber ?? "",
      addressComplement: student.addressComplement ?? "",
      neighborhood: student.neighborhood ?? "",
      city: student.city ?? "",
      state: student.state ?? "",
      birthDate: student.birthDate ?? "",
      medicalNotes: student.medicalNotes ?? "",
      emergencyContactName: student.emergencyContactName ?? "",
      emergencyContactPhone: student.emergencyContactPhone ?? "",
      isActive: student.isActive
    });
    setPostalCodeFeedback(null);
  }

  function handleCancelEdit() {
    setEditingStudentId(null);
    setForm(initialForm);
    setPostalCodeFeedback(null);
    setNotice(null);
  }

  async function handleSendPortalInvite() {
    if (!token || !selectedStudent || !selectedStudent.email) {
      return;
    }

    try {
      setError(null);
      setNotice(null);
      const invitation = await createSchoolInvitation(token, {
        fullName: selectedStudent.fullName,
        email: selectedStudent.email,
        phone: selectedStudent.phone || undefined,
        role: 4,
        expiresInDays: 7,
        schoolDisplayName: school?.displayName,
        schoolSlug: school?.slug
      });

      setNotice(
        invitation.deliveryMode === "File" && invitation.outboxFilePath
          ? `Acesso do portal gerado e salvo em ${invitation.outboxFilePath}.`
          : "Convite de acesso ao portal enviado com sucesso."
      );
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível enviar o acesso ao portal.");
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

  async function lookupPostalCode(rawPostalCode: string) {
    const digits = rawPostalCode.replace(/\D/g, "");

    if (digits.length !== 8) {
      return;
    }

    try {
      setIsLookingUpPostalCode(true);
      setPostalCodeFeedback(null);

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
        setPostalCodeFeedback("CEP não encontrado.");
        return;
      }

      setForm((current) => ({
        ...current,
        street: data.logradouro?.trim() || current.street,
        addressComplement: data.complemento?.trim() || current.addressComplement,
        neighborhood: data.bairro?.trim() || current.neighborhood,
        city: data.localidade?.trim() || current.city,
        state: data.uf?.trim().toUpperCase() || current.state
      }));
      setPostalCodeFeedback("Endereço preenchido automaticamente pelo CEP.");
    } catch {
      setPostalCodeFeedback("Não foi possível consultar o CEP agora.");
    } finally {
      setIsLookingUpPostalCode(false);
    }
  }

  return (
    <div className="space-y-6">
      <PageHero
        title="Visão Geral de Alunos"
        statsBelow
        stats={[
          { label: "Ativos", value: String(activeStudents.length) },
          { label: "Inativos", value: String(inactiveStudents.length) },
          { label: "Total", value: String(students.length) },
          { label: "Inadimplentes", value: String(delinquentStudents) },
          { label: "Próximas aulas", value: String(totalUpcomingLessons) },
          { label: "Progresso médio", value: `${Math.round(averageProgress)}%` },
          { label: "Endereço", value: String(studentsWithAddress) },
          { label: "Portal", value: String(linkedPortalUsers) }
        ]}
      />

      {isLoading ? <LoadingBlock label="Carregando alunos" /> : null}
      {error ? <ErrorBlock message={error} /> : null}
      {notice ? (
        <div className="rounded-[24px] border border-[var(--q-info)]/30 bg-[var(--q-info-bg)] px-5 py-4 text-sm text-[var(--q-info)]">
          {notice}
        </div>
      ) : null}

      <div className="grid gap-4 xl:grid-cols-[1.08fr_0.72fr]">
        <GlassCard
          title="Ficha do aluno"
          description={editingStudentId
            ? "Ajuste os dados principais do aluno selecionado."
            : undefined}
        >
          <form className="space-y-6" onSubmit={handleSubmit}>
            {selectedStudent ? (
              <div className="grid gap-3 md:grid-cols-4">
                <MetricCard label="Matrículas ativas" value={String(selectedStudent.activeEnrollments ?? 0)} />
                <MetricCard label="Aulas realizadas" value={String(selectedStudent.realizedLessons ?? 0)} />
                <MetricCard label="Próximas aulas" value={String(selectedStudent.upcomingLessons ?? 0)} />
                <MetricCard label="Progresso" value={`${Math.round(selectedStudent.progressPercent ?? 0)}%`} />
              </div>
            ) : null}

            {editingStudentId && financialStatuses[editingStudentId]?.status === "Delinquent" ? (
              <div className="rounded-[22px] border border-[var(--q-danger)]/30 bg-[var(--q-danger-bg)] px-4 py-4 text-sm text-[var(--q-danger)]">
                Este aluno possui cobrança em atraso. Revise a situação financeira antes de liberar novos agendamentos.
              </div>
            ) : null}
            <div className="grid gap-6 md:grid-cols-2">
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Nome completo do aluno</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)] outline-none"
                  placeholder="Ex.: Pedro Petersen"
                  value={form.fullName}
                  onChange={(event) => setForm((current) => ({ ...current, fullName: event.target.value }))}
                  required
                />
              </label>
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>E-mail principal</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)] outline-none"
                  placeholder="Usado para contato e futuro acesso ao portal"
                  type="email"
                  value={form.email}
                  onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
                />
              </label>
            </div>

            <div className="grid gap-6 md:grid-cols-2">
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Telefone</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)] outline-none"
                  placeholder="(00) 00000-0000"
                  value={form.phone}
                  onChange={(event) =>
                    setForm((current) => ({ ...current, phone: formatPhone(event.target.value) }))
                  }
                />
              </label>
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Data de nascimento</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)] outline-none"
                  type="date"
                  value={form.birthDate}
                  onChange={(event) => setForm((current) => ({ ...current, birthDate: event.target.value }))}
                />
              </label>
            </div>

            <div className="grid gap-6 md:grid-cols-[220px_minmax(0,1fr)]">
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>CEP</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)] outline-none"
                  placeholder="00000-000"
                  value={form.postalCode}
                  onBlur={(event) => void lookupPostalCode(event.target.value)}
                  onChange={(event) => {
                    setPostalCodeFeedback(null);
                    setForm((current) => ({
                      ...current,
                      postalCode: formatPostalCode(event.target.value)
                    }));
                  }}
                />
              </label>
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Logradouro</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)] outline-none"
                  placeholder="Rua, avenida ou estrada"
                  value={form.street}
                  onChange={(event) => setForm((current) => ({ ...current, street: event.target.value }))}
                />
              </label>
            </div>

            {postalCodeFeedback ? (
              <div
                className={`text-sm ${postalCodeFeedback.includes("automaticamente")
                  ? "text-[var(--q-success)]"
                  : "text-[var(--q-warning)]"
                  }`}
              >
                {isLookingUpPostalCode ? "Consultando CEP..." : postalCodeFeedback}
              </div>
            ) : isLookingUpPostalCode ? (
              <div className="text-sm text-[var(--q-info)]">Consultando CEP...</div>
            ) : null}

            <div className="grid gap-6 md:grid-cols-[220px_minmax(0,1fr)]">
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Número</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)] outline-none"
                  placeholder="Número"
                  value={form.streetNumber}
                  onChange={(event) => setForm((current) => ({ ...current, streetNumber: event.target.value }))}
                />
              </label>
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Complemento</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)] outline-none"
                  placeholder="Apartamento, bloco, referência ou ponto de apoio"
                  value={form.addressComplement}
                  onChange={(event) =>
                    setForm((current) => ({ ...current, addressComplement: event.target.value }))
                  }
                />
              </label>
            </div>

            <div className="grid gap-6 md:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Bairro</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)] outline-none"
                  placeholder="Bairro"
                  value={form.neighborhood}
                  onChange={(event) => setForm((current) => ({ ...current, neighborhood: event.target.value }))}
                />
              </label>
              <div className="grid gap-6 md:grid-cols-[minmax(0,1fr)_96px]">
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Cidade</span>
                  <input
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)] outline-none"
                    placeholder="Cidade do aluno"
                    value={form.city}
                    onChange={(event) => setForm((current) => ({ ...current, city: event.target.value }))}
                  />
                </label>
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Estado</span>
                  <input
                    className="w-full rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-3 py-3.5 text-center text-sm uppercase tracking-[0.08em] text-[var(--q-text)] outline-none"
                    placeholder="UF"
                    maxLength={2}
                    value={form.state}
                    onChange={(event) =>
                      setForm((current) => ({ ...current, state: event.target.value.toUpperCase() }))
                    }
                  />
                </label>
              </div>
            </div>

            <div className="grid gap-6 md:grid-cols-2">
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Contato para emergência</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)] outline-none"
                  placeholder="Quem devemos acionar em caso de incidente"
                  value={form.emergencyContactName}
                  onChange={(event) =>
                    setForm((current) => ({ ...current, emergencyContactName: event.target.value }))
                  }
                />
              </label>
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Telefone de emergência</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)] outline-none"
                  placeholder="(00) 00000-0000"
                  value={form.emergencyContactPhone}
                  onChange={(event) =>
                    setForm((current) => ({ ...current, emergencyContactPhone: formatPhone(event.target.value) }))
                  }
                />
              </label>
            </div>
            <label className="grid gap-2.5 text-sm text-[var(--q-text)] md:col-span-2">
              <span>Observações médicas e operacionais</span>
              <textarea
                className="min-h-32 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)] outline-none"
                placeholder="Ex.: restrições físicas, histórico importante ou orientações de atendimento"
                value={form.medicalNotes}
                onChange={(event) => setForm((current) => ({ ...current, medicalNotes: event.target.value }))}
              />
            </label>

            {editingStudentId ? (
              <label className="flex items-center gap-3 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)]">
                <input
                  checked={form.isActive}
                  onChange={(event) => setForm((current) => ({ ...current, isActive: event.target.checked }))}
                  type="checkbox"
                />
                Aluno ativo
              </label>
            ) : null}

            <div className="flex flex-wrap gap-3">
              <button
                className="mt-1 inline-flex items-center justify-center rounded-full border border-transparent px-5 py-3.5 text-sm font-medium uppercase tracking-[0.24em] text-white shadow-[0_16px_34px_rgba(18,84,135,0.18)] transition hover:opacity-95"
                style={{ backgroundImage: "var(--q-grad-brand)", backgroundColor: "var(--q-navy)" }}
                type="submit"
                disabled={isSaving}
              >
                {isSaving ? "Salvando" : "Salvar"}
              </button>
              <button
                className="mt-1 rounded-full border border-[var(--q-border)] bg-[var(--q-surface)] px-5 py-3.5 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-text)] transition hover:bg-[var(--q-surface-2)]"
                type="button"
                onClick={handleCancelEdit}
              >
                Limpar
              </button>
              {selectedStudent?.email ? (
                <button
                  className="mt-1 rounded-full border border-[var(--q-info)]/30 bg-[var(--q-info-bg)] px-5 py-3.5 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-info)] transition hover:opacity-90"
                  type="button"
                  onClick={() => void handleSendPortalInvite()}
                >
                  Enviar acesso ao portal
                </button>
              ) : null}
            </div>
          </form>
        </GlassCard>

        <GlassCard title="Base de alunos">
          <div className="mb-4">
            <input
              className="w-full rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)] outline-none"
              placeholder="Buscar por nome ou telefone"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </div>
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
              <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                <tr>
                  <th className="pb-4 pr-6">Aluno</th>
                  <th className="pb-4 pr-6">Telefone</th>
                  <th className="pb-4 pr-6">Status</th>
                  <th className="pb-4">Ação</th>
                </tr>
              </thead>
              <tbody>
                {filteredStudents.map((student) => (
                  <tr key={student.id} className="border-t border-[var(--q-border)]">
                    <td className="py-4 pr-6 align-middle">
                      <div className="font-medium text-[var(--q-text)]">{student.fullName}</div>
                    </td>
                    <td className="py-4 pr-6 align-middle">
                      <div className="font-medium text-[var(--q-text)]">
                        {student.phone || "Sem telefone"}
                      </div>
                    </td>
                    <td className="py-4 pr-6 align-middle">
                      <div className="space-y-1">
                        <div className="font-medium text-[var(--q-text)]">
                          {student.isActive ? "Ativo" : "Inativo"}
                        </div>
                        <div className={resolveFinancialTone(financialStatuses[student.id]?.status)}>
                          {translateLabel(financialStatuses[student.id]?.status ?? "UpToDate")}
                        </div>
                      </div>
                    </td>
                    <td className="py-4 align-middle">
                      <button
                        className="rounded-full border border-[var(--q-info)]/25 bg-[var(--q-info-bg)] px-4 py-2.5 text-sm font-medium text-[var(--q-info)] transition hover:opacity-90"
                        type="button"
                        onClick={() => handleEdit(student)}
                      >
                        Editar
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {filteredStudents.length === 0 ? (
            <div className="mt-4 rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
              Nenhum aluno encontrado para esta busca.
            </div>
          ) : null}
        </GlassCard>
      </div>
    </div>
  );
}

function resolveFinancialTone(status?: string) {
  switch (status) {
    case "Delinquent":
      return "text-sm font-medium text-[var(--q-danger)]";
    case "DueSoon":
      return "text-sm font-medium text-[#B58100]";
    default:
      return "text-sm font-medium text-[var(--q-success)]";
  }
}

function MetricCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-4">
      <div className="text-xs uppercase tracking-[0.2em] text-[var(--q-muted)]">{label}</div>
      <div className="mt-2 text-xl font-semibold text-[var(--q-text)]">{value}</div>
    </div>
  );
}
