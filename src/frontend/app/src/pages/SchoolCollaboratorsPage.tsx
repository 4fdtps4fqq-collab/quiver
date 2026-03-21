import { useEffect, useMemo, useState } from "react";
import { useSession } from "../auth/SessionContext";
import {
  createInstructor,
  createSchoolUser,
  getInstructors,
  getSchoolUsers,
  resetSchoolUserPassword,
  type SchoolUser,
  updateInstructor,
  updateSchoolUser
} from "../lib/platform-api";
import { PageHero } from "../components/PageHero";
import { PermissionMatrix } from "../components/PermissionMatrix";
import { ErrorBlock, GlassCard, LoadingBlock } from "../components/OperationsUi";
import { getDefaultPermissionsForRole, normalizePermissions, type PlatformPermission } from "../lib/permissions";
import {
  defaultAvailability,
  formatCurrencyInput,
  formatCurrencyMask,
  formatPhone,
  parseCurrencyInput,
  roleOptions,
  roleValueToName,
  TextField,
  translateRole
} from "./school-admin-shared";

const initialCreateForm = {
  fullName: "",
  email: "",
  role: "3",
  permissions: getDefaultPermissionsForRole("Instructor"),
  phone: "",
  salaryAmount: "",
  specialties: "",
  hourlyRate: "",
  isActive: true
};

const initialEditForm = {
  profileId: "",
  fullName: "",
  role: "3",
  permissions: getDefaultPermissionsForRole("Instructor"),
  phone: "",
  salaryAmount: "",
  specialties: "",
  hourlyRate: "",
  isActive: true
};

type SchoolUserRole = (typeof roleValueToName)[keyof typeof roleValueToName];

export function SchoolCollaboratorsPage() {
  const { token } = useSession();
  const [users, setUsers] = useState<SchoolUser[]>([]);
  const [instructors, setInstructors] = useState<Awaited<ReturnType<typeof getInstructors>>>([]);
  const [selectedUserId, setSelectedUserId] = useState("");
  const [collaboratorSearch, setCollaboratorSearch] = useState("");
  const [createForm, setCreateForm] = useState(initialCreateForm);
  const [editForm, setEditForm] = useState(initialEditForm);
  const [isLoading, setIsLoading] = useState(true);
  const [isSavingCreate, setIsSavingCreate] = useState(false);
  const [isSavingEdit, setIsSavingEdit] = useState(false);
  const [isResettingPassword, setIsResettingPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const collaborators = useMemo(
    () => users.filter((item) => item.role === "Admin" || item.role === "Instructor"),
    [users]
  );
  const selectedUser = collaborators.find((item) => item.identityUserId === selectedUserId) ?? null;
  const selectedInstructor = selectedUser
    ? instructors.find((item) => item.identityUserId === selectedUser.identityUserId) ?? null
    : null;
  const filteredCollaborators = collaboratorSearch.trim()
    ? collaborators.filter((item) => {
        const haystack = `${item.fullName} ${item.phone ?? ""} ${translateRole(item.role)}`.toLowerCase();
        return haystack.includes(collaboratorSearch.trim().toLowerCase());
      })
    : collaborators;

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
      hourlyRate: selectedInstructor ? formatCurrencyInput(selectedInstructor.hourlyRate) : "",
      isActive: selectedUser.isActive
    });
  }, [selectedInstructor, selectedUser]);

  async function loadData(currentToken: string) {
    try {
      setIsLoading(true);
      setError(null);
      const [usersData, instructorsData] = await Promise.all([
        getSchoolUsers(currentToken),
        getInstructors(currentToken)
      ]);
      setUsers(usersData);
      setInstructors(instructorsData);
      const collaboratorList = usersData.filter((item) => item.role === "Admin" || item.role === "Instructor");
      setSelectedUserId((current) => collaboratorList.some((item) => item.identityUserId === current) ? current : "");
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar os colaboradores.");
    } finally {
      setIsLoading(false);
    }
  }

  function resolveRoleName(roleValue: string): SchoolUserRole {
    return roleValueToName[Number(roleValue) as keyof typeof roleValueToName] ?? "Instructor";
  }

  function handleCreateRoleChange(roleValue: string) {
    const roleName = resolveRoleName(roleValue);
    setCreateForm((current) => ({ ...current, role: roleValue, permissions: getDefaultPermissionsForRole(roleName) }));
  }

  function handleEditRoleChange(roleValue: string) {
    const roleName = resolveRoleName(roleValue);
    setEditForm((current) => ({ ...current, role: roleValue, permissions: getDefaultPermissionsForRole(roleName) }));
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
        mustChangePassword: true
      });

      if (resolveRoleName(createForm.role) === "Instructor") {
        await createInstructor(token, {
          fullName: createForm.fullName,
          email: createForm.email || undefined,
          phone: createForm.phone || undefined,
          specialties: createForm.specialties || undefined,
          availability: defaultAvailability(),
          hourlyRate: parseCurrencyInput(createForm.hourlyRate),
          identityUserId: createdUser.identityUserId
        });
      }

      setNotice(
        createdUser.deliveryMode === "File" && createdUser.outboxFilePath
          ? `Colaborador criado. A senha temporária e o onboarding foram salvos no outbox em ${createdUser.outboxFilePath}.`
          : "Colaborador criado. A senha temporária e o onboarding foram enviados por e-mail."
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
      setNotice(null);
      await updateSchoolUser(token, selectedUser.identityUserId, {
        profileId: editForm.profileId,
        fullName: editForm.fullName,
        role: Number(editForm.role),
        permissions: editForm.permissions,
        phone: editForm.phone || undefined,
        salaryAmount: resolveRoleName(editForm.role) === "Admin" ? parseCurrencyInput(editForm.salaryAmount) : null,
        isActive: editForm.isActive,
        mustChangePassword: selectedUser.mustChangePassword
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
            availability: linkedInstructor.availability,
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
            availability: defaultAvailability(),
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

      setNotice("Colaborador atualizado com sucesso.");
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível atualizar o colaborador.");
    } finally {
      setIsSavingEdit(false);
    }
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (selectedUser) {
      await handleUpdateUser();
      return;
    }

    await handleCreateUser();
  }

  async function handleResetPassword() {
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
          ? `Nova senha temporária e onboarding salvos no outbox em ${result.outboxFilePath}.`
          : "Nova senha temporária e onboarding enviados por e-mail."
      );
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível resetar a senha do colaborador.");
    } finally {
      setIsResettingPassword(false);
    }
  }

  function handleClear() {
    setSelectedUserId("");
    setCreateForm(initialCreateForm);
    setEditForm(initialEditForm);
    setError(null);
    setNotice(null);
  }

  return (
    <div className="space-y-6">
      <PageHero
        title="Cadastro de colaboradores"
        description="Cadastre administrativos e instrutores em uma tela própria, com acesso temporário por e-mail e onboarding guiado."
        stats={[
          { label: "Colaboradores", value: String(collaborators.length) },
          { label: "Ativos", value: String(collaborators.filter((item) => item.isActive).length) },
          { label: "Instrutores", value: String(instructors.filter((item) => item.isActive).length) },
          { label: "Administrativos", value: String(collaborators.filter((item) => item.role === "Admin" && item.isActive).length) }
        ]}
        statsBelow
      />

      {isLoading ? <LoadingBlock label="Carregando colaboradores" /> : null}
      {error ? <ErrorBlock message={error} /> : null}
      {notice ? <div className="rounded-[24px] border border-[var(--q-info)]/30 bg-[var(--q-info-bg)] px-5 py-4 text-sm text-[var(--q-info)]">{notice}</div> : null}

      <div className="space-y-4">
        <GlassCard title="Ficha de colaborador" description={selectedUser ? "Atualize os dados principais, permissões e remuneração." : "Novo colaborador com senha temporária e onboarding guiado."}>
          <form className="space-y-6" onSubmit={handleSubmit}>
            <div className="grid gap-6 md:grid-cols-2">
              <TextField label="Nome completo" value={selectedUser ? editForm.fullName : createForm.fullName} onChange={(value) => selectedUser ? setEditForm((current) => ({ ...current, fullName: value })) : setCreateForm((current) => ({ ...current, fullName: value }))} required />
              <TextField label="Telefone" value={selectedUser ? editForm.phone : createForm.phone} onChange={(value) => selectedUser ? setEditForm((current) => ({ ...current, phone: formatPhone(value) })) : setCreateForm((current) => ({ ...current, phone: formatPhone(value) }))} placeholder="(00) 00000-0000" />
            </div>

            {selectedUser ? (
              <div className="space-y-3">
                <TextField label="Email de acesso" value={selectedUser.email ?? ""} disabled />
                <div className="flex flex-wrap gap-3">
                  <button className="rounded-full border border-[var(--q-info)]/25 bg-[var(--q-info-bg)] px-5 py-3 text-sm font-medium uppercase tracking-[0.22em] text-[var(--q-info)] transition hover:opacity-90" type="button" onClick={() => void handleResetPassword()} disabled={isResettingPassword}>
                    {isResettingPassword ? "Resetando senha" : "Resetar senha"}
                  </button>
                  <div className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)]">
                    Envia nova senha temporária e o link de onboarding guiado.
                  </div>
                </div>
              </div>
            ) : (
              <TextField label="Email de acesso" type="email" value={createForm.email} onChange={(value) => setCreateForm((current) => ({ ...current, email: value }))} required />
            )}

            {!selectedUser ? (
              <div className="grid gap-6 md:grid-cols-2">
                <label className="grid gap-2 text-sm text-[var(--q-text)]">
                  <span>Função</span>
                  <select className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none" value={createForm.role} onChange={(event) => handleCreateRoleChange(event.target.value)}>
                    {roleOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                  </select>
                </label>
                <div className="rounded-[22px] border border-[var(--q-info)]/25 bg-[var(--q-info-bg)] px-4 py-3 text-sm text-[var(--q-info)]">
                  O e-mail de criação já envia senha temporária e o caminho de onboarding.
                </div>
              </div>
            ) : (
              <label className="grid max-w-[320px] gap-2 text-sm text-[var(--q-text)]">
                <span>Função</span>
                <select className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none" value={editForm.role} onChange={(event) => handleEditRoleChange(event.target.value)}>
                  {roleOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                </select>
              </label>
            )}

            {resolveRoleName(selectedUser ? editForm.role : createForm.role) === "Instructor" ? (
              <div className="grid gap-6 md:grid-cols-2">
                <TextField label="Valor da hora/aula" value={selectedUser ? editForm.hourlyRate : createForm.hourlyRate} onChange={(value) => selectedUser ? setEditForm((current) => ({ ...current, hourlyRate: formatCurrencyMask(value) })) : setCreateForm((current) => ({ ...current, hourlyRate: formatCurrencyMask(value) }))} required placeholder="0,00" />
                <TextField label="Especialidades" value={selectedUser ? editForm.specialties : createForm.specialties} onChange={(value) => selectedUser ? setEditForm((current) => ({ ...current, specialties: value })) : setCreateForm((current) => ({ ...current, specialties: value }))} />
              </div>
            ) : null}

            {resolveRoleName(selectedUser ? editForm.role : createForm.role) === "Admin" ? (
              <div className="grid gap-6 md:max-w-[320px]">
                <TextField label="Salário" value={selectedUser ? editForm.salaryAmount : createForm.salaryAmount} onChange={(value) => selectedUser ? setEditForm((current) => ({ ...current, salaryAmount: formatCurrencyMask(value) })) : setCreateForm((current) => ({ ...current, salaryAmount: formatCurrencyMask(value) }))} required placeholder="0,00" />
              </div>
            ) : null}

            <PermissionMatrix
              role={resolveRoleName(selectedUser ? editForm.role : createForm.role)}
              value={selectedUser ? editForm.permissions : createForm.permissions}
              onChange={(permissions) =>
                selectedUser
                  ? setEditForm((current) => ({ ...current, permissions: permissions as PlatformPermission[] }))
                  : setCreateForm((current) => ({ ...current, permissions: permissions as PlatformPermission[] }))
              }
            />

            <div className="grid gap-3 md:grid-cols-2">
              <label className="flex items-center gap-3 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)]">
                <input checked={selectedUser ? editForm.isActive : createForm.isActive} onChange={(event) => selectedUser ? setEditForm((current) => ({ ...current, isActive: event.target.checked })) : setCreateForm((current) => ({ ...current, isActive: event.target.checked }))} type="checkbox" />
                Conta ativa
              </label>
              <div className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)]">
                A troca de senha é obrigatória no primeiro acesso e sempre após o reset.
              </div>
            </div>

            <div className="flex flex-wrap gap-3">
              <button className="mt-1 inline-flex items-center justify-center rounded-full border border-transparent px-5 py-3.5 text-sm font-medium uppercase tracking-[0.24em] text-white shadow-[0_16px_34px_rgba(18,84,135,0.18)] transition hover:opacity-95" style={{ backgroundImage: "var(--q-grad-brand)", backgroundColor: "var(--q-navy)" }} type="submit" disabled={selectedUser ? isSavingEdit : isSavingCreate}>
                {selectedUser ? (isSavingEdit ? "Salvando" : "Salvar") : isSavingCreate ? "Salvando" : "Salvar"}
              </button>
              <button className="mt-1 rounded-full border border-[var(--q-border)] bg-[var(--q-surface)] px-5 py-3.5 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-text)] transition hover:bg-[var(--q-surface-2)]" type="button" onClick={handleClear}>
                Limpar
              </button>
            </div>
          </form>
        </GlassCard>

        <GlassCard title="Base de colaboradores" description="Selecione um colaborador para carregar os dados na ficha acima.">
          <div className="mb-4">
            <input className="w-full rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3.5 text-sm text-[var(--q-text)] outline-none" placeholder="Buscar por nome, telefone ou função" value={collaboratorSearch} onChange={(event) => setCollaboratorSearch(event.target.value)} />
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
                    <td className="py-4 pr-6 align-middle"><div className="font-medium text-[var(--q-text)]">{user.fullName}</div></td>
                    <td className="py-4 pr-6 align-middle"><div className="font-medium text-[var(--q-text)]">{translateRole(user.role)}</div></td>
                    <td className="py-4 pr-6 align-middle"><div className="font-medium text-[var(--q-text)]">{user.isActive ? "Ativo" : "Inativo"}</div></td>
                    <td className="py-4 align-middle">
                      <button className="rounded-full border border-[var(--q-info)]/25 bg-[var(--q-info-bg)] px-4 py-2.5 text-sm font-medium text-[var(--q-info)] transition hover:opacity-90" type="button" onClick={() => setSelectedUserId(user.identityUserId)}>
                        Editar
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {filteredCollaborators.length === 0 ? <div className="mt-4 rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">Nenhum colaborador encontrado para esta busca.</div> : null}
        </GlassCard>
      </div>
    </div>
  );
}
