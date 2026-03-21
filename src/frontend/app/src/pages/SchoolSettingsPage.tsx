import { useEffect, useMemo, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { getSchoolCurrent, updateSchoolSettings, type SchoolCurrentResponse } from "../lib/auth-api";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock } from "../components/OperationsUi";
import { MetricTile, NumericField, TextField } from "./school-admin-shared";
import { Link } from "react-router-dom";

export function SchoolSettingsPage() {
  const { token } = useSession();
  const [school, setSchool] = useState<SchoolCurrentResponse | null>(null);
  const [settingsForm, setSettingsForm] = useState({
    bookingLeadTimeMinutes: "60",
    cancellationWindowHours: "24",
    rescheduleWindowHours: "24",
    attendanceConfirmationLeadMinutes: "180",
    lessonReminderLeadHours: "18",
    instructorBufferMinutes: "15",
    noShowGraceMinutes: "15",
    portalNotificationsEnabled: true,
    noShowConsumesCourseMinutes: true,
    noShowChargesSingleLesson: true,
    autoCreateEnrollmentRevenue: true,
    autoCreateSingleLessonRevenue: true,
    themePrimary: "#0E3A52",
    themeAccent: "#2ED4A7"
  });
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const schoolStats = useMemo(
    () => [
      { label: "Timezone", value: school?.timezone ?? "-" },
      { label: "Moeda", value: school?.currencyCode ?? "-" },
      { label: "Status", value: school?.status ?? "-" },
      { label: "Tema", value: school ? "Personalizado" : "-" }
    ],
    [school]
  );

  useEffect(() => {
    if (!token) {
      return;
    }

    void loadData(token);
  }, [token]);

  async function loadData(currentToken: string) {
    try {
      setIsLoading(true);
      setError(null);
      const schoolData = await getSchoolCurrent(currentToken);
      setSchool(schoolData);
      setSettingsForm({
        bookingLeadTimeMinutes: String(schoolData.settings?.bookingLeadTimeMinutes ?? 60),
        cancellationWindowHours: String(schoolData.settings?.cancellationWindowHours ?? 24),
        rescheduleWindowHours: String(schoolData.settings?.rescheduleWindowHours ?? 24),
        attendanceConfirmationLeadMinutes: String(
          schoolData.settings?.attendanceConfirmationLeadMinutes ?? 180
        ),
        lessonReminderLeadHours: String(schoolData.settings?.lessonReminderLeadHours ?? 18),
        instructorBufferMinutes: String(schoolData.settings?.instructorBufferMinutes ?? 15),
        noShowGraceMinutes: String(schoolData.settings?.noShowGraceMinutes ?? 15),
        portalNotificationsEnabled: schoolData.settings?.portalNotificationsEnabled ?? true,
        noShowConsumesCourseMinutes: schoolData.settings?.noShowConsumesCourseMinutes ?? true,
        noShowChargesSingleLesson: schoolData.settings?.noShowChargesSingleLesson ?? true,
        autoCreateEnrollmentRevenue: schoolData.settings?.autoCreateEnrollmentRevenue ?? true,
        autoCreateSingleLessonRevenue: schoolData.settings?.autoCreateSingleLessonRevenue ?? true,
        themePrimary: schoolData.settings?.themePrimary ?? "#0E3A52",
        themeAccent: schoolData.settings?.themeAccent ?? "#2ED4A7"
      });
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar a escola.");
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
      await updateSchoolSettings(token, {
        bookingLeadTimeMinutes: Number(settingsForm.bookingLeadTimeMinutes),
        cancellationWindowHours: Number(settingsForm.cancellationWindowHours),
        rescheduleWindowHours: Number(settingsForm.rescheduleWindowHours),
        attendanceConfirmationLeadMinutes: Number(settingsForm.attendanceConfirmationLeadMinutes),
        lessonReminderLeadHours: Number(settingsForm.lessonReminderLeadHours),
        instructorBufferMinutes: Number(settingsForm.instructorBufferMinutes),
        noShowGraceMinutes: Number(settingsForm.noShowGraceMinutes),
        portalNotificationsEnabled: settingsForm.portalNotificationsEnabled,
        noShowConsumesCourseMinutes: settingsForm.noShowConsumesCourseMinutes,
        noShowChargesSingleLesson: settingsForm.noShowChargesSingleLesson,
        autoCreateEnrollmentRevenue: settingsForm.autoCreateEnrollmentRevenue,
        autoCreateSingleLessonRevenue: settingsForm.autoCreateSingleLessonRevenue,
        themePrimary: settingsForm.themePrimary,
        themeAccent: settingsForm.themeAccent
      });
      setNotice("Parâmetros da escola salvos com sucesso.");
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar os parâmetros da escola.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <div className="space-y-6">
      <PageHero title="Parâmetros da escola" description="Centralize as regras operacionais, branding e políticas do portal em uma tela focada só na escola." stats={schoolStats} statsBelow />
      {isLoading ? <LoadingBlock label="Carregando parâmetros da escola" /> : null}
      {error ? <ErrorBlock message={error} /> : null}
      {notice ? (
        <div className="rounded-[24px] border border-[var(--q-info)]/30 bg-[var(--q-info-bg)] px-5 py-4 text-sm text-[var(--q-info)]">
          {notice}
        </div>
      ) : null}

      <div className="grid gap-4 xl:grid-cols-[0.92fr_1.08fr]">
        <GlassCard title="Resumo institucional" description="Identidade da escola e parâmetros ativos da operação.">
          {school ? (
            <div className="space-y-4">
              <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                <div className="text-lg font-semibold text-[var(--q-text)]">{school.displayName}</div>
                <div className="mt-2 text-sm text-[var(--q-text-2)]">{school.legalName}</div>
                <div className="mt-2 text-xs uppercase tracking-[0.2em] text-[var(--q-muted)]">
                  Identificador: {school.slug || "-"}
                </div>
              </div>

              <div className="grid gap-3 md:grid-cols-2">
                <MetricTile label="Antecedência para agendar" value={`${settingsForm.bookingLeadTimeMinutes} min`} />
                <MetricTile label="Cancelamento" value={`${settingsForm.cancellationWindowHours} h`} />
                <MetricTile label="Remarcação" value={`${settingsForm.rescheduleWindowHours} h`} />
                <MetricTile label="Confirmação de presença" value={`${settingsForm.attendanceConfirmationLeadMinutes} min`} />
                <MetricTile label="Buffer do instrutor" value={`${settingsForm.instructorBufferMinutes} min`} />
                <MetricTile label="Tolerância no-show" value={`${settingsForm.noShowGraceMinutes} min`} />
              </div>

              <div className="grid gap-3 md:grid-cols-2">
                <QuickLink to="/school/collaborators" label="Colaboradores" description="Cadastrar administrativos e instrutores." />
                <QuickLink to="/school/instructors/schedule" label="Agenda dos instrutores" description="Gerir disponibilidade real e bloqueios." />
                <QuickLink to="/school/invitations" label="Convites" description="Onboarding guiado por e-mail." />
                <QuickLink to="/school/audit" label="Auditoria" description="Acessos, senhas e movimentações." />
              </div>
            </div>
          ) : null}
        </GlassCard>

        <GlassCard title="Regras operacionais e do portal" description="Ajuste agenda, no-show, automação financeira e a experiência do aluno.">
          <form className="grid gap-3 md:grid-cols-2" onSubmit={handleSubmit}>
            <NumericField label="Antecedência mínima para agendar em minutos" value={settingsForm.bookingLeadTimeMinutes} onChange={(value) => setSettingsForm((current) => ({ ...current, bookingLeadTimeMinutes: value }))} />
            <NumericField label="Janela para cancelar em horas" value={settingsForm.cancellationWindowHours} onChange={(value) => setSettingsForm((current) => ({ ...current, cancellationWindowHours: value }))} />
            <NumericField label="Janela para remarcar em horas" value={settingsForm.rescheduleWindowHours} onChange={(value) => setSettingsForm((current) => ({ ...current, rescheduleWindowHours: value }))} />
            <NumericField label="Antecedência para confirmar presença em minutos" value={settingsForm.attendanceConfirmationLeadMinutes} onChange={(value) => setSettingsForm((current) => ({ ...current, attendanceConfirmationLeadMinutes: value }))} />
            <NumericField label="Lembrete de aula em horas" value={settingsForm.lessonReminderLeadHours} onChange={(value) => setSettingsForm((current) => ({ ...current, lessonReminderLeadHours: value }))} />
            <NumericField label="Buffer entre aulas do instrutor em minutos" value={settingsForm.instructorBufferMinutes} onChange={(value) => setSettingsForm((current) => ({ ...current, instructorBufferMinutes: value }))} />
            <NumericField label="Tolerância para no-show em minutos" value={settingsForm.noShowGraceMinutes} onChange={(value) => setSettingsForm((current) => ({ ...current, noShowGraceMinutes: value }))} />
            <TextField label="Cor primária" value={settingsForm.themePrimary} onChange={(value) => setSettingsForm((current) => ({ ...current, themePrimary: value }))} />
            <TextField label="Cor de destaque" value={settingsForm.themeAccent} onChange={(value) => setSettingsForm((current) => ({ ...current, themeAccent: value }))} />

            <CheckLine label="Manter notificações do portal do aluno ativas" checked={settingsForm.portalNotificationsEnabled} onChange={(checked) => setSettingsForm((current) => ({ ...current, portalNotificationsEnabled: checked }))} wide />
            <CheckLine label="No-show consome saldo horário das matrículas" checked={settingsForm.noShowConsumesCourseMinutes} onChange={(checked) => setSettingsForm((current) => ({ ...current, noShowConsumesCourseMinutes: checked }))} />
            <CheckLine label="No-show cobra aula avulsa" checked={settingsForm.noShowChargesSingleLesson} onChange={(checked) => setSettingsForm((current) => ({ ...current, noShowChargesSingleLesson: checked }))} />
            <CheckLine label="Gerar receita automática na matrícula" checked={settingsForm.autoCreateEnrollmentRevenue} onChange={(checked) => setSettingsForm((current) => ({ ...current, autoCreateEnrollmentRevenue: checked }))} />
            <CheckLine label="Gerar receita automática na aula avulsa" checked={settingsForm.autoCreateSingleLessonRevenue} onChange={(checked) => setSettingsForm((current) => ({ ...current, autoCreateSingleLessonRevenue: checked }))} />

            <button
              className="rounded-full border border-transparent px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95 md:col-span-2"
              style={{ backgroundImage: "var(--q-grad-brand)", backgroundColor: "var(--q-navy)", boxShadow: "0 18px 32px rgba(11, 60, 93, 0.18)" }}
              type="submit"
              disabled={isSaving}
            >
              {isSaving ? "Salvando regras" : "Salvar regras da escola"}
            </button>
          </form>
        </GlassCard>
      </div>
    </div>
  );
}

function QuickLink({ to, label, description }: { to: string; label: string; description: string }) {
  return (
    <Link className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-4 transition hover:bg-[var(--q-surface-2)]" to={to}>
      <div className="font-medium text-[var(--q-text)]">{label}</div>
      <div className="mt-1 text-sm text-[var(--q-text-2)]">{description}</div>
    </Link>
  );
}

function CheckLine({
  label,
  checked,
  onChange,
  wide = false
}: {
  label: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  wide?: boolean;
}) {
  return (
    <label className={`flex items-center gap-3 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] ${wide ? "md:col-span-2" : ""}`.trim()}>
      <input checked={checked} onChange={(event) => onChange(event.target.checked)} type="checkbox" />
      {label}
    </label>
  );
}
