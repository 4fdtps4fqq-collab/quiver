import type { PageSection } from "../lib/page-data";
import { PageHero } from "./PageHero";
import { SectionCard } from "./SectionCard";

type PageTemplateProps = {
  eyebrow: string;
  title: string;
  description: string;
  stats: Array<{ label: string; value: string }>;
  sections: PageSection[];
};

export function PageTemplate({
  eyebrow,
  title,
  description,
  stats,
  sections
}: PageTemplateProps) {
  return (
    <div className="space-y-6">
      <PageHero eyebrow={eyebrow} title={title} description={description} stats={stats} />
      <section className="grid gap-4 xl:grid-cols-2">
        {sections.map((section) => (
          <SectionCard key={section.title} {...section} />
        ))}
      </section>
    </div>
  );
}
