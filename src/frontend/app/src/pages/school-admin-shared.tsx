import { formatDate } from "../lib/formatters";
import type { InstructorAvailabilitySlot } from "../lib/platform-api";

export const roleOptions = [
  { value: 5, label: "Administrativo", role: "Admin" },
  { value: 3, label: "Instrutor", role: "Instructor" }
] as const;

export const roleValueToName = {
  5: "Admin",
  3: "Instructor",
  4: "Student"
} as const;

export type SchoolUserRole = (typeof roleValueToName)[keyof typeof roleValueToName];

export function translateRole(role?: string) {
  switch (role) {
    case "Owner":
      return "Proprietário";
    case "Admin":
      return "Administrativo";
    case "Instructor":
      return "Instrutor";
    case "Student":
      return "Aluno";
    default:
      return role ?? "-";
  }
}

export function translateAuditEvent(eventType: string) {
  switch (eventType) {
    case "auth.login":
      return "Login";
    case "auth.refresh":
      return "Renovação de sessão";
    case "auth.logout":
      return "Logout";
    case "auth.change-password":
      return "Troca de senha";
    case "auth.forgot-password":
      return "Solicitação de recuperação";
    case "auth.reset-password":
      return "Redefinição de senha";
    case "identity.invitation.create":
      return "Convite criado";
    case "identity.invitation.accept":
      return "Convite aceito";
    case "identity.invitation.cancel":
      return "Convite cancelado";
    case "identity.user.create":
      return "Colaborador criado";
    case "identity.user.update":
      return "Colaborador atualizado";
    case "identity.user.activation":
      return "Ativação ou desativação";
    case "identity.user.reset-password":
      return "Senha temporária enviada";
    default:
      return eventType;
  }
}

export function summarizeAuditMetadata(metadata: unknown) {
  if (!metadata || typeof metadata !== "object") {
    return "";
  }

  return Object.entries(metadata as Record<string, unknown>)
    .filter(([, value]) => value !== null && value !== undefined && value !== "")
    .slice(0, 4)
    .map(([key, value]) => `${key}: ${String(value)}`)
    .join(" • ");
}

export function defaultAvailability(): InstructorAvailabilitySlot[] {
  return [
    { dayOfWeek: 0, startMinutesUtc: 0, endMinutesUtc: 24 * 60, label: "Domingo" },
    { dayOfWeek: 1, startMinutesUtc: 0, endMinutesUtc: 24 * 60, label: "Segunda" },
    { dayOfWeek: 2, startMinutesUtc: 0, endMinutesUtc: 24 * 60, label: "Terça" },
    { dayOfWeek: 3, startMinutesUtc: 0, endMinutesUtc: 24 * 60, label: "Quarta" },
    { dayOfWeek: 4, startMinutesUtc: 0, endMinutesUtc: 24 * 60, label: "Quinta" },
    { dayOfWeek: 5, startMinutesUtc: 0, endMinutesUtc: 24 * 60, label: "Sexta" },
    { dayOfWeek: 6, startMinutesUtc: 0, endMinutesUtc: 24 * 60, label: "Sábado" }
  ];
}

export function minutesToTime(value: number) {
  const hours = Math.floor(value / 60).toString().padStart(2, "0");
  const minutes = (value % 60).toString().padStart(2, "0");
  return `${hours}:${minutes}`;
}

export function timeToMinutes(value: string) {
  const [hours, minutes] = value.split(":").map(Number);
  if (!Number.isFinite(hours) || !Number.isFinite(minutes)) {
    return 8 * 60;
  }

  return hours * 60 + minutes;
}

export function formatCurrencyInput(value: number) {
  return value.toFixed(2).replace(".", ",");
}

export function formatCurrencyMask(value: string) {
  const digits = value.replace(/\D/g, "");
  if (!digits) {
    return "";
  }

  const integer = digits.slice(0, -2) || "0";
  const cents = digits.slice(-2).padStart(2, "0");
  const normalizedInteger = Number(integer).toLocaleString("pt-BR");
  return `${normalizedInteger},${cents}`;
}

export function parseCurrencyInput(value: string) {
  const normalized = value.replace(/\./g, "").replace(",", ".").trim();
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : 0;
}

export function formatPhone(value: string) {
  const digits = value.replace(/\D/g, "").slice(0, 11);

  if (digits.length <= 2) {
    return digits.length ? `(${digits}` : "";
  }

  if (digits.length <= 7) {
    return `(${digits.slice(0, 2)}) ${digits.slice(2)}`;
  }

  return `(${digits.slice(0, 2)}) ${digits.slice(2, 7)}-${digits.slice(7)}`;
}

export function startOfWeek(value: Date) {
  const normalized = new Date(value.getFullYear(), value.getMonth(), value.getDate());
  const day = normalized.getDay();
  const diff = day === 0 ? -6 : 1 - day;
  normalized.setDate(normalized.getDate() + diff);
  normalized.setHours(0, 0, 0, 0);
  return normalized;
}

export function addDays(value: Date, days: number) {
  const next = new Date(value);
  next.setDate(next.getDate() + days);
  return next;
}

export function toDateInputValue(value: Date) {
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, "0");
  const day = String(value.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export function buildWeekDays(weekStart: Date) {
  return Array.from({ length: 7 }, (_, index) => {
    const date = addDays(weekStart, index);
    return {
      date,
      dateKey: toDateInputValue(date),
      weekdayLabel: new Intl.DateTimeFormat("pt-BR", { weekday: "short" }).format(date).replace(".", ""),
      dateLabel: new Intl.DateTimeFormat("pt-BR", { day: "2-digit", month: "2-digit" }).format(date)
    };
  });
}

export function isSameLocalDay(value: string, date: Date) {
  const parsed = new Date(value);
  return (
    parsed.getFullYear() === date.getFullYear() &&
    parsed.getMonth() === date.getMonth() &&
    parsed.getDate() === date.getDate()
  );
}

export function isWithinWeek(value: string, weekStart: Date) {
  const parsed = new Date(value);
  const from = startOfWeek(weekStart).getTime();
  const to = addDays(startOfWeek(weekStart), 7).getTime();
  return parsed.getTime() >= from && parsed.getTime() < to;
}

export function formatWeekRangeLabel(weekStart: Date) {
  const weekEnd = addDays(weekStart, 6);
  return `${formatDate(weekStart.toISOString())} a ${formatDate(weekEnd.toISOString())}`;
}

export function describeBaseAvailabilityForDay(slots: InstructorAvailabilitySlot[], date: Date) {
  const daySlot = slots.find((item) => item.dayOfWeek === date.getDay());
  if (!daySlot) {
    return "Sem padrão";
  }

  return `${minutesToTime(daySlot.startMinutesUtc)} às ${minutesToTime(daySlot.endMinutesUtc)}`;
}

export function combineDateAndTimeToUtc(dateValue: string, timeValue: string) {
  if (!dateValue || !timeValue) {
    return null;
  }

  const parsed = new Date(`${dateValue}T${timeValue}:00`);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

export function formatBlockWindow(startAtUtc: string, endAtUtc: string) {
  const start = new Date(startAtUtc);
  const end = new Date(endAtUtc);
  return `${start.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" })} às ${end.toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" })}`;
}

export function MetricTile({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface)] p-4">
      <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">{label}</div>
      <div className="mt-3 text-xl font-semibold text-[var(--q-text)]">{value}</div>
    </div>
  );
}

export function TextField({
  label,
  value,
  onChange,
  type = "text",
  required = false,
  disabled = false,
  className,
  placeholder
}: {
  label: string;
  value: string;
  onChange?: (value: string) => void;
  type?: string;
  required?: boolean;
  disabled?: boolean;
  className?: string;
  placeholder?: string;
}) {
  return (
    <label className={`grid gap-2 text-sm text-[var(--q-text)] ${className ?? ""}`.trim()}>
      <span>{label}</span>
      <input
        className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none disabled:cursor-not-allowed disabled:opacity-80"
        value={value}
        onChange={onChange ? (event) => onChange(event.target.value) : undefined}
        type={type}
        required={required}
        disabled={disabled}
        placeholder={placeholder}
      />
    </label>
  );
}

export function NumericField({
  label,
  value,
  onChange
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return <TextField label={label} value={value} onChange={onChange} />;
}

export function TimeField({
  label,
  value,
  onChange
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <label className="grid gap-2 text-sm text-[var(--q-text)]">
      <span>{label}</span>
      <input
        className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
        type="time"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}
