import { useEffect, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Eye, EyeOff, LockKeyhole, Mail } from "lucide-react";
import { useSession } from "../auth/SessionContext";
import {
  forgotPasswordRequest,
  resetPasswordRequest,
  type InvitationPreviewResponse
} from "../lib/auth-api";
import { resolveHomePath } from "../lib/home-path";
import { translateLabel } from "../lib/localization";

export function LoginPage() {
  const { login, acceptInvite, previewInvite, isLoading } = useSession();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const inviteToken = searchParams.get("invite");
  const resetToken = searchParams.get("reset");
  const [mode, setMode] = useState<"login" | "invite" | "forgot" | "reset">(
    resetToken ? "reset" : inviteToken ? "invite" : "login"
  );
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [invitePreview, setInvitePreview] = useState<InvitationPreviewResponse | null>(null);
  const [inviteLoading, setInviteLoading] = useState(Boolean(inviteToken));
  const [showPassword, setShowPassword] = useState(false);
  const [showResetPassword, setShowResetPassword] = useState(false);
  const [loginForm, setLoginForm] = useState({
    email: "",
    password: ""
  });
  const [forgotEmail, setForgotEmail] = useState("");
  const [inviteForm, setInviteForm] = useState({
    password: "",
    confirmPassword: ""
  });
  const [resetForm, setResetForm] = useState({
    password: "",
    confirmPassword: ""
  });

  useEffect(() => {
    if (resetToken) {
      setMode("reset");
      setInviteLoading(false);
      setInvitePreview(null);
      return;
    }

    if (!inviteToken) {
      setInviteLoading(false);
      setInvitePreview(null);
      return;
    }

    setMode("invite");
    const currentInviteToken = inviteToken;

    let cancelled = false;

    async function loadPreview() {
      try {
        setInviteLoading(true);
        setError(null);
        const preview = await previewInvite(currentInviteToken);
        if (!cancelled) {
          setInvitePreview(preview);
        }
      } catch (cause) {
        if (!cancelled) {
          setInvitePreview(null);
          setError(cause instanceof Error ? cause.message : "Não foi possível validar o convite.");
        }
      } finally {
        if (!cancelled) {
          setInviteLoading(false);
        }
      }
    }

    void loadPreview();
    return () => {
      cancelled = true;
    };
  }, [inviteToken, previewInvite]);

  const handleLogin = async () => {
    try {
      setSubmitting(true);
      setError(null);
      setNotice(null);
      const session = await login(loginForm);
      navigate(resolveHomePath(session));
    } catch (cause) {
      setError("Não foi possível entrar com este e-mail e senha. Confira os dados e tente novamente.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleAcceptInvite = async () => {
    if (!inviteToken) {
      setError("Token de convite não encontrado.");
      return;
    }

    if (inviteForm.password !== inviteForm.confirmPassword) {
      setError("A confirmação da senha precisa ser igual à senha criada.");
      return;
    }

    try {
      setSubmitting(true);
      setError(null);
      setNotice(null);
      const session = await acceptInvite({
        token: inviteToken,
        password: inviteForm.password
      });
      navigate(resolveHomePath(session));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Não foi possível aceitar o convite.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleForgotPassword = async () => {
    try {
      setSubmitting(true);
      setError(null);
      setNotice(null);
      const response = await forgotPasswordRequest({ email: forgotEmail });
      setNotice(response.message);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Não foi possível iniciar a recuperação de senha.");
    } finally {
      setSubmitting(false);
    }
  };

  const handleResetPassword = async () => {
    if (!resetToken) {
      setError("Token de recuperação não encontrado.");
      return;
    }

    if (resetForm.password !== resetForm.confirmPassword) {
      setError("A confirmação da senha precisa ser igual à senha criada.");
      return;
    }

    try {
      setSubmitting(true);
      setError(null);
      setNotice(null);
      const session = await resetPasswordRequest({
        token: resetToken,
        newPassword: resetForm.password
      });
      const user = await login({
        email: session.email,
        password: resetForm.password
      });
      navigate(resolveHomePath(user));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Não foi possível redefinir a senha.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen bg-[var(--app-bg)] px-4 py-6 text-[var(--q-text)] sm:px-6 sm:py-10">
      <div className="mx-auto max-w-[980px] rounded-[34px] border border-[var(--q-border)] bg-[linear-gradient(135deg,#f9fcff_0%,#edf7fb_58%,#f7fbfe_100%)] shadow-[0_24px_80px_rgba(36,75,132,0.15)]">
        <div className="relative overflow-hidden rounded-[34px] px-6 py-8 sm:px-10 sm:py-10 lg:px-12 lg:py-12">
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_top_left,rgba(255,255,255,0.94),transparent_34%),radial-gradient(circle_at_bottom_right,rgba(31,182,201,0.08),transparent_26%)]" />
          <div className="pointer-events-none absolute -left-12 bottom-0 h-[240px] w-[500px] rounded-tr-[100%] bg-[radial-gradient(circle_at_bottom_left,rgba(46,212,167,0.24),rgba(31,182,201,0.16)_42%,rgba(255,255,255,0)_74%)]" />
          <svg
            className="pointer-events-none absolute bottom-2 left-0 hidden h-[170px] w-[420px] text-[rgba(31,182,201,0.16)] sm:block"
            viewBox="0 0 420 170"
            fill="none"
            aria-hidden="true"
          >
            <path d="M-20 130C52 95 122 94 191 118C248 138 312 140 420 92" stroke="currentColor" strokeWidth="2.5" />
            <path d="M-18 146C58 113 122 111 194 132C254 150 322 149 430 109" stroke="currentColor" strokeWidth="1.8" />
            <path d="M-12 162C66 134 136 132 205 147C271 161 336 157 430 126" stroke="currentColor" strokeWidth="1.4" />
          </svg>

          <div className="relative grid gap-8 lg:grid-cols-[1fr_0.9fr] lg:items-center">
            <section className="flex min-h-[420px] flex-col justify-start pt-3 lg:min-h-[470px]">
              <div className="mt-14 flex justify-center lg:justify-center">
                <img
                  src="/branding/logo-transparent.png"
                  alt="Quiver Kite Experience"
                  className="h-auto w-[250px] object-contain sm:w-[290px]"
                />
              </div>

            </section>

            <section className="relative flex justify-center lg:justify-center">
              <div className="w-full max-w-[370px] rounded-[22px] border border-[rgba(255,255,255,0.8)] bg-[rgba(255,255,255,0.94)] p-6 shadow-[0_18px_50px_rgba(36,75,132,0.16)] backdrop-blur-xl sm:p-8">
                <h2 className="text-[2rem] font-semibold tracking-[-0.03em] text-[var(--q-brand-wordmark)]">
                  {mode === "invite"
                    ? "Aceite seu convite"
                    : mode === "forgot"
                      ? "Recupere seu acesso"
                      : mode === "reset"
                        ? "Defina sua nova senha"
                        : "Acesse sua conta"}
                </h2>
                <p className="mt-2 text-sm leading-6 text-[var(--q-text-2)]">
                  {mode === "invite"
                    ? "Finalize o convite definindo a senha inicial para entrar na plataforma."
                    : mode === "forgot"
                      ? "Informe o e-mail da sua conta para receber o link de recuperação."
                      : mode === "reset"
                        ? "Crie uma nova senha para retomar o acesso à plataforma."
                    : "Gestão profissional para quem vive do esporte"}
                </p>

                {mode === "login" ? (
                  <div className="mt-7 grid gap-3.5">
                    <label className="grid gap-2">
                      <span className="sr-only">Email</span>
                      <div className="flex items-center gap-3 rounded-[14px] border border-[var(--q-border)] bg-white px-4 py-3.5 shadow-[0_2px_8px_rgba(36,75,132,0.04)]">
                        <Mail size={17} className="shrink-0 text-[var(--q-text-2)]" />
                        <input
                          value={loginForm.email}
                          onChange={(event) =>
                            setLoginForm((current) => ({ ...current, email: event.target.value }))
                          }
                          className="w-full bg-transparent text-[0.98rem] outline-none placeholder:text-[color:rgba(75,100,118,0.74)]"
                          placeholder="Digite seu e-mail"
                        />
                      </div>
                    </label>

                    <label className="grid gap-2">
                      <span className="sr-only">Senha</span>
                      <div className="flex items-center gap-3 rounded-[14px] border border-[var(--q-border)] bg-white px-4 py-3.5 shadow-[0_2px_8px_rgba(36,75,132,0.04)]">
                        <LockKeyhole size={17} className="shrink-0 text-[var(--q-text-2)]" />
                        <input
                          type={showPassword ? "text" : "password"}
                          value={loginForm.password}
                          onChange={(event) =>
                            setLoginForm((current) => ({ ...current, password: event.target.value }))
                          }
                          className="w-full bg-transparent text-[0.98rem] outline-none placeholder:text-[color:rgba(75,100,118,0.74)]"
                          placeholder="Digite sua senha"
                        />
                        <button
                          type="button"
                          onClick={() => setShowPassword((current) => !current)}
                          className="inline-flex h-8 w-8 items-center justify-center rounded-full text-[var(--q-text-2)] transition hover:bg-[var(--q-surface-2)]"
                          aria-label={showPassword ? "Ocultar senha" : "Mostrar senha"}
                        >
                          {showPassword ? <EyeOff size={17} /> : <Eye size={17} />}
                        </button>
                      </div>
                    </label>

                    <button
                      onClick={() => void handleLogin()}
                      disabled={submitting || isLoading}
                      style={{ backgroundImage: "var(--q-grad-brand)" }}
                      className="mt-1 rounded-[14px] px-5 py-3.5 text-[1rem] font-semibold text-white shadow-[0_16px_32px_rgba(24,89,145,0.2)] transition hover:opacity-95 disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      {submitting ? "Entrando..." : "Entrar"}
                    </button>

                    <button
                      type="button"
                      onClick={() => {
                        setMode("forgot");
                        setError(null);
                        setNotice(null);
                      }}
                      className="mt-1 text-center text-sm font-medium text-[var(--q-brand-secondary)] transition hover:opacity-80"
                    >
                      Esqueceu a senha?
                    </button>
                  </div>
                ) : mode === "forgot" ? (
                  <div className="mt-7 grid gap-3.5">
                    <label className="grid gap-2">
                      <span className="sr-only">Email</span>
                      <div className="flex items-center gap-3 rounded-[14px] border border-[var(--q-border)] bg-white px-4 py-3.5 shadow-[0_2px_8px_rgba(36,75,132,0.04)]">
                        <Mail size={17} className="shrink-0 text-[var(--q-text-2)]" />
                        <input
                          value={forgotEmail}
                          onChange={(event) => setForgotEmail(event.target.value)}
                          className="w-full bg-transparent text-[0.98rem] outline-none placeholder:text-[color:rgba(75,100,118,0.74)]"
                          placeholder="Digite o e-mail da conta"
                        />
                      </div>
                    </label>

                    <button
                      onClick={() => void handleForgotPassword()}
                      disabled={submitting || isLoading}
                      style={{ backgroundImage: "var(--q-grad-brand)" }}
                      className="mt-1 rounded-[14px] px-5 py-3.5 text-[1rem] font-semibold text-white shadow-[0_16px_32px_rgba(24,89,145,0.2)] transition hover:opacity-95 disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      {submitting ? "Enviando..." : "Enviar link de recuperação"}
                    </button>

                    <button
                      type="button"
                      onClick={() => {
                        setMode("login");
                        setError(null);
                        setNotice(null);
                      }}
                      className="mt-1 text-center text-sm font-medium text-[var(--q-brand-secondary)] transition hover:opacity-80"
                    >
                      Voltar ao login
                    </button>
                  </div>
                ) : mode === "reset" ? (
                  <div className="mt-7 grid gap-4">
                    <label className="grid gap-2">
                      <span className="sr-only">Nova senha</span>
                      <div className="flex items-center gap-3 rounded-[14px] border border-[var(--q-border)] bg-white px-4 py-3.5 shadow-[0_2px_8px_rgba(36,75,132,0.04)]">
                        <LockKeyhole size={17} className="shrink-0 text-[var(--q-text-2)]" />
                        <input
                          type={showResetPassword ? "text" : "password"}
                          value={resetForm.password}
                          onChange={(event) =>
                            setResetForm((current) => ({ ...current, password: event.target.value }))
                          }
                          className="w-full bg-transparent text-[0.98rem] outline-none placeholder:text-[color:rgba(75,100,118,0.74)]"
                          placeholder="Digite a nova senha"
                        />
                        <button
                          type="button"
                          onClick={() => setShowResetPassword((current) => !current)}
                          className="inline-flex h-8 w-8 items-center justify-center rounded-full text-[var(--q-text-2)] transition hover:bg-[var(--q-surface-2)]"
                          aria-label={showResetPassword ? "Ocultar senha" : "Mostrar senha"}
                        >
                          {showResetPassword ? <EyeOff size={17} /> : <Eye size={17} />}
                        </button>
                      </div>
                    </label>

                    <input
                      type={showResetPassword ? "text" : "password"}
                      value={resetForm.confirmPassword}
                      onChange={(event) =>
                        setResetForm((current) => ({ ...current, confirmPassword: event.target.value }))
                      }
                      className="rounded-[14px] border border-[var(--q-border)] bg-white px-4 py-3.5 text-[0.98rem] outline-none placeholder:text-[color:rgba(75,100,118,0.74)]"
                      placeholder="Confirme a nova senha"
                    />

                    <button
                      onClick={() => void handleResetPassword()}
                      disabled={submitting || isLoading}
                      style={{ backgroundImage: "var(--q-grad-brand)" }}
                      className="mt-1 rounded-[14px] px-5 py-3.5 text-[1rem] font-semibold text-white shadow-[0_16px_32px_rgba(24,89,145,0.2)] transition hover:opacity-95 disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      {submitting ? "Salvando..." : "Redefinir senha"}
                    </button>
                  </div>
                ) : (
                  <div className="mt-7 grid gap-4">
                    <div className="rounded-[18px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                      {inviteLoading ? (
                        <div className="text-sm text-[var(--q-text-2)]">Validando convite...</div>
                      ) : invitePreview ? (
                        <div className="space-y-2 text-sm text-[var(--q-text)]">
                          <div>
                            <span className="text-[var(--q-muted)]">Pessoa convidada:</span> {invitePreview.fullName}
                          </div>
                          <div>
                            <span className="text-[var(--q-muted)]">E-mail do convite:</span> {invitePreview.email}
                          </div>
                          <div>
                            <span className="text-[var(--q-muted)]">Papel de acesso:</span> {translateLabel(invitePreview.role)}
                          </div>
                          <div>
                            <span className="text-[var(--q-muted)]">Valido ate:</span>{" "}
                            {new Date(invitePreview.expiresAtUtc).toLocaleString("pt-BR")}
                          </div>
                        </div>
                      ) : (
                        <div className="text-sm text-[var(--q-text-2)]">
                          Não foi possível carregar os detalhes do convite.
                        </div>
                      )}
                    </div>

                    <input
                      type="password"
                      value={inviteForm.password}
                      onChange={(event) =>
                        setInviteForm((current) => ({ ...current, password: event.target.value }))
                      }
                      className="rounded-[14px] border border-[var(--q-border)] bg-white px-4 py-3.5 text-[0.98rem] outline-none placeholder:text-[color:rgba(75,100,118,0.74)]"
                      placeholder="Digite sua senha"
                    />

                    <input
                      type="password"
                      value={inviteForm.confirmPassword}
                      onChange={(event) =>
                        setInviteForm((current) => ({ ...current, confirmPassword: event.target.value }))
                      }
                      className="rounded-[14px] border border-[var(--q-border)] bg-white px-4 py-3.5 text-[0.98rem] outline-none placeholder:text-[color:rgba(75,100,118,0.74)]"
                      placeholder="Confirme sua senha"
                    />

                    <button
                      onClick={() => void handleAcceptInvite()}
                      disabled={submitting || isLoading || inviteLoading || !invitePreview}
                      style={{ backgroundImage: "var(--q-grad-brand)" }}
                      className="mt-1 rounded-[14px] px-5 py-3.5 text-[1rem] font-semibold text-white shadow-[0_16px_32px_rgba(24,89,145,0.2)] transition hover:opacity-95 disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      {submitting ? "Aceitando convite..." : "Aceitar convite"}
                    </button>
                  </div>
                )}

                {error ? (
                  <div className="mt-4 rounded-2xl border border-[var(--q-danger)]/30 bg-[var(--q-danger-bg)] px-4 py-3 text-sm text-[var(--q-danger)]">
                    {error}
                  </div>
                ) : null}
                {notice ? (
                  <div className="mt-4 rounded-2xl border border-[var(--q-info)]/30 bg-[var(--q-info-bg)] px-4 py-3 text-sm text-[var(--q-info)]">
                    {notice}
                  </div>
                ) : null}
              </div>
            </section>
          </div>

          <div className="relative mt-4 text-center text-[1.02rem] font-medium leading-8 text-[color:rgba(65,98,128,0.92)]">
            Menos planilha. <span className="text-[var(--q-aqua)]">Mais tempo na água.</span>
          </div>
        </div>
      </div>
    </div>
  );
}
