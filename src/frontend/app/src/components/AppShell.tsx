import { Outlet, useLocation } from "react-router-dom";
import { PasswordPolicyBanner } from "./PasswordPolicyBanner";
import { Sidebar } from "./Sidebar";
import { Topbar } from "./Topbar";

export function AppShell() {
  const location = useLocation();

  return (
    <div className="min-h-screen bg-[var(--app-bg)] text-[var(--q-text)]">
      <div className="pointer-events-none fixed inset-0 overflow-hidden">
        <div className="absolute -left-24 top-0 h-80 w-80 rounded-full bg-[var(--glow-primary)] blur-3xl" />
        <div className="absolute right-0 top-32 h-96 w-96 rounded-full bg-[var(--glow-secondary)] blur-3xl" />
        <div className="absolute bottom-0 left-1/3 h-72 w-72 rounded-full bg-[var(--glow-tertiary)] blur-3xl" />
      </div>

      <div className="relative mx-auto flex min-h-screen max-w-[1600px] gap-6 px-4 py-4 sm:px-6 lg:px-8">
        <Sidebar currentPath={location.pathname} />

        <main className="flex-1 rounded-[32px] border border-[var(--q-border)] bg-[var(--app-shell)] p-4 shadow-[var(--app-shadow)] backdrop-blur-2xl sm:p-6">
          <>
            <Topbar />
            <PasswordPolicyBanner />
            <Outlet />
          </>
        </main>
      </div>
    </div>
  );
}
