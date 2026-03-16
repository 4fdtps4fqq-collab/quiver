type SectionCardProps = {
  title: string;
  description: string;
  metric: string;
};

export function SectionCard({ title, description, metric }: SectionCardProps) {
  return (
    <article className="rounded-[26px] border border-[var(--q-border)] bg-[var(--q-surface)] p-5 backdrop-blur-xl transition hover:-translate-y-0.5 hover:bg-[var(--q-surface-2)]">
      <div className="text-xs uppercase tracking-[0.3em] text-[var(--q-aqua)]">Capacidade</div>
      <h3 className="mt-4 text-xl font-semibold text-[var(--q-text)]">{title}</h3>
      <p className="mt-3 text-sm leading-6 text-[var(--q-text-2)]">{description}</p>
      <div className="mt-6 inline-flex rounded-full border border-[var(--q-success)]/25 bg-[var(--q-success-bg)] px-3 py-1 text-xs uppercase tracking-[0.25em] text-[var(--q-success)]">
        {metric}
      </div>
    </article>
  );
}
