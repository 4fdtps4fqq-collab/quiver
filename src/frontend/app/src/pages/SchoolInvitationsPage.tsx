import { useEffect, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { getSchoolCurrent } from "../lib/auth-api";
import { cancelSchoolInvitation, createSchoolInvitation, getSchoolInvitations, type SchoolInvitation } from "../lib/platform-api";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock, StatusBadge } from "../components/OperationsUi";
import { formatPhone, roleOptions, TextField, translateRole } from "./school-admin-shared";

const initialInvitationForm = {
  fullName: "",
  email: "",
  phone: "",
  role: "5",
  expiresInDays: "7"
};

export function SchoolInvitationsPage() {
  const { token } = useSession();
  const [schoolDisplayName, setSchoolDisplayName] = useState("");
  const [schoolSlug, setSchoolSlug] = useState("");
  const [invitationForm, setInvitationForm] = useState(initialInvitationForm);
  const [invitations, setInvitations] = useState<SchoolInvitation[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

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
      const [schoolData, invitationsData] = await Promise.all([
        getSchoolCurrent(currentToken),
        getSchoolInvitations(currentToken)
      ]);
      setSchoolDisplayName(schoolData.displayName);
      setSchoolSlug(schoolData.slug);
      setInvitations(invitationsData);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar os convites.");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleCreateInvitation(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSaving(true);
      setError(null);
      setNotice(null);
      const createdInvitation = await createSchoolInvitation(token, {
        fullName: invitationForm.fullName,
        email: invitationForm.email,
        phone: invitationForm.phone || undefined,
        role: Number(invitationForm.role),
        expiresInDays: Number(invitationForm.expiresInDays),
        schoolDisplayName,
        schoolSlug
      });
      setInvitationForm(initialInvitationForm);
      setNotice(
        createdInvitation.deliveryMode === "File" && createdInvitation.outboxFilePath
          ? `Convite salvo no outbox local em ${createdInvitation.outboxFilePath}.`
          : "Convite enviado com sucesso."
      );
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível enviar o convite.");
    } finally {
      setIsSaving(false);
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

  return (
    <div className="space-y-6">
      <PageHero
        title="Gestão de convites por e-mail"
        description="Centralize os convites de onboarding guiado para administrativos e instrutores em uma tela própria."
        stats={[
          { label: "Convites", value: String(invitations.length) },
          { label: "Pendentes", value: String(invitations.filter((item) => item.status === "Pending").length) },
          { label: "Aceitos", value: String(invitations.filter((item) => item.status === "Accepted").length) },
          { label: "Cancelados", value: String(invitations.filter((item) => item.status === "Cancelled").length) }
        ]}
        statsBelow
      />

      {isLoading ? <LoadingBlock label="Carregando convites" /> : null}
      {error ? <ErrorBlock message={error} /> : null}
      {notice ? <div className="rounded-[24px] border border-[var(--q-info)]/30 bg-[var(--q-info-bg)] px-5 py-4 text-sm text-[var(--q-info)]">{notice}</div> : null}

      <div className="grid gap-4 xl:grid-cols-[0.9fr_1.1fr]">
        <GlassCard title="Novo convite" description="O convidado cria a própria senha e conclui o onboarding guiado.">
          <form className="space-y-4" onSubmit={handleCreateInvitation}>
            <div className="grid gap-4 md:grid-cols-2">
              <TextField label="Nome completo" value={invitationForm.fullName} onChange={(value) => setInvitationForm((current) => ({ ...current, fullName: value }))} required />
              <TextField label="Telefone" value={invitationForm.phone} onChange={(value) => setInvitationForm((current) => ({ ...current, phone: formatPhone(value) }))} placeholder="(00) 00000-0000" />
            </div>

            <div className="grid gap-4 md:grid-cols-[1.1fr_0.9fr]">
              <TextField label="Email do convite" type="email" value={invitationForm.email} onChange={(value) => setInvitationForm((current) => ({ ...current, email: value }))} required />
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Função</span>
                <select className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none" value={invitationForm.role} onChange={(event) => setInvitationForm((current) => ({ ...current, role: event.target.value }))}>
                  {roleOptions.map((option) => <option key={`invite-${option.value}`} value={option.value}>{option.label}</option>)}
                </select>
              </label>
            </div>

            <div className="grid gap-4 md:max-w-[240px]">
              <TextField label="Validade em dias" value={invitationForm.expiresInDays} onChange={(value) => setInvitationForm((current) => ({ ...current, expiresInDays: value }))} />
            </div>

            <button className="rounded-full border border-transparent px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95" style={{ backgroundImage: "var(--q-grad-brand)", backgroundColor: "var(--q-navy)", boxShadow: "0 18px 32px rgba(11, 60, 93, 0.18)" }} type="submit" disabled={isSaving}>
              {isSaving ? "Enviando convite" : "Enviar convite"}
            </button>
          </form>
        </GlassCard>

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
                      {invitation.outboxFilePath ? <div className="mt-1 text-xs text-[var(--q-muted)]">{invitation.outboxFilePath}</div> : null}
                    </td>
                    <td className="py-4 align-middle">
                      {invitation.status === "Pending" ? (
                        <button className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-2.5 text-sm font-medium text-[var(--q-text)] transition hover:bg-[var(--q-surface-2)]" type="button" onClick={() => void handleCancelInvitation(invitation.id)}>
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
          {invitations.length === 0 ? <div className="mt-4 rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">Ainda não há convites enviados por esta escola.</div> : null}
        </GlassCard>
      </div>
    </div>
  );
}
