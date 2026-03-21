import { BellRing, CheckCheck, LoaderCircle } from "lucide-react";
import { useEffect, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { ErrorBlock } from "../components/OperationsUi";
import {
  getStudentPortalNotifications,
  readAllStudentPortalNotifications,
  readStudentPortalNotification,
  type StudentPortalNotificationsResponse
} from "../lib/platform-api";
import { formatDateTime } from "../lib/formatters";
import { translateLabel } from "../lib/localization";
import { resolveStudentPortalTheme } from "../lib/student-portal-theme";

export function StudentPortalNotificationsPage() {
  const { token, school } = useSession();
  const theme = resolveStudentPortalTheme(school);
  const [payload, setPayload] = useState<StudentPortalNotificationsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      return;
    }

    void loadNotifications(token);
  }, [token]);

  async function loadNotifications(sessionToken: string) {
    try {
      setLoading(true);
      setError(null);
      setPayload(await getStudentPortalNotifications(sessionToken));
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Não foi possível carregar as notificações do portal.");
    } finally {
      setLoading(false);
    }
  }

  async function handleReadAll() {
    if (!token) {
      return;
    }

    try {
      setSaving(true);
      await readAllStudentPortalNotifications(token);
      await loadNotifications(token);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Não foi possível atualizar as notificações.");
    } finally {
      setSaving(false);
    }
  }

  async function handleRead(notificationId: string) {
    if (!token) {
      return;
    }

    try {
      setSaving(true);
      await readStudentPortalNotification(token, notificationId);
      await loadNotifications(token);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Não foi possível marcar a notificação como lida.");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <div className="flex min-h-[50vh] items-center justify-center rounded-[32px] border border-white/40 bg-white/55 text-sm uppercase tracking-[0.35em] text-slate-700 shadow-xl backdrop-blur-xl">
        <LoaderCircle size={18} className="mr-3 animate-spin" />
        Carregando notificações
      </div>
    );
  }

  if (!payload) {
    return <ErrorBlock message={error ?? "Não foi possível carregar as notificações."} />;
  }

  return (
    <div className="space-y-6">
      <section className="rounded-[32px] p-6 shadow-xl backdrop-blur-2xl" style={{ border: `1px solid ${theme.frame}`, background: theme.heroBackground }}>
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full px-3 py-1 text-xs uppercase tracking-[0.35em] text-slate-700" style={{ border: `1px solid ${theme.frame}`, backgroundColor: "rgba(255,255,255,0.72)" }}>
              <BellRing size={14} />
              Central de notificações
            </div>
            <h2 className="mt-4 text-3xl font-semibold tracking-tight text-slate-950">
              Alertas, lembretes e eventos do seu portal
            </h2>
            <p className="mt-3 max-w-3xl text-sm leading-6 text-slate-600">
              Aqui ficam as atualizações importantes sobre agendamentos, remarcações, cancelamentos e lembretes de aula.
            </p>
          </div>
          <div className="rounded-[24px] px-4 py-4 text-right" style={{ border: `1px solid ${theme.frame}`, background: theme.cardBackground }}>
            <div className="text-xs uppercase tracking-[0.25em] text-slate-500">Não lidas</div>
            <div className="mt-2 text-3xl font-semibold text-slate-950">{payload.unreadCount}</div>
            <button
              onClick={() => void handleReadAll()}
              disabled={saving || payload.unreadCount === 0}
              className="mt-4 inline-flex items-center gap-2 rounded-2xl border border-slate-950/10 bg-white px-4 py-3 text-sm text-slate-900 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60"
            >
              <CheckCheck size={16} />
              Marcar tudo como lido
            </button>
          </div>
        </div>
      </section>

      {error ? <ErrorBlock message={error} /> : null}

      <section className="space-y-4">
        {payload.items.length === 0 ? (
          <div className="rounded-[26px] border border-dashed border-slate-300 bg-white/70 px-5 py-6 text-sm text-slate-600 shadow-lg backdrop-blur-xl">
            Nenhuma notificação no momento.
          </div>
        ) : (
          payload.items.map((item) => (
            <article key={item.id} className="rounded-[28px] p-5 shadow-lg backdrop-blur-2xl" style={{ border: `1px solid ${theme.frame}`, background: theme.cardBackground }}>
              <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                <div>
                  <div className="inline-flex rounded-full px-3 py-1 text-xs uppercase tracking-[0.25em] text-slate-700" style={{ border: `1px solid ${theme.frame}`, backgroundColor: item.isSynthetic ? theme.accentSoft : theme.primarySoft }}>
                    {item.isSynthetic ? "Lembrete" : translateLabel(item.category)}
                  </div>
                  <h3 className="mt-3 text-lg font-semibold text-slate-950">{item.title}</h3>
                  <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-600">{item.message}</p>
                  <div className="mt-3 text-xs uppercase tracking-[0.22em] text-slate-500">
                    {formatDateTime(item.createdAtUtc)}
                  </div>
                </div>

                <div className="flex flex-wrap gap-3">
                  {item.readAtUtc ? (
                    <span className="inline-flex items-center rounded-2xl border border-emerald-300/40 bg-emerald-50 px-4 py-3 text-sm text-emerald-900">
                      Lida em {formatDateTime(item.readAtUtc)}
                    </span>
                  ) : !item.isSynthetic ? (
                    <button
                      onClick={() => void handleRead(item.id)}
                      disabled={saving}
                      className="rounded-2xl border border-slate-950/10 bg-slate-950 px-4 py-3 text-sm text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      Marcar como lida
                    </button>
                  ) : (
                    <span className="inline-flex items-center rounded-2xl border border-amber-300/40 bg-amber-50 px-4 py-3 text-sm text-amber-900">
                      Lembrete dinâmico do portal
                    </span>
                  )}
                </div>
              </div>
            </article>
          ))
        )}
      </section>
    </div>
  );
}
