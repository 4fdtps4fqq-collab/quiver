import { useEffect, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { getAuthenticationAuditEvents, type AuthenticationAuditEvent } from "../lib/auth-api";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock, StatusBadge } from "../components/OperationsUi";
import { summarizeAuditMetadata, translateAuditEvent } from "./school-admin-shared";

export function SchoolAuditPage() {
  const { token } = useSession();
  const [auditEvents, setAuditEvents] = useState<AuthenticationAuditEvent[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

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
      const payload = await getAuthenticationAuditEvents(currentToken, 48);
      setAuditEvents(payload);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar a auditoria.");
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <div className="space-y-6">
      <PageHero
        title="Auditoria de autenticação e acessos"
        description="Acompanhe logins, trocas de senha, convites e movimentações relevantes das contas da escola."
        stats={[
          { label: "Eventos", value: String(auditEvents.length) },
          { label: "Logins", value: String(auditEvents.filter((item) => item.eventType === "auth.login").length) },
          { label: "Convites", value: String(auditEvents.filter((item) => item.eventType.startsWith("identity.invitation")).length) },
          { label: "Senhas", value: String(auditEvents.filter((item) => item.eventType.includes("password")).length) }
        ]}
        statsBelow
      />

      {isLoading ? <LoadingBlock label="Carregando auditoria" /> : null}
      {error ? <ErrorBlock message={error} /> : null}

      <GlassCard title="Eventos recentes" description="Base pronta para MFA futuro e governança de acesso da escola.">
        <div className="space-y-3">
          {auditEvents.length === 0 ? (
            <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
              Ainda não há eventos registrados para esta escola.
            </div>
          ) : (
            auditEvents.map((event) => (
              <div key={event.id} className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="text-sm font-semibold text-[var(--q-text)]">{translateAuditEvent(event.eventType)}</div>
                  <StatusBadge value={event.outcome} />
                </div>
                <div className="mt-2 text-sm text-[var(--q-text-2)]">
                  {event.email || "Conta interna"} • {new Date(event.createdAtUtc).toLocaleString("pt-BR")}
                </div>
                {event.metadata ? (
                  <div className="mt-2 text-xs leading-5 text-[var(--q-muted)]">{summarizeAuditMetadata(event.metadata)}</div>
                ) : null}
              </div>
            ))
          )}
        </div>
      </GlassCard>
    </div>
  );
}
