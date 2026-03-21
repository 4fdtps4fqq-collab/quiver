import { type Dispatch, type SetStateAction, useEffect, useMemo, useState } from "react";
import { useSession } from "../auth/SessionContext";
import {
  getAuthenticationAuditEvents,
  getSchoolCurrent,
  updateSchoolSettings,
  type AuthenticationAuditEvent,
  type SchoolCurrentResponse
} from "../lib/auth-api";
import {
  cancelSchoolInvitation,
  createScheduleBlock,
  createInstructor,
  createSchoolInvitation,
  createSchoolUser,
  getInstructors,
  getSchoolInvitations,
  getScheduleBlocks,
  getSchoolUsers,
  resetSchoolUserPassword,
  deleteScheduleBlock,
  type Instructor,
  type InstructorAvailabilitySlot,
  type ScheduleBlock,
  type SchoolInvitation,
  type SchoolUser,
  updateInstructor,
  updateSchoolUser
} from "../lib/platform-api";
import { PageHero } from "../components/PageHero";
import { PermissionMatrix } from "../components/PermissionMatrix";
import { ErrorBlock, GlassCard, LoadingBlock, StatusBadge } from "../components/OperationsUi";
import { getDefaultPermissionsForRole, normalizePermissions, type PlatformPermission } from "../lib/permissions";
import { formatDate } from "../lib/formatters";

const roleOptions = [
  { value: 5, label: "Administrativo", role: "Admin" },
  { value: 3, label: "Instrutor", role: "Instructor" }
] as const;

const roleValueToName = {
  5: "Admin",
  3: "Instructor",
  4: "Student"
} as const;

type SchoolUserRole = (typeof roleValueToName)[keyof typeof roleValueToName];

const initialCreateForm = {
  fullName: "",
  email: "",
  role: "3",
  permissions: getDefaultPermissionsForRole("Instructor"),
  phone: "",
  salaryAmount: "",
  specialties: "",
  availability: defaultAvailability(),
  hourlyRate: "",
  isActive: true,
  mustChangePassword: true
};

const initialEditForm = {
  profileId: "",
  fullName: "",
  role: "3",
  permissions: getDefaultPermissionsForRole("Instructor"),
  phone: "",
  salaryAmount: "",
  specialties: "",
  availability: defaultAvailability(),
  hourlyRate: "",
  isActive: true,
  mustChangePassword: false
};

const initialInvitationForm = {
  fullName: "",
  email: "",
  phone: "",
  role: "5",
  expiresInDays: "7"
};

export function SchoolAdminPage() {
  const { token } = useSession();

  const [school, setSchool] = useState<SchoolCurrentResponse | null>(null);
  const [users, setUsers] = useState<SchoolUser[]>([]);
  const [instructors, setInstructors] = useState<Instructor[]>([]);
  const [scheduleBlocks, setScheduleBlocks] = useState<ScheduleBlock[]>([]);
  const [invitations, setInvitations] = useState<SchoolInvitation[]>([]);
  const [auditEvents, setAuditEvents] = useState<AuthenticationAuditEvent[]>([]);
  const [selectedUserId, setSelectedUserId] = useState("");
  const [availabilityWeekStart, setAvailabilityWeekStart] = useState(() => startOfWeek(new Date()));
  const [availabilityBlockForm, setAvailabilityBlockForm] = useState(() => ({
    date: toDateInputValue(new Date()),
    startTime: "08:00",
    endTime: "18:00",
    title: "Indisponível",
    notes: ""
  }));

  const [createForm, setCreateForm] = useState(initialCreateForm);
  const [editForm, setEditForm] = useState(initialEditForm);
  const [invitationForm, setInvitationForm] = useState(initialInvitationForm);
  const [collaboratorSearch, setCollaboratorSearch] = useState("");
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
  const [isSavingCreate, setIsSavingCreate] = useState(false);
  const [isSavingEdit, setIsSavingEdit] = useState(false);
  const [isResettingPassword, setIsResettingPassword] = useState(false);
  const [isSavingAvailabilityBlock, setIsSavingAvailabilityBlock] = useState(false);
  const [isSavingSettings, setIsSavingSettings] = useState(false);
  const [isSavingInvitation, setIsSavingInvitation] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const collaborators = useMemo(
    () => users.filter((item) => item.role === "Admin" || item.role === "Instructor"),
    [users]
  );
  const selectedUser = collaborators.find((item) => item.identityUserId === selectedUserId) ?? null;
  const selectedInstructor = useMemo(
    () =>
      selectedUser
        ? instructors.find((item) => item.identityUserId === selectedUser.identityUserId) ?? null
        : null,
    [instructors, selectedUser]
  );
  const activeUsers = collaborators.filter((item) => item.isActive).length;
  const activeInstructors = instructors.filter((item) => item.isActive).length;
  const normalizedCollaboratorSearch = collaboratorSearch.trim().toLowerCase();
  const filteredCollaborators = normalizedCollaboratorSearch
    ? collaborators.filter((item) => {
        const roleLabel = translateRole(item.role).toLowerCase();
        const haystack = `${item.fullName} ${item.phone ?? ""} ${roleLabel}`.toLowerCase();
        return haystack.includes(normalizedCollaboratorSearch);
      })
    : collaborators;

  const schoolStats = useMemo(
    () => [
      { label: "Colaboradores ativos", value: String(activeUsers) },
      { label: "Instrutores ativos", value: String(activeInstructors) },
      { label: "Timezone", value: school?.timezone ?? "-" },
      { label: "Moeda", value: school?.currencyCode ?? "-" }
    ],
    [activeInstructors, activeUsers, school?.currencyCode, school?.timezone]
  );
  const availabilityWeekDays = useMemo(() => buildWeekDays(availabilityWeekStart), [availabilityWeekStart]);
  const selectedInstructorBlocks = useMemo(
    () =>
      selectedInstructor
        ? scheduleBlocks
            .filter((item) => item.scope === "Instructor" && item.instructorId === selectedInstructor.id)
            .sort((left, right) => left.startAtUtc.localeCompare(right.startAtUtc))
        : [],
    [scheduleBlocks, selectedInstructor]
  );
  const selectedInstructorBlocksInWeek = useMemo(
    () => selectedInstructorBlocks.filter((item) => isWithinWeek(item.startAtUtc, availabilityWeekStart)),
    [availabilityWeekStart, selectedInstructorBlocks]
  );

  useEffect(() => {
    if (!token) {
      return;
    }
    void loadData(token);
  }, [token]);

  useEffect(() => {
    if (!selectedUser) {
      setEditForm(initialEditForm);
      return;
    }

    const roleOption = roleOptions.find((item) => item.role === selectedUser.role);
    const roleName = resolveRoleName(String(roleOption?.value ?? 3));

    setEditForm({
      profileId: selectedUser.profileId,
      fullName: selectedUser.fullName,
      role: String(roleOption?.value ?? 3),
      permissions: normalizePermissions(selectedUser.permissions ?? getDefaultPermissionsForRole(roleName)),
      phone: selectedUser.phone ?? "",
      salaryAmount: selectedUser.salaryAmount ? formatCurrencyInput(selectedUser.salaryAmount) : "",
      specialties: selectedInstructor?.specialties ?? "",
      availability: selectedInstructor?.availability?.length ? selectedInstructor.availability : defaultAvailability(),
      hourlyRate: selectedInstructor ? formatCurrencyInput(selectedInstructor.hourlyRate) : "",
      isActive: selectedUser.isActive,
      mustChangePassword: selectedUser.mustChangePassword
    });
  }, [selectedInstructor, selectedUser]);

  useEffect(() => {
    setAvailabilityBlockForm((current) => ({
      ...current,
      date: toDateInputValue(availabilityWeekStart)
    }));
  }, [availabilityWeekStart]);

  async function loadData(currentToken: string) {
    try {
      setIsLoading(true);
      setError(null);
      setNotice(null);

      const [schoolData, usersData, instructorsData, scheduleBlocksData, invitationsData, auditData] = await Promise.all([
        getSchoolCurrent(currentToken),
        getSchoolUsers(currentToken),
        getInstructors(currentToken),
        getScheduleBlocks(currentToken),
        getSchoolInvitations(currentToken),
        getAuthenticationAuditEvents(currentToken, 24)
      ]);

      setSchool(schoolData);
      setUsers(usersData);
      setInstructors(instructorsData);
      setScheduleBlocks(scheduleBlocksData);
      setInvitations(invitationsData);
      setAuditEvents(auditData);
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

      const collaboratorList = usersData.filter((item) => item.role === "Admin" || item.role === "Instructor");
      setSelectedUserId((current) =>
        collaboratorList.some((item) => item.identityUserId === current) ? current : ""
      );
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar a administração da escola.");
    } finally {
      setIsLoading(false);
    }
  }

  function resolveRoleName(roleValue: string): SchoolUserRole {
    return roleValueToName[Number(roleValue) as keyof typeof roleValueToName] ?? "Instructor";
  }

  function handleCreateRoleChange(roleValue: string) {
    const roleName = resolveRoleName(roleValue);
    setCreateForm((current) => ({
      ...current,
      role: roleValue,
      permissions: getDefaultPermissionsForRole(roleName)
    }));
  }

  function handleEditRoleChange(roleValue: string) {
    const roleName = resolveRoleName(roleValue);
    setEditForm((current) => ({
      ...current,
      role: roleValue,
      permissions: getDefaultPermissionsForRole(roleName)
    }));
  }

  async function handleCreateUser() {
    if (!token) {
      return;
    }

    try {
      setIsSavingCreate(true);
      setError(null);
      setNotice(null);
      const createdUser = await createSchoolUser(token, {
        fullName: createForm.fullName,
        email: createForm.email,
        role: Number(createForm.role),
        permissions: createForm.permissions,
        phone: createForm.phone || undefined,
        salaryAmount: resolveRoleName(createForm.role) === "Admin" ? parseCurrencyInput(createForm.salaryAmount) : null,
        isActive: createForm.isActive,
        mustChangePassword: createForm.mustChangePassword
      });

      if (resolveRoleName(createForm.role) === "Instructor") {
        await createInstructor(token, {
          fullName: createForm.fullName,
          email: createForm.email || undefined,
          phone: createForm.phone || undefined,
          specialties: createForm.specialties || undefined,
          availability: createForm.availability,
          hourlyRate: parseCurrencyInput(createForm.hourlyRate),
          identityUserId: createdUser.identityUserId
        });
      }

      setNotice(
        createdUser.deliveryMode === "File" && createdUser.outboxFilePath
          ? `Colaborador criado e senha temporária salva no outbox local em ${createdUser.outboxFilePath}.`
          : "Colaborador criado e senha temporária enviada por e-mail."
      );
      setCreateForm(initialCreateForm);
      setSelectedUserId("");
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível criar o colaborador.");
    } finally {
      setIsSavingCreate(false);
    }
  }

  async function handleUpdateUser() {
    if (!token || !selectedUser) {
      return;
    }

    try {
      setIsSavingEdit(true);
      setError(null);
      await updateSchoolUser(token, selectedUser.identityUserId, {
        profileId: editForm.profileId,
        fullName: editForm.fullName,
        role: Number(editForm.role),
        permissions: editForm.permissions,
        phone: editForm.phone || undefined,
        salaryAmount: resolveRoleName(editForm.role) === "Admin" ? parseCurrencyInput(editForm.salaryAmount) : null,
        isActive: editForm.isActive,
        mustChangePassword: editForm.mustChangePassword
      });

      const nextRole = resolveRoleName(editForm.role);
      const linkedInstructor = instructors.find((item) => item.identityUserId === selectedUser.identityUserId) ?? null;

      if (nextRole === "Instructor") {
        if (linkedInstructor) {
          await updateInstructor(token, linkedInstructor.id, {
            fullName: editForm.fullName,
            email: selectedUser.email || undefined,
            phone: editForm.phone || undefined,
            specialties: editForm.specialties || undefined,
            availability: editForm.availability,
            hourlyRate: parseCurrencyInput(editForm.hourlyRate),
            identityUserId: selectedUser.identityUserId,
            isActive: editForm.isActive
          });
        } else {
          await createInstructor(token, {
            fullName: editForm.fullName,
            email: selectedUser.email || undefined,
            phone: editForm.phone || undefined,
            specialties: editForm.specialties || undefined,
            availability: editForm.availability,
            hourlyRate: parseCurrencyInput(editForm.hourlyRate),
            identityUserId: selectedUser.identityUserId
          });
        }
      } else if (linkedInstructor) {
        await updateInstructor(token, linkedInstructor.id, {
          fullName: linkedInstructor.fullName,
          email: linkedInstructor.email || undefined,
          phone: linkedInstructor.phone || undefined,
          specialties: linkedInstructor.specialties || undefined,
          availability: linkedInstructor.availability,
          hourlyRate: linkedInstructor.hourlyRate,
          identityUserId: selectedUser.identityUserId,
          isActive: false
        });
      }

      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível atualizar o colaborador.");
    } finally {
      setIsSavingEdit(false);
    }
  }

  async function handleSubmitCollaborator(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (selectedUser) {
      await handleUpdateUser();
      return;
    }

    await handleCreateUser();
  }

  function handleEditCollaborator(user: SchoolUser) {
    setSelectedUserId(user.identityUserId);
    setError(null);
  }

  function handleClearCollaboratorForm() {
    setSelectedUserId("");
    setCreateForm(initialCreateForm);
    setEditForm(initialEditForm);
    setError(null);
    setNotice(null);
  }

  async function handleResetCollaboratorPassword() {
    if (!token || !selectedUser) {
      return;
    }

    try {
      setIsResettingPassword(true);
      setError(null);
      setNotice(null);

      const result = await resetSchoolUserPassword(token, selectedUser.identityUserId, {
        deliverByEmail: true,
        email: selectedUser.email ?? undefined,
        fullName: editForm.fullName
      });

      setNotice(
        result.deliveryMode === "File" && result.outboxFilePath
          ? `Nova senha temporária do colaborador salva no outbox local em ${result.outboxFilePath}.`
          : "Nova senha temporária enviada por e-mail com sucesso."
      );

      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível resetar a senha do colaborador.");
    } finally {
      setIsResettingPassword(false);
    }
  }

  async function handleCreateAvailabilityBlock() {
    if (!token || !selectedInstructor) {
      return;
    }

    try {
      setIsSavingAvailabilityBlock(true);
      setError(null);
      setNotice(null);

      const startAtUtc = combineDateAndTimeToUtc(
        availabilityBlockForm.date,
        availabilityBlockForm.startTime
      );
      const endAtUtc = combineDateAndTimeToUtc(
        availabilityBlockForm.date,
        availabilityBlockForm.endTime
      );

      if (!startAtUtc || !endAtUtc || endAtUtc <= startAtUtc) {
        setError("Defina uma data e um intervalo válido para o bloqueio do instrutor.");
        return;
      }

      await createScheduleBlock(token, {
        scope: 2,
        instructorId: selectedInstructor.id,
        title: availabilityBlockForm.title.trim() || "Indisponível",
        notes: availabilityBlockForm.notes.trim() || undefined,
        startAtUtc: startAtUtc.toISOString(),
        endAtUtc: endAtUtc.toISOString()
      });

      setNotice("Bloqueio semanal do instrutor salvo com sucesso.");
      setAvailabilityBlockForm((current) => ({ ...current, notes: "" }));
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar o bloqueio do instrutor.");
    } finally {
      setIsSavingAvailabilityBlock(false);
    }
  }

  async function handleDeleteAvailabilityBlock(blockId: string) {
    if (!token) {
      return;
    }

    try {
      setError(null);
      setNotice(null);
      await deleteScheduleBlock(token, blockId);
      setNotice("Bloqueio removido da agenda do instrutor.");
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível remover o bloqueio do instrutor.");
    }
  }

  async function handleCreateInvitation(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token || !school) {
      return;
    }

    try {
      setIsSavingInvitation(true);
      setError(null);
      setNotice(null);
      const createdInvitation = await createSchoolInvitation(token, {
        fullName: invitationForm.fullName,
        email: invitationForm.email,
        phone: invitationForm.phone || undefined,
        role: Number(invitationForm.role),
        expiresInDays: Number(invitationForm.expiresInDays),
        schoolDisplayName: school.displayName,
        schoolSlug: school.slug
      });

      setInvitationForm(initialInvitationForm);
      setNotice(
        createdInvitation.deliveryMode === "File" && createdInvitation.outboxFilePath
          ? `Convite gerado e salvo no outbox local em ${createdInvitation.outboxFilePath}.`
          : "Convite enviado com sucesso."
      );
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível enviar o convite.");
    } finally {
      setIsSavingInvitation(false);
    }
  }

  async function handleCancelInvitation(invitationId: string) {
    if (!token) {
      return;
    }

    try {
      setError(null);
      setNotice(null);
      await cancelSchoolInvitation(token, invitationId);
      setNotice("Convite cancelado com sucesso.");
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível cancelar o convite.");
    }
  }

  async function handleUpdateSettings(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSavingSettings(true);
      setError(null);
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
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar as regras da escola.");
    } finally {
      setIsSavingSettings(false);
    }
  }

  return (
    <div className="space-y-6">
      <PageHero
        title="Escola e equipe em um único lugar."
        stats={schoolStats}
        statsBelow
      />

      {isLoading ? <LoadingBlock label="Carregando administração da escola" /> : null}
      {error ? <ErrorBlock message={error} /> : null}
      {notice ? (
        <div className="rounded-[24px] border border-[var(--q-info)]/30 bg-[var(--q-info-bg)] px-5 py-4 text-sm text-[var(--q-info)]">
          {notice}
        </div>
      ) : null}

      <div className="grid gap-4 xl:grid-cols-[0.92fr_1.08fr]">
        <GlassCard title="Resumo da escola" description="Dados principais e parâmetros ativos da operação.">
          {school ? (
            <div className="space-y-4">
              <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                <div className="text-lg font-semibold text-[var(--q-text)]">{school.displayName}</div>
                <div className="mt-2 text-sm text-[var(--q-text-2)]">{school.legalName}</div>
                <div className="mt-2 text-xs uppercase tracking-[0.2em] text-[var(--q-muted)]">
                  Identificador: {school.slug || "-"}
                </div>
                <div className="mt-3 flex flex-wrap gap-2">
                  <StatusBadge value={school.status} />
                </div>
              </div>

              <div className="grid gap-3 md:grid-cols-2">
                <MetricTile label="Antecedência para agendar" value={`${settingsForm.bookingLeadTimeMinutes} min`} />
                <MetricTile label="Cancelamento" value={`${settingsForm.cancellationWindowHours} h`} />
                <MetricTile label="Remarcação" value={`${settingsForm.rescheduleWindowHours} h`} />
                <MetricTile
                  label="Confirmação de presença"
                  value={`${settingsForm.attendanceConfirmationLeadMinutes} min`}
                />
                <MetricTile label="Buffer do instrutor" value={`${settingsForm.instructorBufferMinutes} min`} />
                <MetricTile label="Tolerância no-show" value={`${settingsForm.noShowGraceMinutes} min`} />
              </div>
            </div>
          ) : null}
        </GlassCard>

        <GlassCard
          title="Regras operacionais e do portal"
          description="Ajuste agenda, no-show, automação financeira e a experiência do aluno."
        >
          <form className="grid gap-3 md:grid-cols-2" onSubmit={handleUpdateSettings}>
            <NumericField
              label="Antecedência mínima para agendar em minutos"
              value={settingsForm.bookingLeadTimeMinutes}
              onChange={(value) => setSettingsForm((current) => ({ ...current, bookingLeadTimeMinutes: value }))}
            />
            <NumericField
              label="Janela para cancelar em horas"
              value={settingsForm.cancellationWindowHours}
              onChange={(value) => setSettingsForm((current) => ({ ...current, cancellationWindowHours: value }))}
            />
            <NumericField
              label="Janela para remarcar em horas"
              value={settingsForm.rescheduleWindowHours}
              onChange={(value) => setSettingsForm((current) => ({ ...current, rescheduleWindowHours: value }))}
            />
            <NumericField
              label="Antecedência para confirmar presença em minutos"
              value={settingsForm.attendanceConfirmationLeadMinutes}
              onChange={(value) =>
                setSettingsForm((current) => ({ ...current, attendanceConfirmationLeadMinutes: value }))
              }
            />
            <NumericField
              label="Lembrete de aula em horas"
              value={settingsForm.lessonReminderLeadHours}
              onChange={(value) => setSettingsForm((current) => ({ ...current, lessonReminderLeadHours: value }))}
            />
            <NumericField
              label="Buffer entre aulas do instrutor em minutos"
              value={settingsForm.instructorBufferMinutes}
              onChange={(value) => setSettingsForm((current) => ({ ...current, instructorBufferMinutes: value }))}
            />
            <NumericField
              label="Tolerância para no-show em minutos"
              value={settingsForm.noShowGraceMinutes}
              onChange={(value) => setSettingsForm((current) => ({ ...current, noShowGraceMinutes: value }))}
            />
            <TextField
              label="Cor primária"
              value={settingsForm.themePrimary}
              onChange={(value) => setSettingsForm((current) => ({ ...current, themePrimary: value }))}
            />
            <TextField
              label="Cor de destaque"
              value={settingsForm.themeAccent}
              onChange={(value) => setSettingsForm((current) => ({ ...current, themeAccent: value }))}
            />

            <label className="flex items-center gap-3 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] md:col-span-2">
              <input
                checked={settingsForm.portalNotificationsEnabled}
                onChange={(event) =>
                  setSettingsForm((current) => ({ ...current, portalNotificationsEnabled: event.target.checked }))
                }
                type="checkbox"
              />
              Manter notificações do portal do aluno ativas
            </label>
            <label className="flex items-center gap-3 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)]">
              <input
                checked={settingsForm.noShowConsumesCourseMinutes}
                onChange={(event) =>
                  setSettingsForm((current) => ({ ...current, noShowConsumesCourseMinutes: event.target.checked }))
                }
                type="checkbox"
              />
              No-show consome saldo horário das matrículas
            </label>
            <label className="flex items-center gap-3 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)]">
              <input
                checked={settingsForm.noShowChargesSingleLesson}
                onChange={(event) =>
                  setSettingsForm((current) => ({ ...current, noShowChargesSingleLesson: event.target.checked }))
                }
                type="checkbox"
              />
              No-show cobra aula avulsa
            </label>
            <label className="flex items-center gap-3 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)]">
              <input
                checked={settingsForm.autoCreateEnrollmentRevenue}
                onChange={(event) =>
                  setSettingsForm((current) => ({ ...current, autoCreateEnrollmentRevenue: event.target.checked }))
                }
                type="checkbox"
              />
              Gerar receita automática na matrícula
            </label>
            <label className="flex items-center gap-3 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)]">
              <input
                checked={settingsForm.autoCreateSingleLessonRevenue}
                onChange={(event) =>
                  setSettingsForm((current) => ({ ...current, autoCreateSingleLessonRevenue: event.target.checked }))
                }
                type="checkbox"
              />
              Gerar receita automática na aula avulsa
            </label>

            <button
              className="rounded-full border border-transparent px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95 md:col-span-2"
              style={{
                backgroundImage: "var(--q-grad-brand)",
                backgroundColor: "var(--q-navy)",
                boxShadow: "0 18px 32px rgba(11, 60, 93, 0.18)"
              }}
              type="submit"
              disabled={isSavingSettings}
            >
              {isSavingSettings ? "Salvando regras" : "Salvar regras do portal"}
            </button>
          </form>
        </GlassCard>
      </div>

      <div className="grid gap-4 xl:grid-cols-[0.92fr_1.08fr]">
        <GlassCard
          title="Ficha de colaborador"
          description={selectedUser ? "Ajuste os dados principais do colaborador selecionado." : undefined}
        >
          <form className="space-y-6" onSubmit={handleSubmitCollaborator}>
            <div className="grid gap-6 md:grid-cols-2">
              <TextField
                label="Nome completo"
                value={selectedUser ? editForm.fullName : createForm.fullName}
                onChange={(value) =>
                  selectedUser
                    ? setEditForm((current) => ({ ...current, fullName: value }))
                    : setCreateForm((current) => ({ ...current, fullName: value }))
                }
                required
              />
              <TextField
                label="Telefone"
                value={selectedUser ? editForm.phone : createForm.phone}
                onChange={(value) =>
                  selectedUser
                    ? setEditForm((current) => ({ ...current, phone: formatPhone(value) }))
                    : setCreateForm((current) => ({ ...current, phone: formatPhone(value) }))
                }
                placeholder="(00) 00000-0000"
              />
            </div>

            {selectedUser ? (
              <div className="space-y-3">
                <TextField label="E-mail de acesso" value={selectedUser.email ?? ""} disabled />
                <div className="flex flex-wrap gap-3">
                  <button
                    className="rounded-full border border-[var(--q-info)]/25 bg-[var(--q-info-bg)] px-5 py-3 text-sm font-medium uppercase tracking-[0.22em] text-[var(--q-info)] transition hover:opacity-90"
                    type="button"
                    onClick={() => void handleResetCollaboratorPassword()}
                    disabled={isResettingPassword}
                  >
                    {isResettingPassword ? "Resetando senha" : "Resetar senha"}
                  </button>
                  <div className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)]">
                    Gera uma nova senha temporária e envia por e-mail ao colaborador.
                  </div>
                </div>
              </div>
            ) : (
              <TextField
                label="E-mail de acesso"
                type="email"
                value={createForm.email}
                onChange={(value) => setCreateForm((current) => ({ ...current, email: value }))}
                required
              />
            )}

            {!selectedUser ? (
              <div className="grid gap-6 md:grid-cols-2">
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Função</span>
                  <select
                    className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                    value={createForm.role}
                    onChange={(event) => handleCreateRoleChange(event.target.value)}
                  >
                    {roleOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </label>
                <div className="rounded-[22px] border border-[var(--q-info)]/25 bg-[var(--q-info-bg)] px-4 py-3 text-sm text-[var(--q-info)]">
                  O sistema gera uma senha temporária automaticamente e envia o acesso por e-mail.
                </div>
              </div>
            ) : (
              <label className="grid max-w-[320px] gap-2 text-sm text-[var(--q-text)]">
                <span>Função</span>
                <select
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  value={editForm.role}
                  onChange={(event) => handleEditRoleChange(event.target.value)}
                >
                  {roleOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </label>
            )}

            {resolveRoleName(selectedUser ? editForm.role : createForm.role) === "Instructor" ? (
              <div className="grid gap-6 md:grid-cols-2">
                <TextField
                  label="Valor da hora/aula"
                  value={selectedUser ? editForm.hourlyRate : createForm.hourlyRate}
                  onChange={(value) =>
                    selectedUser
                      ? setEditForm((current) => ({ ...current, hourlyRate: formatCurrencyMask(value) }))
                      : setCreateForm((current) => ({ ...current, hourlyRate: formatCurrencyMask(value) }))
                  }
                  required
                  placeholder="0,00"
                />
                <TextField
                  label="Especialidades"
                  value={selectedUser ? editForm.specialties : createForm.specialties}
                  onChange={(value) =>
                    selectedUser
                      ? setEditForm((current) => ({ ...current, specialties: value }))
                      : setCreateForm((current) => ({ ...current, specialties: value }))
                  }
                />
              </div>
            ) : null}

            {resolveRoleName(selectedUser ? editForm.role : createForm.role) === "Admin" ? (
              <div className="grid gap-6 md:max-w-[320px]">
                <TextField
                  label="Salário"
                  value={selectedUser ? editForm.salaryAmount : createForm.salaryAmount}
                  onChange={(value) =>
                    selectedUser
                      ? setEditForm((current) => ({ ...current, salaryAmount: formatCurrencyMask(value) }))
                      : setCreateForm((current) => ({ ...current, salaryAmount: formatCurrencyMask(value) }))
                  }
                  required
                  placeholder="0,00"
                />
              </div>
            ) : null}

            {resolveRoleName(selectedUser ? editForm.role : createForm.role) === "Instructor" ? (
              <div className="space-y-4 rounded-[24px] border border-[var(--q-border)] bg-[var(--q-surface)] p-4">
                <div className="space-y-1">
                  <div className="text-sm font-medium text-[var(--q-text)]">Agenda semanal do instrutor</div>
                  <div className="text-sm text-[var(--q-text-2)]">
                    Em vez de assumir a mesma terça ou quinta em todas as semanas, use a jornada base só como referência e ajuste a agenda real por data.
                  </div>
                </div>

                <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                  <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <div className="text-sm font-medium text-[var(--q-text)]">Jornada base do instrutor</div>
                      <div className="text-xs text-[var(--q-text-2)]">
                        Referência padrão para sugestões e validação da agenda.
                      </div>
                    </div>
                    <div className="text-xs uppercase tracking-[0.2em] text-[var(--q-muted)]">
                      Modelo recorrente
                    </div>
                  </div>

                  <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                    {(selectedUser ? editForm.availability : createForm.availability).map((slot, index) => (
                      <div
                        key={`${slot.dayOfWeek}-${index}`}
                        className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface)] p-4"
                      >
                        <div className="mb-3 text-sm font-medium text-[var(--q-text)]">{slot.label}</div>
                        <div className="grid gap-3 sm:grid-cols-2">
                          <TimeField
                            label="Início"
                            value={minutesToTime(slot.startMinutesUtc)}
                            onChange={(value) =>
                              updateAvailabilityRow(
                                selectedUser,
                                index,
                                "startMinutesUtc",
                                timeToMinutes(value),
                                setCreateForm,
                                setEditForm
                              )
                            }
                          />
                          <TimeField
                            label="Fim"
                            value={minutesToTime(slot.endMinutesUtc)}
                            onChange={(value) =>
                              updateAvailabilityRow(
                                selectedUser,
                                index,
                                "endMinutesUtc",
                                timeToMinutes(value),
                                setCreateForm,
                                setEditForm
                              )
                            }
                          />
                        </div>
                      </div>
                    ))}
                  </div>
                </div>

                <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                  <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <div className="text-sm font-medium text-[var(--q-text)]">Semana operacional</div>
                      <div className="text-xs text-[var(--q-text-2)]">
                        Navegue por semana e bloqueie indisponibilidades reais do instrutor, como em uma agenda.
                      </div>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      <button
                        className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-2 text-xs uppercase tracking-[0.2em] text-[var(--q-text)]"
                        type="button"
                        onClick={() => setAvailabilityWeekStart((current) => addDays(current, -7))}
                      >
                        Semana anterior
                      </button>
                      <button
                        className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-2 text-xs uppercase tracking-[0.2em] text-[var(--q-text)]"
                        type="button"
                        onClick={() => setAvailabilityWeekStart(startOfWeek(new Date()))}
                      >
                        Semana atual
                      </button>
                      <button
                        className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-2 text-xs uppercase tracking-[0.2em] text-[var(--q-text)]"
                        type="button"
                        onClick={() => setAvailabilityWeekStart((current) => addDays(current, 7))}
                      >
                        Próxima semana
                      </button>
                    </div>
                  </div>

                  <div className="mb-4 text-sm font-medium text-[var(--q-text)]">
                    {formatWeekRangeLabel(availabilityWeekStart)}
                  </div>

                  {selectedInstructor ? (
                    <div className="space-y-4">
                      <div className="grid gap-3 xl:grid-cols-[1.2fr_0.8fr]">
                        <div className="overflow-x-auto rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface)] p-3">
                          <div className="grid min-w-[780px] grid-cols-7 gap-3">
                            {availabilityWeekDays.map((day) => (
                              <div
                                key={day.dateKey}
                                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] p-3"
                              >
                                <div className="mb-3 border-b border-[var(--q-border)] pb-3">
                                  <div className="text-xs uppercase tracking-[0.2em] text-[var(--q-muted)]">
                                    {day.weekdayLabel}
                                  </div>
                                  <div className="mt-1 text-sm font-medium text-[var(--q-text)]">
                                    {day.dateLabel}
                                  </div>
                                  <div className="mt-2 rounded-xl bg-[var(--q-success-bg)] px-3 py-2 text-xs text-[var(--q-success)]">
                                    Base: {describeBaseAvailabilityForDay(selectedUser ? editForm.availability : createForm.availability, day.date)}
                                  </div>
                                </div>

                                <div className="space-y-2">
                                  {selectedInstructorBlocksInWeek.filter((block) => isSameLocalDay(block.startAtUtc, day.date)).length === 0 ? (
                                    <div className="rounded-xl border border-dashed border-[var(--q-divider)] px-3 py-4 text-xs text-[var(--q-text-2)]">
                                      Sem bloqueios nesta data.
                                    </div>
                                  ) : (
                                    selectedInstructorBlocksInWeek
                                      .filter((block) => isSameLocalDay(block.startAtUtc, day.date))
                                      .map((block) => (
                                        <div
                                          key={block.id}
                                          className="rounded-xl border border-[var(--q-danger)]/20 bg-[var(--q-danger-bg)] px-3 py-3"
                                        >
                                          <div className="text-xs uppercase tracking-[0.2em] text-[var(--q-danger)]">
                                            Bloqueado
                                          </div>
                                          <div className="mt-1 text-sm font-medium text-[var(--q-text)]">
                                            {formatBlockWindow(block.startAtUtc, block.endAtUtc)}
                                          </div>
                                          <div className="mt-1 text-xs text-[var(--q-text-2)]">{block.title}</div>
                                          {block.notes ? (
                                            <div className="mt-1 text-xs text-[var(--q-text-2)]">{block.notes}</div>
                                          ) : null}
                                          <button
                                            className="mt-3 rounded-full border border-[var(--q-danger)]/25 bg-white px-3 py-1.5 text-[11px] uppercase tracking-[0.2em] text-[var(--q-danger)]"
                                            type="button"
                                            onClick={() => void handleDeleteAvailabilityBlock(block.id)}
                                          >
                                            Remover
                                          </button>
                                        </div>
                                      ))
                                  )}
                                </div>
                              </div>
                            ))}
                          </div>
                        </div>

                        <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface)] p-4">
                          <div className="text-sm font-medium text-[var(--q-text)]">Bloquear horário da semana</div>
                          <div className="mt-1 text-xs text-[var(--q-text-2)]">
                            Use este bloco para férias, compromissos, vento ruim, manutenção pessoal ou qualquer indisponibilidade pontual.
                          </div>

                          <div className="mt-4 grid gap-3">
                            <TextField
                              label="Data"
                              type="date"
                              value={availabilityBlockForm.date}
                              onChange={(value) =>
                                setAvailabilityBlockForm((current) => ({ ...current, date: value }))
                              }
                            />
                            <div className="grid gap-3 sm:grid-cols-2">
                              <TimeField
                                label="Início"
                                value={availabilityBlockForm.startTime}
                                onChange={(value) =>
                                  setAvailabilityBlockForm((current) => ({ ...current, startTime: value }))
                                }
                              />
                              <TimeField
                                label="Fim"
                                value={availabilityBlockForm.endTime}
                                onChange={(value) =>
                                  setAvailabilityBlockForm((current) => ({ ...current, endTime: value }))
                                }
                              />
                            </div>
                            <TextField
                              label="Título"
                              value={availabilityBlockForm.title}
                              onChange={(value) =>
                                setAvailabilityBlockForm((current) => ({ ...current, title: value }))
                              }
                              placeholder="Ex.: Atendimento externo"
                            />
                            <TextField
                              label="Observação"
                              value={availabilityBlockForm.notes}
                              onChange={(value) =>
                                setAvailabilityBlockForm((current) => ({ ...current, notes: value }))
                              }
                              placeholder="Motivo do bloqueio"
                            />
                            <button
                              className="rounded-full border border-transparent px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white shadow-[0_16px_34px_rgba(18,84,135,0.18)] transition hover:opacity-95"
                              style={{ backgroundImage: "var(--q-grad-brand)", backgroundColor: "var(--q-navy)" }}
                              type="button"
                              onClick={() => void handleCreateAvailabilityBlock()}
                              disabled={isSavingAvailabilityBlock}
                            >
                              {isSavingAvailabilityBlock ? "Salvando" : "Bloquear horário"}
                            </button>
                          </div>
                        </div>
                      </div>
                    </div>
                  ) : (
                    <div className="rounded-2xl border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
                      Salve o colaborador primeiro para abrir a agenda semanal real do instrutor.
                    </div>
                  )}
                </div>
              </div>
            ) : null}

            <div>
              <PermissionMatrix
                role={resolveRoleName(selectedUser ? editForm.role : createForm.role)}
                value={selectedUser ? editForm.permissions : createForm.permissions}
                onChange={(permissions) =>
                  selectedUser
                    ? setEditForm((current) => ({ ...current, permissions: permissions as PlatformPermission[] }))
                    : setCreateForm((current) => ({
                        ...current,
                        permissions: permissions as PlatformPermission[]
                      }))
                }
              />
            </div>

            <div className="grid gap-3 md:grid-cols-2">
              <label className="flex items-center gap-3 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)]">
                <input
                  checked={selectedUser ? editForm.isActive : createForm.isActive}
                  onChange={(event) =>
                    selectedUser
                      ? setEditForm((current) => ({ ...current, isActive: event.target.checked }))
                      : setCreateForm((current) => ({ ...current, isActive: event.target.checked }))
                  }
                  type="checkbox"
                />
                Conta ativa
              </label>

              <div className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)]">
                A troca de senha é obrigatória no primeiro acesso e sempre após o reset.
              </div>
            </div>

            <div className="flex flex-wrap gap-3">
              <button
                className="mt-1 inline-flex items-center justify-center rounded-full border border-transparent px-5 py-3.5 text-sm font-medium uppercase tracking-[0.24em] text-white shadow-[0_16px_34px_rgba(18,84,135,0.18)] transition hover:opacity-95"
                style={{ backgroundImage: "var(--q-grad-brand)", backgroundColor: "var(--q-navy)" }}
                type="submit"
                disabled={selectedUser ? isSavingEdit : isSavingCreate}
              >
                {selectedUser ? (isSavingEdit ? "Salvando" : "Salvar") : isSavingCreate ? "Salvando" : "Salvar"}
              </button>
              <button
                className="mt-1 rounded-full border border-[var(--q-border)] bg-[var(--q-surface)] px-5 py-3.5 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-text)] transition hover:bg-[var(--q-surface-2)]"
                type="button"
                onClick={handleClearCollaboratorForm}
              >
                Limpar
              </button>
            </div>
          </form>
        </GlassCard>

        <GlassCard title="Base de colaboradores">
          <div className="mb-4">
            <input
              className="w-full rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)] outline-none"
              placeholder="Buscar por nome, telefone ou função"
              value={collaboratorSearch}
              onChange={(event) => setCollaboratorSearch(event.target.value)}
            />
          </div>
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
              <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                <tr>
                  <th className="pb-4 pr-6">Colaborador</th>
                  <th className="pb-4 pr-6">Função</th>
                  <th className="pb-4 pr-6">Status</th>
                  <th className="pb-4">Ação</th>
                </tr>
              </thead>
              <tbody>
                {filteredCollaborators.map((user) => (
                  <tr key={user.identityUserId} className="border-t border-[var(--q-border)]">
                    <td className="py-4 pr-6 align-middle">
                      <div className="font-medium text-[var(--q-text)]">{user.fullName}</div>
                    </td>
                    <td className="py-4 pr-6 align-middle">
                      <div className="font-medium text-[var(--q-text)]">{translateRole(user.role)}</div>
                    </td>
                    <td className="py-4 pr-6 align-middle">
                      <div className="font-medium text-[var(--q-text)]">{user.isActive ? "Ativo" : "Inativo"}</div>
                    </td>
                    <td className="py-4 align-middle">
                      <button
                        className="rounded-full border border-[var(--q-info)]/25 bg-[var(--q-info-bg)] px-4 py-2.5 text-sm font-medium text-[var(--q-info)] transition hover:opacity-90"
                        type="button"
                        onClick={() => handleEditCollaborator(user)}
                      >
                        Editar
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {filteredCollaborators.length === 0 ? (
            <div className="mt-4 rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
              Nenhum colaborador encontrado para esta busca.
            </div>
          ) : null}
        </GlassCard>
      </div>

      <div className="grid gap-4 xl:grid-cols-[0.9fr_1.1fr]">
        <GlassCard
          title="Convites por e-mail"
          description="Use convites para onboarding guiado de administrativos e instrutores, com senha criada pela própria pessoa."
        >
          <form className="space-y-4" onSubmit={handleCreateInvitation}>
            <div className="grid gap-4 md:grid-cols-2">
              <TextField
                label="Nome completo"
                value={invitationForm.fullName}
                onChange={(value) => setInvitationForm((current) => ({ ...current, fullName: value }))}
                required
              />
              <TextField
                label="Telefone"
                value={invitationForm.phone}
                onChange={(value) => setInvitationForm((current) => ({ ...current, phone: formatPhone(value) }))}
                placeholder="(00) 00000-0000"
              />
            </div>

            <div className="grid gap-4 md:grid-cols-[1.1fr_0.9fr]">
              <TextField
                label="E-mail do convite"
                type="email"
                value={invitationForm.email}
                onChange={(value) => setInvitationForm((current) => ({ ...current, email: value }))}
                required
              />
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Função</span>
                <select
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  value={invitationForm.role}
                  onChange={(event) => setInvitationForm((current) => ({ ...current, role: event.target.value }))}
                >
                  {roleOptions.map((option) => (
                    <option key={`invite-${option.value}`} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            <div className="grid gap-4 md:max-w-[240px]">
              <TextField
                label="Validade em dias"
                value={invitationForm.expiresInDays}
                onChange={(value) => setInvitationForm((current) => ({ ...current, expiresInDays: value }))}
              />
            </div>

            <button
              className="rounded-full border border-transparent px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95"
              style={{
                backgroundImage: "var(--q-grad-brand)",
                backgroundColor: "var(--q-navy)",
                boxShadow: "0 18px 32px rgba(11, 60, 93, 0.18)"
              }}
              type="submit"
              disabled={isSavingInvitation}
            >
              {isSavingInvitation ? "Enviando convite" : "Enviar convite"}
            </button>
          </form>
        </GlassCard>

        <GlassCard
          title="Auditoria de autenticação e acessos"
          description="Últimos eventos de login, convites, redefinição de senha e movimentação de contas da escola."
        >
          <div className="space-y-3">
            <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text-2)]">
              MFA futuro: a base de auditoria, recuperação por e-mail e governança de contas já está pronta para a próxima etapa de múltiplos fatores.
            </div>

            <div className="space-y-3">
              {auditEvents.length === 0 ? (
                <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
                  Ainda não há eventos registrados para esta escola.
                </div>
              ) : (
                auditEvents.map((event) => (
                  <div
                    key={event.id}
                    className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-4"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div className="text-sm font-semibold text-[var(--q-text)]">
                        {translateAuditEvent(event.eventType)}
                      </div>
                      <StatusBadge value={event.outcome} />
                    </div>
                    <div className="mt-2 text-sm text-[var(--q-text-2)]">
                      {event.email || "Conta interna"} • {new Date(event.createdAtUtc).toLocaleString("pt-BR")}
                    </div>
                    {event.metadata ? (
                      <div className="mt-2 text-xs leading-5 text-[var(--q-muted)]">
                        {summarizeAuditMetadata(event.metadata)}
                      </div>
                    ) : null}
                  </div>
                ))
              )}
            </div>
          </div>
        </GlassCard>
      </div>

      <GlassCard title="Convites recentes" description="Monitore o status dos convites ativos e cancele quando necessário.">
        <div className="overflow-x-auto">
          <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
            <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
              <tr>
                <th className="pb-4 pr-6">Pessoa</th>
                <th className="pb-4 pr-6">Função</th>
                <th className="pb-4 pr-6">Status</th>
                <th className="pb-4 pr-6">Entrega</th>
                <th className="pb-4">Ação</th>
              </tr>
            </thead>
            <tbody>
              {invitations.map((invitation) => (
                <tr key={invitation.id} className="border-t border-[var(--q-border)]">
                  <td className="py-4 pr-6 align-middle">
                    <div className="font-medium text-[var(--q-text)]">{invitation.fullName}</div>
                    <div className="mt-1 text-xs text-[var(--q-muted)]">{invitation.email}</div>
                  </td>
                  <td className="py-4 pr-6 align-middle">
                    <div className="font-medium text-[var(--q-text)]">{translateRole(invitation.role)}</div>
                  </td>
                  <td className="py-4 pr-6 align-middle">
                    <StatusBadge value={invitation.status} />
                  </td>
                  <td className="py-4 pr-6 align-middle">
                    <div className="font-medium text-[var(--q-text)]">{invitation.deliveryMode ?? "-"}</div>
                    {invitation.outboxFilePath ? (
                      <div className="mt-1 text-xs text-[var(--q-muted)]">{invitation.outboxFilePath}</div>
                    ) : null}
                  </td>
                  <td className="py-4 align-middle">
                    {invitation.status === "Pending" ? (
                      <button
                        className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-2.5 text-sm font-medium text-[var(--q-text)] transition hover:bg-[var(--q-surface-2)]"
                        type="button"
                        onClick={() => void handleCancelInvitation(invitation.id)}
                      >
                        Cancelar
                      </button>
                    ) : (
                      <span className="text-sm text-[var(--q-muted)]">Sem ação</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {invitations.length === 0 ? (
          <div className="mt-4 rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
            Ainda não há convites enviados por esta escola.
          </div>
        ) : null}
      </GlassCard>
    </div>
  );
}

function MetricTile({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface)] p-4">
      <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">{label}</div>
      <div className="mt-3 text-xl font-semibold text-[var(--q-text)]">{value}</div>
    </div>
  );
}

function TextField({
  label,
  value,
  onChange,
  type = "text",
  required = false,
  disabled = false,
  className,
  placeholder
}: {
  label: string;
  value: string;
  onChange?: (value: string) => void;
  type?: string;
  required?: boolean;
  disabled?: boolean;
  className?: string;
  placeholder?: string;
}) {
  return (
    <label className={`grid gap-2 text-sm text-[var(--q-text)] ${className ?? ""}`.trim()}>
      <span>{label}</span>
      <input
        className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none disabled:cursor-not-allowed disabled:opacity-80"
        value={value}
        onChange={onChange ? (event) => onChange(event.target.value) : undefined}
        type={type}
        required={required}
        disabled={disabled}
        placeholder={placeholder}
      />
    </label>
  );
}

function NumericField({
  label,
  value,
  onChange
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return <TextField label={label} value={value} onChange={onChange} />;
}

function TimeField({
  label,
  value,
  onChange
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="grid gap-2 text-sm text-[var(--q-text)]">
      <span>{label}</span>
      <input
        className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
        type="time"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}

function formatCurrencyInput(value: number) {
  return value.toFixed(2).replace(".", ",");
}

function formatCurrencyMask(value: string) {
  const digits = value.replace(/\D/g, "");
  if (!digits) {
    return "";
  }

  const integer = digits.slice(0, -2) || "0";
  const cents = digits.slice(-2).padStart(2, "0");
  const normalizedInteger = Number(integer).toLocaleString("pt-BR");
  return `${normalizedInteger},${cents}`;
}

function parseCurrencyInput(value: string) {
  const normalized = value.replace(/\./g, "").replace(",", ".").trim();
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : 0;
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

function translateRole(role?: string) {
  switch (role) {
    case "Owner":
      return "Proprietário";
    case "Admin":
      return "Administrativo";
    case "Instructor":
      return "Instrutor";
    case "Student":
      return "Aluno";
    default:
      return role ?? "-";
  }
}

function translateAuditEvent(eventType: string) {
  switch (eventType) {
    case "auth.login":
      return "Login";
    case "auth.refresh":
      return "Renovação de sessão";
    case "auth.logout":
      return "Logout";
    case "auth.change-password":
      return "Troca de senha";
    case "auth.forgot-password":
      return "Solicitação de recuperação";
    case "auth.reset-password":
      return "Redefinição de senha";
    case "identity.invitation.create":
      return "Convite criado";
    case "identity.invitation.accept":
      return "Convite aceito";
    case "identity.invitation.cancel":
      return "Convite cancelado";
    case "identity.user.create":
      return "Colaborador criado";
    case "identity.user.update":
      return "Colaborador atualizado";
    case "identity.user.activation":
      return "Ativação ou desativação";
    case "identity.user.reset-password":
      return "Senha temporária enviada";
    default:
      return eventType;
  }
}

function summarizeAuditMetadata(metadata: unknown) {
  if (!metadata || typeof metadata !== "object") {
    return "";
  }

  const entries = Object.entries(metadata as Record<string, unknown>)
    .filter(([, value]) => value !== null && value !== undefined && value !== "")
    .slice(0, 4)
    .map(([key, value]) => `${key}: ${String(value)}`);

  return entries.join(" • ");
}

function defaultAvailability(): InstructorAvailabilitySlot[] {
  return [
    { dayOfWeek: 1, startMinutesUtc: 8 * 60, endMinutesUtc: 18 * 60, label: "Segunda" },
    { dayOfWeek: 2, startMinutesUtc: 8 * 60, endMinutesUtc: 18 * 60, label: "Terça" },
    { dayOfWeek: 3, startMinutesUtc: 8 * 60, endMinutesUtc: 18 * 60, label: "Quarta" },
    { dayOfWeek: 4, startMinutesUtc: 8 * 60, endMinutesUtc: 18 * 60, label: "Quinta" },
    { dayOfWeek: 5, startMinutesUtc: 8 * 60, endMinutesUtc: 18 * 60, label: "Sexta" },
    { dayOfWeek: 6, startMinutesUtc: 8 * 60, endMinutesUtc: 14 * 60, label: "Sábado" }
  ];
}

function minutesToTime(value: number) {
  const hours = Math.floor(value / 60).toString().padStart(2, "0");
  const minutes = (value % 60).toString().padStart(2, "0");
  return `${hours}:${minutes}`;
}

function timeToMinutes(value: string) {
  const [hours, minutes] = value.split(":").map(Number);
  if (!Number.isFinite(hours) || !Number.isFinite(minutes)) {
    return 8 * 60;
  }

  return hours * 60 + minutes;
}

function updateAvailabilityRow(
  selectedUser: SchoolUser | null,
  index: number,
  field: "startMinutesUtc" | "endMinutesUtc",
  value: number,
  setCreateForm: Dispatch<SetStateAction<typeof initialCreateForm>>,
  setEditForm: Dispatch<SetStateAction<typeof initialEditForm>>
) {
  const apply = (items: InstructorAvailabilitySlot[]) =>
    items.map((slot, slotIndex) => (slotIndex === index ? { ...slot, [field]: value } : slot));

  if (selectedUser) {
    setEditForm((current) => ({ ...current, availability: apply(current.availability) }));
    return;
  }

  setCreateForm((current) => ({ ...current, availability: apply(current.availability) }));
}

function startOfWeek(value: Date) {
  const normalized = new Date(value.getFullYear(), value.getMonth(), value.getDate());
  const day = normalized.getDay();
  const diff = day === 0 ? -6 : 1 - day;
  normalized.setDate(normalized.getDate() + diff);
  normalized.setHours(0, 0, 0, 0);
  return normalized;
}

function addDays(value: Date, days: number) {
  const next = new Date(value);
  next.setDate(next.getDate() + days);
  return next;
}

function toDateInputValue(value: Date) {
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, "0");
  const day = String(value.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function buildWeekDays(weekStart: Date) {
  return Array.from({ length: 7 }, (_, index) => {
    const date = addDays(weekStart, index);
    return {
      date,
      dateKey: toDateInputValue(date),
      weekdayLabel: new Intl.DateTimeFormat("pt-BR", { weekday: "short" }).format(date).replace(".", ""),
      dateLabel: new Intl.DateTimeFormat("pt-BR", { day: "2-digit", month: "2-digit" }).format(date)
    };
  });
}

function isSameLocalDay(value: string, date: Date) {
  const parsed = new Date(value);
  return (
    parsed.getFullYear() === date.getFullYear() &&
    parsed.getMonth() === date.getMonth() &&
    parsed.getDate() === date.getDate()
  );
}

function isWithinWeek(value: string, weekStart: Date) {
  const parsed = new Date(value);
  const from = startOfWeek(weekStart).getTime();
  const to = addDays(startOfWeek(weekStart), 7).getTime();
  return parsed.getTime() >= from && parsed.getTime() < to;
}

function formatWeekRangeLabel(weekStart: Date) {
  const weekEnd = addDays(weekStart, 6);
  return `${formatDate(weekStart.toISOString())} a ${formatDate(weekEnd.toISOString())}`;
}

function describeBaseAvailabilityForDay(slots: InstructorAvailabilitySlot[], date: Date) {
  const daySlot = slots.find((item) => item.dayOfWeek === date.getDay());
  if (!daySlot) {
    return "Sem padrão";
  }

  return `${minutesToTime(daySlot.startMinutesUtc)} às ${minutesToTime(daySlot.endMinutesUtc)}`;
}

function combineDateAndTimeToUtc(dateValue: string, timeValue: string) {
  if (!dateValue || !timeValue) {
    return null;
  }

  const parsed = new Date(`${dateValue}T${timeValue}:00`);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

function formatBlockWindow(startAtUtc: string, endAtUtc: string) {
  const start = new Date(startAtUtc);
  const end = new Date(endAtUtc);
  return `${start.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" })} às ${end.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" })}`;
}
