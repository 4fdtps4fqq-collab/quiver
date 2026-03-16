import type { Role, SessionUser } from "../auth/SessionContext";

export const platformPermissions = {
  dashboardView: "dashboard.view",
  studentsManage: "students.manage",
  instructorsManage: "instructors.manage",
  coursesManage: "courses.manage",
  enrollmentsManage: "enrollments.manage",
  lessonsManage: "lessons.manage",
  equipmentManage: "equipment.manage",
  maintenanceManage: "maintenance.manage",
  financeManage: "finance.manage",
  schoolManage: "school.manage"
} as const;

export type PlatformPermission = (typeof platformPermissions)[keyof typeof platformPermissions];

export type PermissionDefinition = {
  key: PlatformPermission;
  label: string;
  description: string;
};

export type PermissionGroup = {
  key: string;
  title: string;
  description: string;
  items: PermissionDefinition[];
};

export const permissionGroups: PermissionGroup[] = [
  {
    key: "overview",
    title: "Visão geral",
    description: "Acesso ao painel consolidado e indicadores da escola.",
    items: [
      {
        key: platformPermissions.dashboardView,
        label: "Painel e indicadores",
        description: "Permite abrir o dashboard operacional da escola."
      }
    ]
  },
  {
    key: "academics",
    title: "Acadêmico",
    description: "Gestão da operação pedagógica e de agenda.",
    items: [
      {
        key: platformPermissions.studentsManage,
        label: "Alunos",
        description: "Permite cadastrar, editar e consultar alunos."
      },
      {
        key: platformPermissions.instructorsManage,
        label: "Instrutores",
        description: "Permite manter o cadastro e o valor da hora/aula dos instrutores."
      },
      {
        key: platformPermissions.coursesManage,
        label: "Cursos",
        description: "Permite criar e editar cursos com carga horária e preço."
      },
      {
        key: platformPermissions.enrollmentsManage,
        label: "Matrículas",
        description: "Permite contratar cursos, acompanhar saldo e encerrar matrículas."
      },
      {
        key: platformPermissions.lessonsManage,
        label: "Agenda e aulas",
        description: "Permite agendar, remarcar, cancelar e realizar aulas."
      }
    ]
  },
  {
    key: "operations",
    title: "Operação",
    description: "Controle do acervo e das rotinas de manutenção.",
    items: [
      {
        key: platformPermissions.equipmentManage,
        label: "Equipamentos",
        description: "Permite manter depósitos, equipamentos e checkout/checkin."
      },
      {
        key: platformPermissions.maintenanceManage,
        label: "Manutenção",
        description: "Permite configurar regras, registrar serviços e acompanhar alertas."
      }
    ]
  },
  {
    key: "finance",
    title: "Financeiro",
    description: "Receitas, despesas, cobranças e inadimplência.",
    items: [
      {
        key: platformPermissions.financeManage,
        label: "Financeiro",
        description: "Permite operar lançamentos, cobranças e acompanhar margem."
      }
    ]
  },
  {
    key: "administration",
    title: "Administração",
    description: "Configurações da escola, usuários e políticas do portal.",
    items: [
      {
        key: platformPermissions.schoolManage,
        label: "Administração da escola",
        description: "Permite gerir usuários, convites e regras da escola."
      }
    ]
  }
];

export const allPlatformPermissions = permissionGroups.flatMap((group) => group.items.map((item) => item.key));

export function getDefaultPermissionsForRole(role: Role): PlatformPermission[] {
  switch (role) {
    case "SystemAdmin":
    case "Owner":
      return [...allPlatformPermissions];
    case "Admin":
      return [
        platformPermissions.dashboardView,
        platformPermissions.studentsManage,
        platformPermissions.instructorsManage,
        platformPermissions.coursesManage,
        platformPermissions.enrollmentsManage,
        platformPermissions.lessonsManage,
        platformPermissions.equipmentManage,
        platformPermissions.maintenanceManage,
        platformPermissions.financeManage,
        platformPermissions.schoolManage
      ];
    case "Instructor":
      return [
        platformPermissions.dashboardView,
        platformPermissions.studentsManage,
        platformPermissions.coursesManage,
        platformPermissions.lessonsManage,
        platformPermissions.equipmentManage,
        platformPermissions.maintenanceManage
      ];
    case "Student":
      return [];
    default:
      return [];
  }
}

export function isPermissionConfigurable(role: Role) {
  return role === "Admin" || role === "Instructor";
}

export function normalizePermissions(permissions: string[] | undefined | null) {
  return Array.from(new Set((permissions ?? []).filter((item): item is PlatformPermission => allPlatformPermissions.includes(item as PlatformPermission)))).sort();
}

export function hasPermissionAccess(
  user: Pick<SessionUser, "role" | "permissions"> | null | undefined,
  requiredPermissions?: string[]
) {
  if (!user) {
    return false;
  }

  if (user.role === "SystemAdmin" || user.role === "Owner") {
    return true;
  }

  if (!requiredPermissions || requiredPermissions.length === 0) {
    return true;
  }

  return requiredPermissions.every((permission) => user.permissions.includes(permission));
}
