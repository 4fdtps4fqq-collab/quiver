import {
  Activity,
  BookOpen,
  CalendarRange,
  CircleDollarSign,
  GraduationCap,
  Home,
  School,
  Users,
  Wrench
} from "lucide-react";
import type { Role } from "../auth/SessionContext";
import { platformPermissions } from "./permissions";

export type NavigationItem = {
  to: string;
  label: string;
  section: string;
  icon: typeof Home;
  roles: Role[];
  requiredPermissions?: string[];
};

export const navigationItems: NavigationItem[] = [
  { to: "/system/schools", label: "Nova escola", section: "Administração", icon: School, roles: ["SystemAdmin"] },
  { to: "/system/schools/directory", label: "Consulta de escolas", section: "Administração", icon: School, roles: ["SystemAdmin"] },
  { to: "/dashboard", label: "Painel", section: "Visão geral", icon: Home, roles: ["Owner", "Admin", "Instructor"], requiredPermissions: [platformPermissions.dashboardView] },
  { to: "/students", label: "Alunos", section: "Acadêmico", icon: Users, roles: ["Owner", "Admin", "Instructor"], requiredPermissions: [platformPermissions.studentsManage] },
  { to: "/courses", label: "Cursos", section: "Acadêmico", icon: BookOpen, roles: ["Owner", "Admin", "Instructor"], requiredPermissions: [platformPermissions.coursesManage] },
  { to: "/enrollments", label: "Matrículas", section: "Acadêmico", icon: GraduationCap, roles: ["Owner", "Admin", "Instructor"], requiredPermissions: [platformPermissions.enrollmentsManage] },
  { to: "/lessons", label: "Agenda", section: "Acadêmico", icon: CalendarRange, roles: ["Owner", "Admin", "Instructor"], requiredPermissions: [platformPermissions.lessonsManage] },
  { to: "/equipment", label: "Equipamentos", section: "Operação", icon: Activity, roles: ["Owner", "Admin", "Instructor"], requiredPermissions: [platformPermissions.equipmentManage] },
  { to: "/maintenance", label: "Manutenção", section: "Operação", icon: Wrench, roles: ["Owner", "Admin", "Instructor"], requiredPermissions: [platformPermissions.maintenanceManage] },
  { to: "/finance", label: "Financeiro", section: "Financeiro", icon: CircleDollarSign, roles: ["Owner", "Admin", "Instructor"], requiredPermissions: [platformPermissions.financeManage] },
  { to: "/school", label: "Escola", section: "Administração", icon: School, roles: ["Owner", "Admin", "Instructor"], requiredPermissions: [platformPermissions.schoolManage] }
];
