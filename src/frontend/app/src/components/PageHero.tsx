type PageHeroProps = {
  eyebrow?: string;
  title: string;
  description?: string;
  stats: Array<{ label: string; value: string }>;
  statsBelow?: boolean;
};

export function PageHero({ eyebrow, title, description, stats, statsBelow = false }: PageHeroProps) {
  return (
    <section className="rounded-[28px] border border-[var(--q-border)] bg-[var(--q-grad-ocean)] p-6 shadow-[var(--app-shadow)]">
      {eyebrow ? (
        <div className="text-xs uppercase tracking-[0.45em] text-[var(--q-aqua)]">{eyebrow}</div>
      ) : null}
      <div className={`${eyebrow ? "mt-4" : ""} grid gap-8 ${statsBelow ? "" : "lg:grid-cols-[1.4fr_0.9fr]"}`}>
        <div>
          <h2 className="text-3xl font-semibold tracking-tight text-[var(--q-text)]">{title}</h2>
          {description ? (
            <p className="mt-4 max-w-2xl text-sm leading-6 text-[var(--q-text-2)]">{description}</p>
          ) : null}
        </div>

        <div className={`grid grid-cols-2 gap-3 ${statsBelow ? "sm:grid-cols-4" : ""}`}>
          {stats.map((stat) => (
            <div key={stat.label} className="rounded-3xl border border-[var(--q-border)] bg-[var(--q-surface)] p-4 shadow-[var(--app-shadow-soft)]">
              <div className="text-xs uppercase tracking-[0.3em] text-[var(--q-muted)]">{stat.label}</div>
              <div className="mt-3 text-2xl font-semibold text-[var(--q-text)]">{stat.value}</div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
