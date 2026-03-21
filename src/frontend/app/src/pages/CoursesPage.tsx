import { useEffect, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock, StatusBadge } from "../components/OperationsUi";
import { formatCurrency, formatMinutes } from "../lib/formatters";
import { createCourse, getCourses, type Course, updateCourse } from "../lib/platform-api";
import { formatCurrencyInput, formatCurrencyMask, parseCurrencyInput } from "./school-admin-shared";

const levelOptions = [
  { value: 2, label: "Iniciante" },
  { value: 3, label: "Intermediário" },
  { value: 4, label: "Avançado" }
];

const initialForm = {
  name: "",
  level: "2",
  totalHours: "6",
  price: ""
};

export function CoursesPage() {
  const { token } = useSession();
  const [courses, setCourses] = useState<Course[]>([]);
  const [selectedCourseId, setSelectedCourseId] = useState("");
  const [form, setForm] = useState(initialForm);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      return;
    }

    void loadCourses(token);
  }, [token]);

  async function loadCourses(currentToken: string) {
    try {
      setIsLoading(true);
      setError(null);
      setCourses(await getCourses(currentToken));
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar os cursos.");
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    if (!selectedCourseId) {
      setForm(initialForm);
      return;
    }

    const current = courses.find((item) => item.id === selectedCourseId);
    if (!current) {
      setForm(initialForm);
      return;
    }

    setForm({
      name: current.name,
      level: String(mapLevelLabelToValue(current.level)),
      totalHours: String(current.totalHours),
      price: formatCurrencyInput(current.price)
    });
  }, [courses, selectedCourseId]);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSaving(true);
      setError(null);
      setNotice(null);
      if (selectedCourseId) {
        await updateCourse(token, selectedCourseId, {
          name: form.name,
          level: Number(form.level),
          totalHours: Number(form.totalHours),
          price: parseCurrencyInput(form.price),
          isActive: true
        });
        setNotice("Curso atualizado com sucesso.");
        await loadCourses(token);
      } else {
        await createCourse(token, {
          name: form.name,
          level: Number(form.level),
          totalHours: Number(form.totalHours),
          price: parseCurrencyInput(form.price)
        });
        setNotice("Curso cadastrado com sucesso.");
        await loadCourses(token);
      }

      setForm(initialForm);
      setSelectedCourseId("");
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar o curso.");
    } finally {
      setIsSaving(false);
    }
  }

  async function handleInactivateCourse() {
    if (!token || !selectedCourse) {
      return;
    }

    try {
      setIsSaving(true);
      setError(null);
      setNotice(null);
      await updateCourse(token, selectedCourse.id, {
        name: selectedCourse.name,
        level: mapLevelLabelToValue(selectedCourse.level),
        totalHours: selectedCourse.totalHours,
        price: selectedCourse.price,
        isActive: false
      });
      setNotice("Curso inativado com sucesso.");
      await loadCourses(token);
      setSelectedCourseId("");
      setForm(initialForm);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível inativar o curso.");
    } finally {
      setIsSaving(false);
    }
  }

  const activeCourses = courses.filter((item) => item.isActive);
  const averagePrice =
    courses.length === 0 ? 0 : courses.reduce((sum, item) => sum + item.price, 0) / courses.length;
  const totalCapacityMinutes = courses.reduce((sum, item) => sum + item.totalMinutes, 0);
  const selectedCourse = courses.find((item) => item.id === selectedCourseId) ?? null;

  return (
    <div className="space-y-6">
      <PageHero
        eyebrow="Cursos"
        title="Catálogo de cursos com snapshot consistente para venda, matrícula e agenda."
        description="Os cursos agora alimentam a operação sem perder o valor histórico contratado na matrícula, sempre controlados por carga horária."
        stats={[
          { label: "Catalogo", value: String(courses.length) },
          { label: "Ativos", value: String(activeCourses.length) },
          { label: "Ticket médio", value: formatCurrency(averagePrice) },
          { label: "Carga total", value: formatMinutes(totalCapacityMinutes) }
        ]}
      />

      {isLoading ? <LoadingBlock label="Carregando cursos" /> : null}
      {error ? <ErrorBlock message={error} /> : null}
      {notice ? <div className="rounded-[24px] border border-[var(--q-info)]/30 bg-[var(--q-info-bg)] px-5 py-4 text-sm text-[var(--q-info)]">{notice}</div> : null}

      <div className="grid gap-4 xl:grid-cols-[0.85fr_1.15fr]">
        <GlassCard title={selectedCourse ? "Ficha do curso" : "Novo curso"} description="Defina o pacote comercial preservando nível, carga horária e valor.">
          <form className="grid gap-3" onSubmit={handleSubmit}>
            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Nome do curso</span>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Como o curso aparecerá nas matrículas"
                value={form.name}
                onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))}
                required
              />
            </label>
            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Nível do curso</span>
              <select
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                value={form.level}
                onChange={(event) => setForm((current) => ({ ...current, level: event.target.value }))}
              >
                {levelOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Carga horária do curso em horas</span>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Ex.: 6 ou 7,5 horas de curso"
                type="number"
                min="0.5"
                step="0.5"
                value={form.totalHours}
                onChange={(event) => setForm((current) => ({ ...current, totalHours: event.target.value }))}
              />
            </label>
            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Valor</span>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Valor que será congelado na matrícula"
                value={form.price}
                onChange={(event) => setForm((current) => ({ ...current, price: formatCurrencyMask(event.target.value) }))}
              />
            </label>
            <div className="flex flex-wrap gap-3">
              <button
                className="rounded-full border border-transparent px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95"
                style={{ backgroundImage: "var(--q-grad-brand)", backgroundColor: "var(--q-navy)" }}
                type="submit"
                disabled={isSaving}
              >
                {isSaving ? "Salvando" : "Salvar"}
              </button>
              {selectedCourse ? (
                <>
                  <button
                    className="rounded-full border border-[var(--q-warning)]/40 bg-[var(--q-warning-bg)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-text)] transition hover:opacity-95"
                    type="button"
                    onClick={() => void handleInactivateCourse()}
                    disabled={isSaving}
                  >
                    Inativar
                  </button>
                </>
              ) : null}
            </div>
          </form>
        </GlassCard>

        <GlassCard title="Catálogo atual" description="Leitura operacional dos pacotes ativos e sua faixa de esforço.">
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
              <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                <tr>
                  <th className="pb-3">Curso</th>
                  <th className="pb-3">Nível</th>
                  <th className="pb-3">Carga horária</th>
                  <th className="pb-3">Preço</th>
                  <th className="pb-3">Status</th>
                </tr>
              </thead>
              <tbody>
                {courses.map((course) => (
                  <tr
                    key={course.id}
                    className={`cursor-pointer border-t border-[var(--q-border)] ${selectedCourse?.id === course.id ? "bg-[var(--q-info-bg)]" : ""}`}
                    onClick={() => setSelectedCourseId(course.id)}
                  >
                    <td className="py-3 font-medium text-[var(--q-text)]">{course.name}</td>
                    <td className="py-3">{translateCourseLevel(course.level)}</td>
                    <td className="py-3">{formatMinutes(course.totalMinutes)}</td>
                    <td className="py-3">{formatCurrency(course.price)}</td>
                    <td className="py-3">
                      <StatusBadge value={course.isActive ? "Active" : "Inactive"} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </GlassCard>
      </div>

      <GlassCard
        title="Trilha pedagógica do curso"
        description="Sequência sugerida de evolução para orientar instrutor, agenda e leitura de progresso."
      >
        {selectedCourse ? (
          <div className="grid gap-3 lg:grid-cols-4">
            {selectedCourse.pedagogicalTrack.map((module) => (
              <div
                key={module.id}
                className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4"
              >
                <div className="text-xs uppercase tracking-[0.2em] text-[var(--q-muted)]">
                  {module.estimatedHours}h previstas
                </div>
                <div className="mt-2 text-lg font-semibold text-[var(--q-text)]">{module.title}</div>
                <div className="mt-2 text-sm leading-6 text-[var(--q-text-2)]">{module.focus}</div>
              </div>
            ))}
          </div>
        ) : (
          <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
            Selecione um curso para visualizar a trilha pedagógica.
          </div>
        )}
      </GlassCard>
    </div>
  );
}

function translateCourseLevel(level: string) {
  switch (level) {
    case "Discovery":
      return "Descoberta";
    case "Beginner":
      return "Iniciante";
    case "Intermediate":
      return "Intermediário";
    case "Advanced":
      return "Avançado";
    default:
      return level;
  }
}

function mapLevelLabelToValue(level: string) {
  switch (level) {
    case "Discovery":
    case "Descoberta":
      return 1;
    case "Beginner":
    case "Iniciante":
      return 2;
    case "Intermediate":
    case "Intermediário":
      return 3;
    case "Advanced":
    case "Avançado":
      return 4;
    default:
      return 2;
  }
}
