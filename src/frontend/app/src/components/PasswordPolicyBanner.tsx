import { useState } from "react";
import { useSession } from "../auth/SessionContext";
import { ErrorBlock } from "./OperationsUi";

export function PasswordPolicyBanner() {
  const { user, changePassword } = useSession();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!user?.mustChangePassword) {
    return null;
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (newPassword !== confirmPassword) {
      setError("A confirmação da nova senha precisa ser igual à nova senha.");
      return;
    }

    try {
      setIsSaving(true);
      setError(null);
      await changePassword({ currentPassword, newPassword });
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Não foi possível trocar a senha.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="mb-6 rounded-[28px] border border-[var(--q-warning)]/35 bg-[var(--q-warning-bg)] p-5 shadow-[var(--app-shadow-soft)]">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div className="max-w-2xl">
          <div className="text-xs uppercase tracking-[0.28em] text-[#B58100]">Política de senha</div>
          <h3 className="mt-2 text-xl font-semibold text-[var(--q-text)]">Sua conta exige troca imediata de senha</h3>
          <p className="mt-2 text-sm leading-6 text-[var(--q-text-2)]">
            Isso normalmente acontece após um reset administrativo ou no primeiro acesso por convite. Atualize a senha agora para liberar o uso normal da conta.
          </p>
        </div>

        <form className="grid min-w-[320px] gap-3 lg:max-w-md" onSubmit={handleSubmit}>
          <label className="grid gap-2 text-sm text-[var(--q-text)]">
            <span>Senha atual ou temporária</span>
            <input
              className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
              type="password"
              value={currentPassword}
              onChange={(event) => setCurrentPassword(event.target.value)}
              required
            />
          </label>
          <label className="grid gap-2 text-sm text-[var(--q-text)]">
            <span>Nova senha</span>
            <input
              className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
              type="password"
              value={newPassword}
              onChange={(event) => setNewPassword(event.target.value)}
              required
            />
          </label>
          <label className="grid gap-2 text-sm text-[var(--q-text)]">
            <span>Confirmação da nova senha</span>
            <input
              className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
              type="password"
              value={confirmPassword}
              onChange={(event) => setConfirmPassword(event.target.value)}
              required
            />
          </label>
          <button
            className="rounded-full border border-transparent bg-[var(--q-navy)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-92"
            type="submit"
            disabled={isSaving}
          >
            {isSaving ? "Atualizando..." : "Atualizar senha"}
          </button>
          {error ? <ErrorBlock message={error} /> : null}
        </form>
      </div>
    </section>
  );
}
