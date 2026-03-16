import { BellRing, Compass, LayoutDashboard, LogOut, ScrollText, UserRound, Waves } from "lucide-react";
import { NavLink, Outlet } from "react-router-dom";
import { useSession } from "../auth/SessionContext";
import { resolveStudentPortalTheme } from "../lib/student-portal-theme";
export function StudentPortalShell() {
  const { user, school, logout } = useSession();
  const theme = resolveStudentPortalTheme(school);
  const navigation = [
    { to: "/student", label: "Visão geral", icon: LayoutDashboard },
    { to: "/student/history", label: "Histórico", icon: ScrollText },
    { to: "/student/notifications", label: "Notificações", icon: BellRing },
    { to: "/student/profile", label: "Perfil", icon: UserRound }
  ];

  return (
    <div className="min-h-screen text-slate-950" style={{ background: theme.shellBackground }}>
      <div className="pointer-events-none fixed inset-0 overflow-hidden">
        <div className="absolute left-[-8rem] top-[-2rem] h-72 w-72 rounded-full blur-3xl" style={{ backgroundColor: theme.primarySoft }} />
        <div className="absolute right-[-6rem] top-24 h-80 w-80 rounded-full blur-3xl" style={{ backgroundColor: theme.accentSoft }} />
        <div className="absolute bottom-[-6rem] left-1/3 h-96 w-96 rounded-full bg-white/40 blur-3xl" />
      </div>

      <div className="relative mx-auto max-w-7xl px-4 py-5 sm:px-6 lg:px-8">
        <header
          className="rounded-[30px] px-5 py-5 text-white shadow-2xl backdrop-blur-2xl sm:px-7"
          style={{ border: `1px solid ${theme.frame}`, background: `linear-gradient(135deg, ${theme.primary} 0%, rgba(15,23,42,0.88) 58%, ${theme.accentSoft} 100%)` }}
        >
          <div className="flex flex-col gap-5 lg:flex-row lg:items-center lg:justify-between">
            <div>
              <div className="inline-flex items-center gap-2 rounded-full px-3 py-1 text-xs uppercase tracking-[0.35em]" style={{ border: `1px solid ${theme.accentSoft}`, backgroundColor: theme.accentSoft, color: "white" }}>
                <Waves size={14} />
                Portal do aluno
              </div>
              <div className="mt-4">
                <img
                  src={school?.logoDataUrl || "/branding/logo-transparent.png"}
                  alt={school?.displayName ? `Logo da escola ${school.displayName}` : "Quiver Kite Experience"}
                  className="h-auto w-[160px] object-contain"
                />
              </div>
              <h1 className="mt-4 text-3xl font-semibold tracking-tight">Sua jornada na água, com agenda, progresso e autonomia</h1>
              <p className="mt-3 max-w-3xl text-sm leading-6 text-slate-300">
                Acompanhe sua evolução, confirme presença, remarque quando precisar e mantenha o ritmo do treinamento sem depender da equipe operacional.
              </p>
              <div className="mt-4 flex flex-wrap gap-3 text-sm text-slate-100/90">
                <div className="rounded-2xl px-4 py-3" style={{ backgroundColor: theme.primarySoft, border: `1px solid ${theme.frame}` }}>
                  Escola: <span className="font-medium text-white">{school?.displayName ?? user?.schoolName}</span>
                </div>
                <div className="rounded-2xl px-4 py-3" style={{ backgroundColor: "rgba(255,255,255,0.08)", border: `1px solid ${theme.frame}` }}>
                  Regras do portal sincronizadas com a escola
                </div>
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-3">
              <div className="rounded-2xl px-4 py-3" style={{ border: `1px solid ${theme.frame}`, backgroundColor: "rgba(255,255,255,0.08)" }}>
                <div className="text-xs uppercase tracking-[0.25em] text-cyan-100/70">Aluno logado</div>
                <div className="mt-1 text-sm font-medium text-white">{user?.fullName}</div>
              </div>
              <div className="rounded-2xl px-4 py-3" style={{ border: `1px solid ${theme.frame}`, backgroundColor: "rgba(255,255,255,0.08)" }}>
                <div className="text-xs uppercase tracking-[0.25em] text-cyan-100/70">Área</div>
                <div className="mt-1 inline-flex items-center gap-2 text-sm font-medium text-white">
                  <Compass size={16} />
                  Portal do aluno
                </div>
              </div>
              <button
                onClick={logout}
                className="inline-flex items-center gap-2 rounded-2xl px-4 py-3 text-sm text-white transition hover:bg-white/14"
                style={{ border: `1px solid ${theme.frame}`, backgroundColor: "rgba(255,255,255,0.08)" }}
              >
                <LogOut size={16} />
                Sair
              </button>
            </div>
          </div>

          <nav className="mt-6 hidden gap-3 md:flex">
            {navigation.map(({ to, label, icon: Icon }) => (
              <NavLink
                key={to}
                to={to}
                end={to === "/student"}
                className={({ isActive }) =>
                  `inline-flex items-center gap-2 rounded-2xl px-4 py-3 text-sm transition ${isActive ? "text-slate-950" : "text-slate-200 hover:bg-white/10"}`
                }
                style={({ isActive }) => ({
                  border: `1px solid ${theme.frame}`,
                  background: isActive ? `linear-gradient(135deg, white 0%, ${theme.accentSoft} 100%)` : "rgba(255,255,255,0.05)"
                })}
              >
                <Icon size={16} />
                {label}
              </NavLink>
            ))}
          </nav>
        </header>

        <main className="pb-24 pt-6 md:pb-6">
          <Outlet />
        </main>

        <nav className="fixed inset-x-4 bottom-4 z-20 grid grid-cols-4 gap-2 rounded-[28px] border border-white/40 bg-slate-950/88 p-2 shadow-2xl backdrop-blur-2xl md:hidden">
            {navigation.map(({ to, label, icon: Icon }) => (
              <NavLink
                key={to}
                to={to}
                end={to === "/student"}
                className={({ isActive }) =>
                  `flex flex-col items-center justify-center gap-1 rounded-2xl px-2 py-3 text-[11px] uppercase tracking-[0.18em] transition ${
                    isActive ? "bg-white text-slate-950" : "text-slate-200"
                  }`
                }
                style={({ isActive }) => isActive ? { background: `linear-gradient(135deg, white 0%, ${theme.accentSoft} 100%)` } : undefined}
              >
                <Icon size={16} />
                <span>{label}</span>
            </NavLink>
          ))}
        </nav>
      </div>
    </div>
  );
}
