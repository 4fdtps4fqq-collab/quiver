import { useEffect, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { translateLabel } from "../lib/localization";

type CardProps = {
  title?: string;
  description?: string;
  aside?: ReactNode;
  children: ReactNode;
  className?: string;
};

export function GlassCard({ title, description, aside, children, className }: CardProps) {
  return (
    <section
      className={`rounded-[28px] border border-[var(--q-border)] bg-[var(--q-surface)] p-5 shadow-[var(--app-shadow-soft)] backdrop-blur-xl ${className ?? ""}`.trim()}
    >
      {(title || description || aside) && (
        <header className="mb-5 flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-2">
            {title ? <h3 className="text-lg font-semibold tracking-tight text-[var(--q-text)]">{title}</h3> : null}
            {description ? <p className="max-w-2xl text-sm leading-6 text-[var(--q-text-2)]">{description}</p> : null}
          </div>
          {aside ? <div>{aside}</div> : null}
        </header>
      )}
      {children}
    </section>
  );
}

export function StatGrid({
  items
}: {
  items: Array<{ label: string; value: string; tone?: "cyan" | "amber" | "emerald" }>;
}) {
  return (
    <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      {items.map((item) => (
        <article
          key={item.label}
          className="rounded-[24px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4"
          data-tone={item.tone ?? "cyan"}
        >
          <div className="text-xs uppercase tracking-[0.28em] text-[var(--q-muted)]">{item.label}</div>
          <div className="mt-3 text-2xl font-semibold text-[var(--q-text)]">{item.value}</div>
        </article>
      ))}
    </div>
  );
}

export function StatusBadge({ value }: { value?: string | number | boolean | null }) {
  const safeValue = String(value ?? "-").trim() || "-";
  const normalized = safeValue.toLowerCase();
  const tone =
    normalized.includes("realized") || normalized.includes("active") || normalized.includes("excellent") || normalized.includes("enabled")
      ? "border-[var(--q-success)]/30 bg-[var(--q-success-bg)] text-[var(--q-success)]"
      : normalized.includes("cancel") || normalized.includes("repair") || normalized.includes("out")
        ? "border-[var(--q-danger)]/30 bg-[var(--q-danger-bg)] text-[var(--q-danger)]"
        : normalized.includes("attention") || normalized.includes("wind") || normalized.includes("expire")
          ? "border-[var(--q-warning)]/30 bg-[var(--q-warning-bg)] text-[#B58100]"
          : "border-[var(--q-info)]/30 bg-[var(--q-info-bg)] text-[var(--q-info)]";

  return (
    <span className={`inline-flex rounded-full border px-3 py-1 text-xs uppercase tracking-[0.24em] ${tone}`}>
      {translateLabel(safeValue)}
    </span>
  );
}

export function LoadingBlock({ label = "Carregando dados" }: { label?: string }) {
  return (
    <div className="rounded-[24px] border border-dashed border-[var(--q-divider)] bg-[var(--q-surface-2)] px-4 py-8 text-center text-sm uppercase tracking-[0.28em] text-[var(--q-text-2)]">
      {label}
    </div>
  );
}

export function ErrorBlock({ message }: { message: string }) {
  const [dismissed, setDismissed] = useState(false);

  useEffect(() => {
    setDismissed(false);
  }, [message]);

  if (!message || dismissed || typeof document === "undefined") {
    return null;
  }

  return createPortal(
    <div className="fixed inset-0 z-[140] flex items-center justify-center bg-slate-950/30 px-4 py-6 backdrop-blur-[2px]">
      <div className="w-full max-w-md rounded-[28px] border border-[var(--q-danger)]/30 bg-[var(--q-surface)] p-5 shadow-[0_28px_70px_rgba(15,23,42,0.28)]">
        <div className="flex items-start justify-between gap-4">
          <div>
            <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-danger)]">Atenção</div>
            <div className="mt-3 text-sm leading-6 text-[var(--q-text)]">{message}</div>
          </div>
          <button
            type="button"
            onClick={() => setDismissed(true)}
            className="inline-flex h-9 w-9 items-center justify-center rounded-full border border-[var(--q-border)] bg-[var(--q-surface-2)] text-lg text-[var(--q-text-2)] transition hover:opacity-85"
            aria-label="Fechar mensagem"
          >
            ×
          </button>
        </div>
        <div className="mt-5 flex justify-end">
          <button
            type="button"
            onClick={() => setDismissed(true)}
            className="rounded-2xl bg-[var(--q-navy)] px-4 py-2.5 text-sm font-medium text-white transition hover:opacity-95"
          >
            Fechar
          </button>
        </div>
      </div>
    </div>,
    document.body
  );
}
