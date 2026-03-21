import { Bell, ChevronRight, LogOut } from "lucide-react";
import { useLocation } from "react-router-dom";
import { useSession } from "../auth/SessionContext";
import { navigationItems } from "../lib/navigation";
import { translateLabel } from "../lib/localization";

type RouteHeaderMeta = {
  section: string;
  title: string;
  description: string;
};

const routeMeta: Array<{ match: string; meta: RouteHeaderMeta }> = [
  {
    match: "/system/schools",
    meta: {
      section: "Administração",
      title: "Escolas da plataforma",
      description: "Provisione novas escolas e acompanhe a base ativa do sistema."
    }
  },
  {
    match: "/dashboard",
    meta: {
      section: "Visão geral",
      title: "Painel operacional",
      description: "Acompanhe os indicadores principais da escola em um único lugar."
    }
  },
  {
    match: "/students",
    meta: {
      section: "Acadêmico",
      title: "Alunos",
      description: "Gerencie alunos, contatos e vínculo com o portal da escola."
    }
  },
  {
    match: "/courses",
    meta: {
      section: "Acadêmico",
      title: "Cursos",
      description: "Configure programas por carga horária e valor comercial."
    }
  },
  {
    match: "/enrollments",
    meta: {
      section: "Acadêmico",
      title: "Matrículas",
      description: "Acompanhe saldo em horas, snapshots e ciclo de vida das matrículas."
    }
  },
  {
    match: "/lessons",
    meta: {
      section: "Acadêmico",
      title: "Agenda",
      description: "Visualize aulas, remarcações e execução da rotina da escola."
    }
  },
  {
    match: "/equipment",
    meta: {
      section: "Operação",
      title: "Equipamentos",
      description: "Controle estoque, condição atual e uso acumulado dos ativos."
    }
  },
  {
    match: "/maintenance",
    meta: {
      section: "Operação",
      title: "Manutenção",
      description: "Gerencie alertas, regras preventivas e histórico de serviços."
    }
  },
  {
    match: "/finance",
    meta: {
      section: "Financeiro",
      title: "Financeiro",
      description: "Monitore receitas, despesas e leitura de margem da escola."
    }
  },
  {
    match: "/school",
    meta: {
      section: "Administração",
      title: "Administração da escola",
      description: "Ajuste equipe, permissões, identidade visual e regras operacionais da escola."
    }
  }
];

export function Topbar() {
  const location = useLocation();
  const { user, logout } = useSession();

  const header = resolveHeaderMeta(location.pathname, user?.schoolName ?? "Escola");

  return (
    <div className="mb-8 flex flex-col gap-5 border-b border-[var(--q-divider)] pb-6 lg:flex-row lg:items-start lg:justify-between">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2 text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
          <span>{header.section}</span>
          <ChevronRight size={14} className="text-[var(--q-divider)]" />
          <span className="text-[var(--q-text-2)]">{header.title}</span>
        </div>

        <div className="mt-3 flex flex-wrap items-center gap-3">
          <h1 className="text-3xl font-semibold tracking-tight text-[var(--q-text)] sm:text-4xl">
            {header.title}
          </h1>
        </div>

        <p className="mt-2 max-w-2xl text-sm leading-6 text-[var(--q-text-2)]">
          {header.description}
        </p>
      </div>

      <div className="flex items-center gap-3 self-start">
        <button
          className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface)] p-3 text-[var(--q-text)] transition hover:bg-[var(--q-surface-2)]"
          aria-label="Notificações"
        >
          <Bell size={18} />
        </button>
        <div className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-3 text-right">
          <div className="text-sm font-medium text-[var(--q-text)]">{user?.fullName}</div>
          <div className="text-xs uppercase tracking-[0.25em] text-[var(--q-muted)]">
            {translateLabel(user?.role)}
          </div>
        </div>
        <button
          onClick={logout}
          className="inline-flex items-center gap-2 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-3 text-sm text-[var(--q-text)] transition hover:bg-[var(--q-surface-2)]"
        >
          <LogOut size={16} />
          Sair
        </button>
      </div>
    </div>
  );
}

function resolveHeaderMeta(pathname: string, schoolName: string): RouteHeaderMeta {
  const navigationMatch = navigationItems.find((item) => pathname.startsWith(item.to));
  const matched = routeMeta.find((item) => pathname.startsWith(item.match));
  if (matched) {
    return {
      section: matched.meta.section || navigationMatch?.section || "Operação",
      title: matched.meta.title,
      description: matched.meta.description
    };
  }

  if (navigationMatch) {
    return {
      section: navigationMatch.section,
      title: navigationMatch.label,
      description: `Gerencie ${navigationMatch.label.toLowerCase()} em ${schoolName}.`
    };
  }

  return {
    section: "Operação",
    title: schoolName,
    description: "Acompanhe a rotina da escola com foco em clareza e execução."
  };
}
