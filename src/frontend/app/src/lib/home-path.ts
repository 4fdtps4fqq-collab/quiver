import type { SessionUser } from "../auth/SessionContext";
import { navigationItems } from "./navigation";
import { hasPermissionAccess } from "./permissions";

export function resolveHomePath(user: Pick<SessionUser, "role" | "permissions"> | null | undefined) {
  if (!user) {
    return "/login";
  }

  if (user.role === "Student") {
    return "/student";
  }

  if (user.role === "SystemAdmin") {
    return "/system/schools";
  }

  const firstAllowedItem = navigationItems.find(
    (item) => item.roles.includes(user.role) && hasPermissionAccess(user, item.requiredPermissions)
  );

  return firstAllowedItem?.to ?? "/login";
}
