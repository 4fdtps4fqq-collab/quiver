import { History, LoaderCircle, ShieldCheck } from "lucide-react";
import { useEffect, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { ErrorBlock } from "../components/OperationsUi";
import { getStudentPortalHistory, type StudentPortalHistoryResponse } from "../lib/platform-api";
import { formatDateTime, formatMinutes } from "../lib/formatters";
import { translateLabel } from "../lib/localization";
import { resolveStudentPortalTheme } from "../lib/student-portal-theme";

export function StudentPortalHistoryPage() {
  const { token, school } = useSession();
  const theme = resolveStudentPortalTheme(school);
  const [payload, setPayload] = useState<StudentPortalHistoryResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      return;
    }

    const currentToken = token;
    let cancelled = false;

    async function load() {
      try {
        setLoading(true);
        setError(null);
        const data = await getStudentPortalHistory(currentToken);
        if (!cancelled) {
          setPayload(data);
        }
      } catch (cause) {
        if (!cancelled) {
          setError(cause instanceof Error ? cause.message : "Não foi possível carregar o histórico do aluno.");
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [token]);

  if (loading) {
    return (
      <div className="flex min-h-[50vh] items-center justify-center rounded-[32px] border border-white/40 bg-white/55 text-sm uppercase tracking-[0.35em] text-slate-700 shadow-xl backdrop-blur-xl">
        <LoaderCircle size={18} className="mr-3 animate-spin" />
        Carregando histórico
      </div>
    );
  }

  if (!payload) {
    return <ErrorBlock message={error ?? "Não foi possível carregar o histórico."} />;
  }

  return (
    <div className="space-y-6">
      <section className="rounded-[32px] p-6 shadow-xl backdrop-blur-2xl" style={{ border: `1px solid ${theme.frame}`, background: theme.heroBackground }}>
        <div className="inline-flex items-center gap-2 rounded-full px-3 py-1 text-xs uppercase tracking-[0.35em] text-slate-700" style={{ border: `1px solid ${theme.frame}`, backgroundColor: "rgba(255,255,255,0.72)" }}>
          <History size={14} />
          Histórico do treinamento
        </div>
        <h2 className="mt-4 text-3xl font-semibold tracking-tight text-slate-950">
          Linha do tempo de aulas, observações e sinais de evolução
        </h2>
        <p className="mt-3 max-w-3xl text-sm leading-6 text-slate-600">
          Use esta área para revisar o que já aconteceu, entender o efeito de remarcações e cancelamentos
          e acompanhar a leitura evolutiva de cada sessão.
        </p>
      </section>

      <section className="space-y-4">
        {payload.items.length === 0 ? (
          <div className="rounded-[26px] border border-dashed border-slate-300 bg-white/70 px-5 py-6 text-sm text-slate-600 shadow-lg backdrop-blur-xl">
            Seu histórico ainda não tem aulas registradas.
          </div>
        ) : (
          payload.items.map((item) => (
            <article key={item.id} className="rounded-[28px] p-5 shadow-lg backdrop-blur-2xl" style={{ border: `1px solid ${theme.frame}`, background: theme.cardBackground }}>
              <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                <div>
                  <div className="inline-flex rounded-full px-3 py-1 text-xs uppercase tracking-[0.25em] text-slate-700" style={{ border: `1px solid ${theme.frame}`, backgroundColor: theme.primarySoft }}>
                    {item.timelineLabel}
                  </div>
                  <h3 className="mt-3 text-xl font-semibold text-slate-950">{item.sessionTitle}</h3>
                  <div className="mt-2 text-sm text-slate-700">
                    {item.courseName ?? translateLabel(item.kind)} com {item.instructorName}
                  </div>
                  <div className="mt-2 text-sm text-slate-600">
                    {formatDateTime(item.startAtUtc)} • {formatMinutes(item.durationMinutes)} • {translateLabel(item.status)}
                  </div>
                </div>

                <div className="rounded-[22px] px-4 py-4 text-sm text-slate-700" style={{ border: `1px solid ${theme.frame}`, background: theme.mutedCardBackground }}>
                  <div className="text-xs uppercase tracking-[0.25em] text-slate-500">Leitura da sessão</div>
                  <div className="mt-2 max-w-sm leading-6">{item.statusMessage}</div>
                </div>
              </div>

              <div className="mt-4 grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
                <div className="rounded-[22px] px-4 py-4" style={{ border: `1px solid ${theme.frame}`, background: theme.cardBackground }}>
                  <div className="text-xs uppercase tracking-[0.25em] text-slate-500">Observações e evolução</div>
                  <div className="mt-3 text-sm leading-6 text-slate-700">{item.evolutionSummary}</div>
                  {item.notes ? (
                    <div className="mt-3 text-sm text-slate-600">Observações registradas: {item.notes}</div>
                  ) : null}
                </div>

                <div className="rounded-[22px] px-4 py-4" style={{ border: `1px solid ${theme.frame}`, background: theme.mutedCardBackground }}>
                  <div className="inline-flex items-center gap-2 text-xs uppercase tracking-[0.25em] text-emerald-800">
                    <ShieldCheck size={14} />
                    Presença do aluno
                  </div>
                  <div className="mt-3 text-sm leading-6 text-slate-700">
                    {item.studentConfirmedAtUtc
                      ? `Confirmada em ${formatDateTime(item.studentConfirmedAtUtc)}.`
                      : "Não houve registro de confirmação de presença no portal para esta sessão."}
                  </div>
                  {item.studentConfirmationNote ? (
                    <div className="mt-3 text-sm text-slate-600">Observação do aluno: {item.studentConfirmationNote}</div>
                  ) : null}
                </div>
              </div>
            </article>
          ))
        )}
      </section>
    </div>
  );
}
