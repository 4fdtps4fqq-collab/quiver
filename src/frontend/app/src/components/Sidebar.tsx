import { NavLink } from "react-router-dom";
import { useSession } from "../auth/SessionContext";
import { navigationItems } from "../lib/navigation";
import { hasPermissionAccess } from "../lib/permissions";

export function Sidebar({ currentPath }: { currentPath: string }) {
  const { user, school } = useSession();

  const items = navigationItems.filter(
    (item) => user && item.roles.includes(user.role) && hasPermissionAccess(user, item.requiredPermissions)
  );
  const sections = items.reduce<Array<{ section: string; items: typeof items }>>((accumulator, item) => {
    const currentSection = accumulator.find((entry) => entry.section === item.section);
    if (currentSection) {
      currentSection.items.push(item);
      return accumulator;
    }

    accumulator.push({
      section: item.section,
      items: [item]
    });
    return accumulator;
  }, []);

  return (
    <aside className="hidden w-[256px] shrink-0 rounded-[32px] border border-[var(--q-border)] bg-[var(--app-shell-strong)] p-5 shadow-[var(--app-shadow-soft)] backdrop-blur-2xl lg:block">
      <div className="mb-8 flex justify-start px-3">
        <img
          src={school?.logoDataUrl || "/branding/logo.png"}
          alt={school?.displayName ? `Logo da escola ${school.displayName}` : "Quiver Kite Experience"}
          className="h-auto w-[150px] object-contain"
        />
      </div>

      <nav className="space-y-6">
        {sections.map((section) => (
          <div key={section.section}>
            <div className="mb-2 px-4 text-[11px] font-medium uppercase tracking-[0.24em] text-[var(--q-muted)]">
              {section.section}
            </div>
            <div className="space-y-2">
              {section.items.map((item) => {
                const Icon = item.icon;
                const active = currentPath.startsWith(item.to);

                return (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    className={`flex items-center gap-3 rounded-2xl px-4 py-3 text-sm transition ${
                      active
                        ? "bg-[var(--q-info-bg)] text-[var(--q-text)] shadow-[inset_0_0_0_1px_var(--q-border)]"
                        : "text-[var(--q-text-2)] hover:bg-[var(--q-surface-2)] hover:text-[var(--q-text)]"
                    }`}
                  >
                    <Icon size={18} />
                    <span>{item.label}</span>
                  </NavLink>
                );
              })}
            </div>
          </div>
        ))}
      </nav>
    </aside>
  );
}
