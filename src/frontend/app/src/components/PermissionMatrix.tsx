import type { Role } from "../auth/SessionContext";
import {
  isPermissionConfigurable,
  permissionGroups,
  type PlatformPermission
} from "../lib/permissions";

type PermissionMatrixProps = {
  role: Role;
  value: string[];
  onChange: (nextPermissions: PlatformPermission[]) => void;
};

export function PermissionMatrix({ role, value, onChange }: PermissionMatrixProps) {
  const disabled = !isPermissionConfigurable(role);
  const forceAllChecked = role === "Owner";

  function togglePermission(permission: PlatformPermission) {
    if (disabled) {
      return;
    }

    const nextPermissions = value.includes(permission)
      ? value.filter((item) => item !== permission)
      : [...value, permission];

    onChange(nextPermissions as PlatformPermission[]);
  }

  return (
    <div className="rounded-[24px] border border-[var(--q-border)] bg-[var(--q-surface)] p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="text-sm font-semibold text-[var(--q-text)]">Permissões por funcionalidade</div>
          <p className="mt-1 text-sm leading-6 text-[var(--q-text-2)]">
            Defina exatamente quais módulos esse usuário pode acessar dentro da escola.
          </p>
        </div>
        <div className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface-2)] px-3 py-1 text-xs font-medium uppercase tracking-[0.18em] text-[var(--q-muted)]">
          {role === "Owner"
            ? "Acesso total pelo papel"
            : role === "Student"
              ? "Portal do aluno"
              : "Permissões configuráveis"}
        </div>
      </div>

      {role === "Owner" ? (
        <div className="mt-4 rounded-[20px] border border-[var(--q-success)]/20 bg-[var(--q-success-bg)] px-4 py-3 text-sm text-[var(--q-text)]">
          O proprietário sempre mantém acesso total ao backoffice da escola.
        </div>
      ) : null}

      {role === "Student" ? (
        <div className="mt-4 rounded-[20px] border border-[var(--q-info)]/20 bg-[var(--q-info-bg)] px-4 py-3 text-sm text-[var(--q-text)]">
          O papel de aluno usa apenas o portal do aluno e não recebe permissões do backoffice.
        </div>
      ) : null}

      <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        {permissionGroups.map((group) => (
          <div key={group.key} className="rounded-[20px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
            <div className="text-sm font-semibold text-[var(--q-text)]">{group.title}</div>
            <p className="mt-1 text-sm leading-6 text-[var(--q-text-2)]">{group.description}</p>

            <div className="mt-4 space-y-3">
              {group.items.map((item) => {
                const checked = forceAllChecked || value.includes(item.key);

                return (
                  <label
                    key={item.key}
                    className={`flex items-start gap-3 rounded-[18px] border px-3 py-3 text-sm ${
                      checked
                        ? "border-[var(--q-info)]/30 bg-white"
                        : "border-[var(--q-border)] bg-[var(--q-surface)]"
                    } ${disabled ? "opacity-75" : ""}`}
                  >
                    <input
                      type="checkbox"
                      checked={checked}
                      disabled={disabled}
                      onChange={() => togglePermission(item.key)}
                      className="mt-1 h-4 w-4 rounded border-[var(--q-border)] text-[var(--q-aqua)]"
                    />
                    <span className="min-w-0">
                      <span className="block font-medium text-[var(--q-text)]">{item.label}</span>
                      <span className="mt-1 block leading-5 text-[var(--q-text-2)]">{item.description}</span>
                    </span>
                  </label>
                );
              })}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
