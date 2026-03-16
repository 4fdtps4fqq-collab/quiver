import {
  BellRing,
  CalendarCheck2,
  CalendarSync,
  CheckCircle2,
  GraduationCap,
  LoaderCircle,
  RefreshCw,
  Sailboat,
  Sparkles,
  Target
} from "lucide-react";
import { useEffect, useMemo, useState, type CSSProperties, type Dispatch, type ReactNode, type SetStateAction } from "react";
import { Link } from "react-router-dom";
import { useSession } from "../auth/SessionContext";
import {
  cancelStudentLesson,
  confirmStudentLessonPresence,
  getStudentPortalOverview,
  rescheduleStudentLesson,
  scheduleStudentCourseLesson,
  type StudentPortalOverview
} from "../lib/platform-api";
import {
  formatDateTime,
  formatMinutes,
  formatRelativeDistance,
  fromLocalDateTimeInput,
  toLocalDateTimeInput
} from "../lib/formatters";
import { translateLabel } from "../lib/localization";
import { resolveStudentPortalTheme } from "../lib/student-portal-theme";

const defaultForm = {
  enrollmentId: "",
  instructorId: "",
  startAtUtc: "",
  durationMinutes: "90",
  notes: "",
  reason: ""
};

export function StudentPortalPage() {
  const { token, school } = useSession();
  const theme = resolveStudentPortalTheme(school);
  const [overview, setOverview] = useState<StudentPortalOverview | null>(null);
  const [form, setForm] = useState(defaultForm);
  const [activeLessonId, setActiveLessonId] = useState<string | null>(null);
  const [mode, setMode] = useState<"schedule" | "reschedule">("schedule");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      return;
    }

    void loadOverview(token);
  }, [token]);

  const bookableEnrollments = useMemo(
    () =>
      (overview?.enrollments ?? []).filter(
        (item) => item.status === "Active" && item.availableToScheduleMinutes > 0
      ),
    [overview]
  );

  async function loadOverview(sessionToken: string) {
    try {
      setLoading(true);
      setError(null);
      const payload = await getStudentPortalOverview(sessionToken);
      setOverview(payload);
      setForm((current) => ({
        ...current,
        enrollmentId: current.enrollmentId || payload.enrollments[0]?.id || "",
        instructorId: current.instructorId || payload.instructors[0]?.id || ""
      }));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Não foi possível carregar o portal do aluno.");
    } finally {
      setLoading(false);
    }
  }

  function startSchedule() {
    setMode("schedule");
    setActiveLessonId(null);
    setFeedback(null);
    setError(null);
    setForm({
      ...defaultForm,
      enrollmentId: bookableEnrollments[0]?.id ?? "",
      instructorId: overview?.instructors[0]?.id ?? ""
    });
  }

  function startReschedule(lessonId: string) {
    const lesson = overview?.upcomingLessons.find((item) => item.id === lessonId);
    if (!lesson) {
      return;
    }

    setMode("reschedule");
    setActiveLessonId(lessonId);
    setFeedback(null);
    setError(null);
    setForm({
      enrollmentId: lesson.enrollment?.enrollmentId ?? "",
      instructorId: lesson.instructor.instructorId,
      startAtUtc: toLocalDateTimeInput(lesson.startAtUtc),
      durationMinutes: String(lesson.durationMinutes),
      notes: lesson.notes ?? "",
      reason: "Preciso ajustar meu horário."
    });
  }

  async function handleSubmit() {
    if (!token) {
      return;
    }

    if (!form.instructorId || !form.startAtUtc || !form.durationMinutes) {
      setError("Preencha instrutor, data e duração para continuar.");
      return;
    }

    if (mode === "schedule" && !form.enrollmentId) {
      setError("Selecione a matrícula que vai consumir o saldo da aula.");
      return;
    }

    try {
      setSaving(true);
      setError(null);
      setFeedback(null);

      if (mode === "schedule") {
        await scheduleStudentCourseLesson(token, {
          enrollmentId: form.enrollmentId,
          instructorId: form.instructorId,
          startAtUtc: fromLocalDateTimeInput(form.startAtUtc) ?? "",
          durationMinutes: Number(form.durationMinutes),
          notes: form.notes
        });
        setFeedback("A nova aula foi agendada no seu portal.");
      } else if (activeLessonId) {
        await rescheduleStudentLesson(token, activeLessonId, {
          instructorId: form.instructorId,
          startAtUtc: fromLocalDateTimeInput(form.startAtUtc) ?? "",
          durationMinutes: Number(form.durationMinutes),
          notes: form.notes,
          reason: form.reason
        });
        setFeedback("Sua aula foi remarcada com sucesso.");
      }

      await loadOverview(token);
      startSchedule();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Não foi possível salvar a alteração.");
    } finally {
      setSaving(false);
    }
  }

  async function handleCancel(lessonId: string) {
    if (!token) {
      return;
    }

    const reason = window.prompt("Motivo do cancelamento:", "Imprevisto pessoal.");
    if (reason === null) {
      return;
    }

    try {
      setSaving(true);
      setError(null);
      setFeedback(null);
      await cancelStudentLesson(token, lessonId, { reason });
      setFeedback("A aula foi cancelada e saiu da sua agenda.");
      await loadOverview(token);
      if (activeLessonId === lessonId) {
        startSchedule();
      }
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Não foi possível cancelar a aula.");
    } finally {
      setSaving(false);
    }
  }

  async function handleConfirmPresence(lessonId: string) {
    if (!token) {
      return;
    }

    const note = window.prompt("Observação opcional para a confirmação de presença:", "Tudo certo para a aula.");
    if (note === null) {
      return;
    }

    try {
      setSaving(true);
      setError(null);
      setFeedback(null);
      await confirmStudentLessonPresence(token, lessonId, { note });
      setFeedback("Sua presença foi confirmada no portal.");
      await loadOverview(token);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Não foi possível confirmar a presença.");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <LoadingBlock label="Carregando portal do aluno" />;
  }

  if (!overview) {
    return <ErrorBlock message={error ?? "Não foi possível abrir o portal do aluno."} />;
  }

  return (
    <div className="space-y-6">
      <section
        className="rounded-[34px] p-6 shadow-2xl backdrop-blur-2xl"
        style={{ border: `1px solid ${theme.frame}`, background: theme.heroBackground }}
      >
        <div className="grid gap-6 xl:grid-cols-[1.2fr_0.8fr]">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full px-3 py-1 text-xs uppercase tracking-[0.35em] text-slate-700" style={{ border: `1px solid ${theme.frame}`, backgroundColor: "rgba(255,255,255,0.72)" }}>
              <Sparkles size={14} />
              Visão geral do treino
            </div>
            <h2 className="mt-4 text-3xl font-semibold tracking-tight text-slate-950">
              {overview.student.fullName}, sua próxima borda está organizada.
            </h2>
            <p className="mt-3 max-w-2xl text-sm leading-6 text-slate-700">
              O portal mostra seu progresso por etapa, deixa claras as regras da escola e facilita agenda, presença e próximos passos.
            </p>
            <div className="mt-5 grid gap-3 md:grid-cols-3">
              <MetricCard label="Momento do treino" value={overview.summary.trainingStage} />
              <MetricCard label="Prontidão atual" value={`${overview.progress.readinessScore}%`} />
              <MetricCard label="Perfil preenchido" value={`${overview.summary.profileCompleteness}%`} />
            </div>
            <div className="mt-5 grid gap-3 md:hidden">
              <MobileGuideCard
                title="O que fazer agora"
                description={`${overview.progress.currentFocus}. Proximo passo: ${overview.progress.recommendedNextStep}`}
                style={{ border: `1px solid ${theme.frame}`, background: theme.mutedCardBackground }}
              />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <StatCard icon={Sailboat} label="Aulas realizadas" value={String(overview.summary.totalRealizedLessons)} />
            <StatCard icon={CalendarCheck2} label="Aulas futuras" value={String(overview.summary.totalUpcomingLessons)} />
            <StatCard icon={GraduationCap} label="Matrículas ativas" value={String(overview.summary.activeEnrollments)} />
            <StatCard icon={BellRing} label="Notificações novas" value={String(overview.notifications.unreadCount)} />
          </div>
        </div>
        <div className="mt-6 grid gap-3 lg:grid-cols-4">
          <RuleChip title="Agendamento" value={`${overview.portalRules.bookingLeadTimeMinutes} min`} />
          <RuleChip title="Cancelamento" value={`${overview.portalRules.cancellationWindowHours} h`} />
          <RuleChip title="Remarcação" value={`${overview.portalRules.rescheduleWindowHours} h`} />
          <RuleChip title="Presença" value={`${overview.portalRules.attendanceConfirmationLeadMinutes} min antes`} />
        </div>
        <div className="mt-6 grid gap-3 md:grid-cols-3">
          <MobileGuideCard
            title="Escola ativa"
            description={school?.displayName ?? "Sua escola atual"}
            style={{ border: `1px solid ${theme.frame}`, background: theme.cardBackground }}
          />
          <MobileGuideCard
            title="Lembretes"
            description={
              overview.portalRules.portalNotificationsEnabled
                ? `Você recebe avisos até ${overview.portalRules.lessonReminderLeadHours}h antes.`
                : "A escola desativou lembretes automáticos do portal."
            }
            style={{ border: `1px solid ${theme.frame}`, background: theme.cardBackground }}
          />
          <MobileGuideCard
            title="Saldo disponivel"
            description={
              overview.enrollments.length > 0
                ? `${formatMinutes(overview.enrollments[0].availableToScheduleMinutes)} livres na primeira matrícula ativa.`
                : "Sem matrícula ativa para novos agendamentos."
            }
            style={{ border: `1px solid ${theme.frame}`, background: theme.cardBackground }}
          />
        </div>
      </section>

      <div className="grid gap-6 xl:grid-cols-[1.08fr_0.92fr]">
        <section className="space-y-6">
          <ScheduleComposer
            mode={mode}
            form={form}
            saving={saving}
            bookableEnrollments={bookableEnrollments}
            instructors={overview.instructors}
            onModeReset={startSchedule}
            onChange={setForm}
            onSubmit={() => void handleSubmit()}
            onRefresh={() => token && void loadOverview(token)}
          />
          {feedback ? <SuccessBlock message={feedback} /> : null}
          {error ? <ErrorBlock message={error} /> : null}

          <article className="rounded-[32px] p-6 shadow-xl backdrop-blur-2xl" style={{ border: `1px solid ${theme.frame}`, background: theme.cardBackground }}>
            <div className="flex items-start justify-between gap-4">
              <div>
                <div className="inline-flex items-center gap-2 text-xs uppercase tracking-[0.35em] text-slate-500">
                  <Target size={14} />
                  Progresso pedagógico
                </div>
                <h3 className="mt-3 text-2xl font-semibold text-slate-950">Módulos e habilidades</h3>
              </div>
              <div className="rounded-[24px] px-4 py-4 text-sm leading-6 text-slate-700" style={{ border: `1px solid ${theme.frame}`, background: theme.mutedCardBackground }}>
                <div className="text-xs uppercase tracking-[0.25em] text-slate-500">Foco atual</div>
                <div className="mt-2 font-medium text-slate-950">{overview.progress.currentFocus}</div>
                <div className="mt-2">{overview.progress.recommendedNextStep}</div>
              </div>
            </div>

            <div className="mt-5 space-y-4">
              {overview.progress.modules.map((module) => (
                <div key={module.id} className="rounded-[24px] bg-white/80 p-5" style={{ border: `1px solid ${theme.frame}` }}>
                  <div className="flex items-start justify-between gap-4">
                    <div>
                      <div className="text-sm font-semibold text-slate-950">{module.title}</div>
                      <div className="mt-1 text-sm leading-6 text-slate-600">{module.description}</div>
                    </div>
                      <div className="rounded-2xl px-4 py-3 text-white" style={{ border: `1px solid ${theme.frame}`, backgroundColor: theme.primary }}>
                      <div className="text-xs uppercase tracking-[0.25em] text-slate-300">{translateLabel(module.status)}</div>
                      <div className="mt-1 text-lg font-semibold">{module.progressPercent}%</div>
                    </div>
                  </div>
                  <div className="mt-4 grid gap-3 md:grid-cols-3">
                    {module.skills.map((skill) => (
                      <MetricCard key={skill.title} label={skill.title} value={`${skill.progressPercent}% • ${translateLabel(skill.status)}`} />
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </article>
        </section>

        <section className="space-y-6">
          <article className="rounded-[32px] p-6 shadow-xl backdrop-blur-2xl" style={{ border: `1px solid ${theme.frame}`, background: theme.cardBackground }}>
            <div className="flex items-start justify-between gap-4">
              <div>
                <div className="text-xs uppercase tracking-[0.35em] text-slate-500">Agenda do aluno</div>
                <h3 className="mt-3 text-2xl font-semibold text-slate-950">Suas próximas aulas</h3>
              </div>
              <Link to="/student/history" className="text-sm font-medium text-cyan-800 hover:text-cyan-950">Abrir histórico</Link>
            </div>
            <div className="mt-5 space-y-4">
              {overview.upcomingLessons.length === 0 ? <EmptyState message="Nenhuma aula futura na agenda." /> : overview.upcomingLessons.map((lesson) => (
                <div key={lesson.id} className="rounded-[24px] bg-white/85 p-4" style={{ border: `1px solid ${theme.frame}` }}>
                  <div className="text-sm font-semibold text-slate-950">{lesson.enrollment?.courseName ?? translateLabel(lesson.kind)}</div>
                  <div className="mt-1 text-sm text-slate-700">{formatDateTime(lesson.startAtUtc)} • {formatMinutes(lesson.durationMinutes)}</div>
                  <div className="mt-2 text-sm text-slate-600">Instrutor: {lesson.instructor.name} • Faltam: {formatRelativeDistance(lesson.startAtUtc)}</div>
                  {lesson.studentConfirmedAtUtc ? <div className="mt-2 text-sm text-emerald-700">Presença confirmada em {formatDateTime(lesson.studentConfirmedAtUtc)}</div> : null}
                  <div className="mt-4 flex flex-wrap gap-3">
                    {lesson.canConfirmPresence ? <ActionButton label="Confirmar presença" onClick={() => void handleConfirmPresence(lesson.id)} icon={<CheckCircle2 size={16} />} tone="success" /> : null}
                    {lesson.canReschedule ? <ActionButton label="Remarcar" onClick={() => startReschedule(lesson.id)} icon={<CalendarSync size={16} />} tone="dark" /> : null}
                    {lesson.canCancel ? <ActionButton label="Cancelar aula" onClick={() => void handleCancel(lesson.id)} tone="danger" /> : null}
                  </div>
                </div>
              ))}
            </div>
          </article>

          <article className="rounded-[32px] p-6 shadow-xl backdrop-blur-2xl" style={{ border: `1px solid ${theme.frame}`, background: theme.cardBackground }}>
            <div className="flex items-start justify-between gap-4">
              <div>
                <div className="inline-flex items-center gap-2 text-xs uppercase tracking-[0.35em] text-slate-500">
                  <BellRing size={14} />
                  Central de notificações
                </div>
                <h3 className="mt-3 text-2xl font-semibold text-slate-950">O que merece sua atenção agora</h3>
              </div>
              <Link to="/student/notifications" className="text-sm font-medium text-cyan-800 hover:text-cyan-950">Ver tudo</Link>
            </div>
            <div className="mt-5 space-y-3">
              {overview.notifications.items.length === 0 ? <EmptyState message="Nenhuma notificação nova no momento." /> : overview.notifications.items.map((item) => (
                <div key={item.id} className="rounded-[22px] px-4 py-4" style={{ border: `1px solid ${theme.frame}`, background: theme.mutedCardBackground }}>
                  <div className="text-sm font-medium text-slate-950">{item.title}</div>
                  <div className="mt-1 text-sm leading-6 text-slate-600">{item.message}</div>
                  <div className="mt-3 text-xs uppercase tracking-[0.22em] text-slate-500">{formatDateTime(item.createdAtUtc)}</div>
                </div>
              ))}
            </div>
          </article>

          <article className="rounded-[32px] p-6 shadow-xl backdrop-blur-2xl" style={{ border: `1px solid ${theme.frame}`, background: theme.cardBackground }}>
            <div className="text-xs uppercase tracking-[0.35em] text-slate-500">Progressão do curso</div>
            <h3 className="mt-3 text-2xl font-semibold text-slate-950">Carga horária das matrículas</h3>
            <div className="mt-5 space-y-4">
              {overview.enrollments.length === 0 ? <EmptyState message="Nenhuma matrícula vinculada ao seu cadastro." /> : overview.enrollments.map((enrollment) => (
                <div key={enrollment.id} className="rounded-[24px] bg-white/85 p-4" style={{ border: `1px solid ${theme.frame}` }}>
                  <div className="flex items-start justify-between gap-4">
                    <div>
                      <div className="text-sm font-semibold text-slate-950">{enrollment.courseName}</div>
                      <div className="mt-1 text-sm text-slate-600">{translateLabel(enrollment.status)} • {translateLabel(enrollment.level)}</div>
                    </div>
                    <div className="text-lg font-semibold text-slate-950">{enrollment.progressPercent}%</div>
                  </div>
                  <div className="mt-4 grid gap-3 sm:grid-cols-3">
                    <MetricCard label="Contratada" value={formatMinutes(enrollment.includedMinutesSnapshot)} />
                    <MetricCard label="Realizada" value={formatMinutes(enrollment.usedMinutes)} />
                    <MetricCard label="Livre para agendar" value={formatMinutes(enrollment.availableToScheduleMinutes)} />
                  </div>
                </div>
              ))}
            </div>
          </article>
        </section>
      </div>
    </div>
  );
}

function ScheduleComposer({
  mode,
  form,
  saving,
  bookableEnrollments,
  instructors,
  onModeReset,
  onChange,
  onSubmit,
  onRefresh
}: {
  mode: "schedule" | "reschedule";
  form: typeof defaultForm;
  saving: boolean;
  bookableEnrollments: StudentPortalOverview["enrollments"];
  instructors: StudentPortalOverview["instructors"];
  onModeReset: () => void;
  onChange: Dispatch<SetStateAction<typeof defaultForm>>;
  onSubmit: () => void;
  onRefresh: () => void;
}) {
  const { school } = useSession();
  const theme = resolveStudentPortalTheme(school);

  return (
    <article className="rounded-[32px] p-6 shadow-xl backdrop-blur-2xl" style={{ border: `1px solid ${theme.frame}`, background: theme.cardBackground }}>
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <div className="inline-flex items-center gap-2 text-xs uppercase tracking-[0.35em] text-slate-500">
            <CalendarSync size={14} />
            {mode === "schedule" ? "Novo agendamento" : "Remarcação"}
          </div>
          <h3 className="mt-3 text-2xl font-semibold text-slate-950">{mode === "schedule" ? "Agendar próxima aula do curso" : "Remarcar aula já agendada"}</h3>
        </div>
        {mode === "reschedule" ? <ActionButton label="Voltar para novo agendamento" onClick={onModeReset} icon={<RefreshCw size={16} />} tone="dark" /> : null}
      </div>
      <div className="mt-4 grid gap-3 md:grid-cols-3">
        <MobileGuideCard title="Janela para agendar" description={`${school?.settings?.bookingLeadTimeMinutes ?? 0} minutos de antecedência mínima.`} style={{ border: `1px solid ${theme.frame}`, background: theme.mutedCardBackground }} />
        <MobileGuideCard title="Janela para remarcar" description={`${school?.settings?.rescheduleWindowHours ?? 0} horas antes do início da aula.`} style={{ border: `1px solid ${theme.frame}`, background: theme.mutedCardBackground }} />
        <MobileGuideCard title="Presença no portal" description={`${school?.settings?.attendanceConfirmationLeadMinutes ?? 0} minutos antes da aula.`} style={{ border: `1px solid ${theme.frame}`, background: theme.mutedCardBackground }} />
      </div>
      <div className="mt-6 grid gap-4 md:grid-cols-2">
        <label className="grid gap-2 text-sm text-slate-800">
          <span>Matrícula que vai consumir saldo horário</span>
          <select value={form.enrollmentId} onChange={(event) => onChange((current) => ({ ...current, enrollmentId: event.target.value }))} disabled={mode === "reschedule"} className="rounded-2xl border border-slate-900/10 bg-white px-4 py-3 outline-none transition focus:border-cyan-400 disabled:bg-slate-100">
            <option value="">Selecione a matrícula ativa</option>
            {bookableEnrollments.map((item) => <option key={item.id} value={item.id}>{item.courseName} • {formatMinutes(item.availableToScheduleMinutes)} livres</option>)}
          </select>
        </label>
        <label className="grid gap-2 text-sm text-slate-800">
          <span>Instrutor desejado</span>
          <select value={form.instructorId} onChange={(event) => onChange((current) => ({ ...current, instructorId: event.target.value }))} className="rounded-2xl border border-slate-900/10 bg-white px-4 py-3 outline-none transition focus:border-cyan-400">
            <option value="">Selecione o instrutor</option>
            {instructors.map((item) => <option key={item.id} value={item.id}>{item.fullName}{item.specialties ? ` • ${item.specialties}` : ""}</option>)}
          </select>
        </label>
        <label className="grid gap-2 text-sm text-slate-800">
          <span>Data e horário pretendidos</span>
          <input type="datetime-local" value={form.startAtUtc} onChange={(event) => onChange((current) => ({ ...current, startAtUtc: event.target.value }))} className="rounded-2xl border border-slate-900/10 bg-white px-4 py-3 outline-none transition focus:border-cyan-400" />
        </label>
        <label className="grid gap-2 text-sm text-slate-800">
          <span>Duração da aula em minutos</span>
          <input type="number" min={30} step={15} value={form.durationMinutes} onChange={(event) => onChange((current) => ({ ...current, durationMinutes: event.target.value }))} className="rounded-2xl border border-slate-900/10 bg-white px-4 py-3 outline-none transition focus:border-cyan-400" />
        </label>
      </div>
      <div className="mt-4 flex flex-wrap gap-3">
        <ActionButton label={mode === "schedule" ? "Agendar aula" : "Confirmar remarcação"} onClick={onSubmit} icon={saving ? <LoaderCircle size={16} className="animate-spin" /> : <CalendarSync size={16} />} tone="dark" />
        <ActionButton label="Atualizar portal" onClick={onRefresh} icon={<RefreshCw size={16} />} />
        <Link to="/student/profile" className="inline-flex items-center gap-2 rounded-2xl border border-cyan-300/30 bg-cyan-50 px-4 py-3 text-sm text-cyan-950 transition hover:bg-cyan-100">Revisar perfil</Link>
      </div>
    </article>
  );
}

function ActionButton({ label, onClick, icon, tone = "light" }: { label: string; onClick: () => void; icon?: ReactNode; tone?: "light" | "dark" | "success" | "danger" }) {
  const className = tone === "dark"
    ? "border border-slate-950/10 bg-slate-950 text-white hover:bg-slate-800"
    : tone === "success"
      ? "border border-emerald-300/60 bg-emerald-50 text-emerald-900 hover:bg-emerald-100"
      : tone === "danger"
        ? "border border-rose-300/70 bg-rose-50 text-rose-900 hover:bg-rose-100"
        : "border border-slate-950/10 bg-white text-slate-900 hover:bg-slate-50";

  return <button onClick={onClick} className={`inline-flex items-center gap-2 rounded-2xl px-4 py-3 text-sm transition ${className}`}>{icon}{label}</button>;
}

function StatCard({ icon: Icon, label, value }: { icon: typeof CalendarCheck2; label: string; value: string }) {
  return <div className="rounded-[28px] border border-slate-950/8 bg-white/75 p-4 shadow-lg"><div className="flex items-center justify-between gap-3"><div className="text-xs uppercase tracking-[0.25em] text-slate-500">{label}</div><div className="rounded-2xl bg-slate-950 p-2 text-white"><Icon size={16} /></div></div><div className="mt-4 text-3xl font-semibold tracking-tight text-slate-950">{value}</div></div>;
}

function MetricCard({ label, value }: { label: string; value: string }) {
  return <div className="rounded-[22px] border border-slate-950/8 bg-white/75 px-4 py-4"><div className="text-xs uppercase tracking-[0.25em] text-slate-500">{label}</div><div className="mt-2 text-sm font-medium text-slate-950">{value}</div></div>;
}

function RuleChip({ title, value }: { title: string; value: string }) {
  return <div className="rounded-[24px] border border-white/55 bg-white/70 px-4 py-4"><div className="text-xs uppercase tracking-[0.25em] text-slate-500">{title}</div><div className="mt-2 text-lg font-semibold text-slate-950">{value}</div></div>;
}

function MobileGuideCard({
  title,
  description,
  style
}: {
  title: string;
  description: string;
  style?: CSSProperties;
}) {
  return (
    <div className="rounded-[24px] px-4 py-4" style={style}>
      <div className="text-xs uppercase tracking-[0.25em] text-slate-500">{title}</div>
      <div className="mt-2 text-sm leading-6 text-slate-700">{description}</div>
    </div>
  );
}

function EmptyState({ message }: { message: string }) {
  return <div className="rounded-[24px] border border-dashed border-slate-300 bg-slate-50/80 px-4 py-6 text-sm text-slate-600">{message}</div>;
}

function LoadingBlock({ label }: { label: string }) {
  return <div className="flex min-h-[60vh] items-center justify-center rounded-[32px] border border-white/40 bg-white/55 text-sm uppercase tracking-[0.35em] text-slate-700 shadow-xl backdrop-blur-xl">{label}</div>;
}

function SuccessBlock({ message }: { message: string }) {
  return <div className="rounded-[24px] border border-emerald-300/60 bg-emerald-100/80 px-4 py-3 text-sm text-emerald-950">{message}</div>;
}

function ErrorBlock({ message }: { message: string }) {
  return <div className="rounded-[24px] border border-rose-300/60 bg-rose-100/80 px-4 py-3 text-sm text-rose-950">{message}</div>;
}
