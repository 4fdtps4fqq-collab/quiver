import { useEffect, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock, StatusBadge } from "../components/OperationsUi";
import { formatCurrency, formatDateTime, formatMinutes } from "../lib/formatters";
import {
  createEnrollment,
  getCourses,
  getEnrollmentLedger,
  getEnrollments,
  getStudents,
  updateEnrollmentStatus,
  type Course,
  type Enrollment,
  type EnrollmentLedgerEntry,
  type Student
} from "../lib/platform-api";

const enrollmentStatuses = [
  { value: 1, label: "Ativa" },
  { value: 2, label: "Concluída" },
  { value: 3, label: "Cancelada" },
  { value: 4, label: "Expirada" }
];

const initialForm = {
  studentId: "",
  courseId: "",
  startedAtUtc: ""
};

export function EnrollmentsPage() {
  const { token } = useSession();
  const [students, setStudents] = useState<Student[]>([]);
  const [courses, setCourses] = useState<Course[]>([]);
  const [enrollments, setEnrollments] = useState<Enrollment[]>([]);
  const [selectedEnrollmentId, setSelectedEnrollmentId] = useState<string>("");
  const [ledger, setLedger] = useState<EnrollmentLedgerEntry[]>([]);
  const [statusValue, setStatusValue] = useState("1");
  const [form, setForm] = useState(initialForm);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const selectedEnrollment = enrollments.find((item) => item.id === selectedEnrollmentId) ?? null;

  useEffect(() => {
    if (!token) {
      return;
    }

    void loadData(token);
  }, [token]);

  useEffect(() => {
    if (!token || !selectedEnrollmentId) {
      setLedger([]);
      return;
    }

    void loadLedger(token, selectedEnrollmentId);
  }, [selectedEnrollmentId, token]);

  async function loadData(currentToken: string) {
    try {
      setIsLoading(true);
      setError(null);

      const [studentsData, coursesData, enrollmentsData] = await Promise.all([
        getStudents(currentToken),
        getCourses(currentToken),
        getEnrollments(currentToken)
      ]);

      setStudents(studentsData);
      setCourses(coursesData);
      setEnrollments(enrollmentsData);

      if (!selectedEnrollmentId && enrollmentsData.length > 0) {
        setSelectedEnrollmentId(enrollmentsData[0].id);
        const statusOption = enrollmentStatuses.find(
          (item) => item.label === enrollmentsData[0].status
        );
        setStatusValue(String(statusOption?.value ?? 1));
      }
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar as matrículas.");
    } finally {
      setIsLoading(false);
    }
  }

  async function loadLedger(currentToken: string, enrollmentId: string) {
    try {
      setError(null);
      const items = await getEnrollmentLedger(currentToken, enrollmentId);
      setLedger(items);
      const current = enrollments.find((item) => item.id === enrollmentId);
      const statusOption = enrollmentStatuses.find((item) => item.label === current?.status);
      setStatusValue(String(statusOption?.value ?? 1));
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar o ledger.");
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
      await createEnrollment(token, {
        studentId: form.studentId,
        courseId: form.courseId,
        startedAtUtc: form.startedAtUtc ? new Date(form.startedAtUtc).toISOString() : null
      });
      setForm(initialForm);
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível criar a matrícula.");
    } finally {
      setIsSaving(false);
    }
  }

  async function handleStatusUpdate() {
    if (!token || !selectedEnrollment) {
      return;
    }

    try {
      setIsUpdatingStatus(true);
      setError(null);
      const nextStatus = Number(statusValue);
      await updateEnrollmentStatus(token, selectedEnrollment.id, {
        status: nextStatus,
        endedAtUtc: nextStatus === 1 ? null : new Date().toISOString()
      });
      await loadData(token);
      await loadLedger(token, selectedEnrollment.id);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível atualizar o status.");
    } finally {
      setIsUpdatingStatus(false);
    }
  }

  const activeCount = enrollments.filter((item) => item.status === "Active").length;
  const averageRemaining =
    enrollments.length === 0
      ? 0
      : enrollments.reduce((sum, item) => sum + item.remainingMinutes, 0) / enrollments.length;
  const contractedRevenue = enrollments.reduce((sum, item) => sum + item.coursePriceSnapshot, 0);

  return (
    <div className="space-y-6">
      <PageHero
        eyebrow="Matrículas"
        title="Matrículas com snapshot comercial preservado e saldo explicável por ledger."
        description="Cada contratação congela a carga horária e o preço do curso, enquanto o consumo da agenda permanece rastreável minuto a minuto pelo histórico."
        stats={[
          { label: "Ativas", value: String(activeCount) },
          { label: "Total", value: String(enrollments.length) },
          { label: "Saldo médio", value: formatMinutes(Math.round(averageRemaining)) },
          { label: "Receita snapshot", value: formatCurrency(contractedRevenue) }
        ]}
      />

      {isLoading ? <LoadingBlock label="Carregando matrículas" /> : null}
      {error ? <ErrorBlock message={error} /> : null}

      <div className="grid gap-4 xl:grid-cols-[0.85fr_1.15fr]">
        <GlassCard
          title="Nova matrícula"
          description="Crie uma nova contratação usando o curso ativo e congelando a carga horária e o preço daquele pacote neste momento."
        >
          <form className="grid gap-3" onSubmit={handleSubmit}>
            <select
              className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
              value={form.studentId}
              onChange={(event) => setForm((current) => ({ ...current, studentId: event.target.value }))}
              required
            >
              <option value="">Selecione o aluno</option>
              {students.map((student) => (
                <option key={student.id} value={student.id}>
                  {student.fullName}
                </option>
              ))}
            </select>
            <select
              className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
              value={form.courseId}
              onChange={(event) => setForm((current) => ({ ...current, courseId: event.target.value }))}
              required
            >
              <option value="">Selecione o curso</option>
              {courses.map((course) => (
                <option key={course.id} value={course.id}>
                  {course.name} - {course.level} - {formatMinutes(course.totalMinutes)}
                </option>
              ))}
            </select>
            <input
              className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
              type="datetime-local"
              value={form.startedAtUtc}
              onChange={(event) => setForm((current) => ({ ...current, startedAtUtc: event.target.value }))}
            />
            <button
              className="rounded-full border border-transparent bg-[var(--q-grad-brand)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95"
              type="submit"
              disabled={isSaving}
            >
              {isSaving ? "Salvando" : "Criar matrícula"}
            </button>
          </form>
        </GlassCard>

        <GlassCard title="Carteira ativa" description="Selecione uma matrícula para inspecionar o ledger de consumo e ajuste de status.">
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
              <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                <tr>
                  <th className="pb-3">Aluno</th>
                  <th className="pb-3">Curso</th>
                  <th className="pb-3">Módulo</th>
                  <th className="pb-3">Progresso</th>
                  <th className="pb-3">Saldo</th>
                  <th className="pb-3">Snapshot</th>
                  <th className="pb-3">Status</th>
                </tr>
              </thead>
              <tbody>
                {enrollments.map((enrollment) => (
                  <tr
                    key={enrollment.id}
                    className={`cursor-pointer border-t border-[var(--q-border)] ${selectedEnrollmentId === enrollment.id ? "bg-[var(--q-info-bg)]" : ""}`}
                    onClick={() => setSelectedEnrollmentId(enrollment.id)}
                  >
                    <td className="py-3 font-medium text-[var(--q-text)]">{enrollment.studentName}</td>
                    <td className="py-3">{enrollment.courseName}</td>
                    <td className="py-3">{enrollment.currentModule}</td>
                    <td className="py-3">{Math.round(enrollment.progressPercent)}%</td>
                    <td className="py-3">
                      {formatMinutes(enrollment.remainingMinutes)} / {formatMinutes(enrollment.includedMinutesSnapshot)}
                    </td>
                    <td className="py-3">{formatCurrency(enrollment.coursePriceSnapshot)}</td>
                    <td className="py-3">
                      <StatusBadge value={enrollment.status} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </GlassCard>
      </div>

      <div className="grid gap-4 xl:grid-cols-[0.8fr_1.2fr]">
        <GlassCard
          title="Controle da matrícula"
          description="Ajuste o status quando a contratação for concluída, expirada ou cancelada."
        >
          {selectedEnrollment ? (
            <div className="space-y-4">
              <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                <div className="text-sm font-medium text-[var(--q-text)]">{selectedEnrollment.studentName}</div>
                <div className="mt-2 text-sm text-[var(--q-text-2)]">{selectedEnrollment.courseName}</div>
                <div className="mt-3 grid gap-3 md:grid-cols-3">
                  <div>
                    <div className="text-xs uppercase tracking-[0.2em] text-[var(--q-muted)]">Módulo atual</div>
                    <div className="mt-1 text-sm font-medium text-[var(--q-text)]">{selectedEnrollment.currentModule}</div>
                  </div>
                  <div>
                    <div className="text-xs uppercase tracking-[0.2em] text-[var(--q-muted)]">Progresso</div>
                    <div className="mt-1 text-sm font-medium text-[var(--q-text)]">{Math.round(selectedEnrollment.progressPercent)}%</div>
                  </div>
                  <div>
                    <div className="text-xs uppercase tracking-[0.2em] text-[var(--q-muted)]">No-show</div>
                    <div className="mt-1 text-sm font-medium text-[var(--q-text)]">{selectedEnrollment.noShowCount}</div>
                  </div>
                </div>
                <div className="mt-3">
                  <StatusBadge value={selectedEnrollment.status} />
                </div>
              </div>

              <select
                className="w-full rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                value={statusValue}
                onChange={(event) => setStatusValue(event.target.value)}
              >
                {enrollmentStatuses.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>

              <button
                className="w-full rounded-full border border-[var(--q-warning)]/25 bg-[var(--q-warning-bg)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-[#B58100] transition hover:opacity-95"
                type="button"
                onClick={handleStatusUpdate}
                disabled={isUpdatingStatus}
              >
                {isUpdatingStatus ? "Atualizando" : "Atualizar status"}
              </button>
            </div>
          ) : (
            <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
              Selecione uma matrícula para ver detalhes.
            </div>
          )}
        </GlassCard>

        <GlassCard
          title="Ledger de saldo"
          description="Cada transição de aula realizada gera um movimento compensatório em minutos para manter o saldo da matrícula auditável."
        >
          {selectedEnrollment ? (
            <div className="space-y-3">
              <div className="grid gap-3 md:grid-cols-4">
                <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                  <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">Snapshot</div>
                  <div className="mt-3 text-2xl font-semibold text-[var(--q-text)]">
                    {formatMinutes(selectedEnrollment.includedMinutesSnapshot)}
                  </div>
                </div>
                <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                  <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">Consumidas</div>
                  <div className="mt-3 text-2xl font-semibold text-[var(--q-text)]">{formatMinutes(selectedEnrollment.usedMinutes)}</div>
                </div>
                <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                  <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">Restantes</div>
                  <div className="mt-3 text-2xl font-semibold text-[var(--q-text)]">
                    {formatMinutes(selectedEnrollment.remainingMinutes)}
                  </div>
                </div>
                <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                  <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">Início</div>
                  <div className="mt-3 text-base font-semibold text-[var(--q-text)]">
                    {formatDateTime(selectedEnrollment.startedAtUtc)}
                  </div>
                </div>
              </div>

              <div className="overflow-x-auto">
                <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
                  <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                    <tr>
                      <th className="pb-3">Quando</th>
                      <th className="pb-3">Motivo</th>
                      <th className="pb-3">Variação em horas</th>
                      <th className="pb-3">Aula</th>
                    </tr>
                  </thead>
                  <tbody>
                    {ledger.map((entry) => (
                      <tr key={entry.id} className="border-t border-[var(--q-border)]">
                        <td className="py-3">{formatDateTime(entry.occurredAtUtc)}</td>
                        <td className="py-3">{entry.reason}</td>
                        <td className="py-3 font-medium text-[var(--q-text)]">
                          {entry.deltaMinutes > 0 ? `+${formatMinutes(entry.deltaMinutes)}` : formatMinutes(entry.deltaMinutes)}
                        </td>
                        <td className="py-3">{entry.lessonId ?? "-"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ) : (
            <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
              Nenhuma matrícula selecionada.
            </div>
          )}
        </GlassCard>
      </div>
    </div>
  );
}
