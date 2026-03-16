import type { ReactElement } from "react";
import { Navigate } from "react-router-dom";
import { useSession, type Role } from "../auth/SessionContext";
import { resolveHomePath } from "../lib/home-path";
import { hasPermissionAccess } from "../lib/permissions";

export function ProtectedRoute({
  children,
  allowedRoles,
  requiredPermissions
}: {
  children: ReactElement;
  allowedRoles?: Role[];
  requiredPermissions?: string[];
}) {
  const { user, isLoading } = useSession();

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-[var(--app-bg)] text-sm uppercase tracking-[0.4em] text-cyan-100">
        Carregando sessão
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login" replace />;
  }

  if (allowedRoles && !allowedRoles.includes(user.role)) {
    return <Navigate to={resolveHomePath(user)} replace />;
  }

  if (!hasPermissionAccess(user, requiredPermissions)) {
    return <Navigate to={resolveHomePath(user)} replace />;
  }

  return children;
}
