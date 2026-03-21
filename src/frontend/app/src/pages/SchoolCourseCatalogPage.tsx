import { useEffect, useMemo, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock } from "../components/OperationsUi";
import {
  deleteCourseLevelSetting,
  getCourseLevelSettings,
  upsertCourseLevelSetting,
  type CourseLevelCatalogResponse
} from "../lib/platform-api";

type TrackFormItem = {
  id?: string;
  title: string;
  focus: string;
  weightPercent: string;
};

type CatalogForm = {
  id?: string;
  levelValue: string;
  name: string;
  isActive: boolean;
  pedagogicalTrack: TrackFormItem[];
};

const blankTrack = (): TrackFormItem[] => [{ title: "", focus: "", weightPercent: "100" }];

function createBlankForm(levelValue = 2): CatalogForm {
  return {
    levelValue: String(levelValue),
    name: "",
    isActive: true,
    pedagogicalTrack: blankTrack()
  };
}

export function SchoolCourseCatalogPage() {
  const { token } = useSession();
  const [catalog, setCatalog] = useState<CourseLevelCatalogResponse>({ availableLevels: [], items: [] });
  const [selectedSettingId, setSelectedSettingId] = useState<string | null>(null);
  const [form, setForm] = useState<CatalogForm>(createBlankForm());
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const selectedSetting = useMemo(
    () => catalog.items.find((item) => item.id === selectedSettingId) ?? null,
    [catalog.items, selectedSettingId]
  );

  useEffect(() => {
    if (!token) {
      return;
    }

    void loadCatalog(token);
  }, [token]);

  useEffect(() => {
    if (!selectedSetting) {
      return;
    }

    setForm({
      id: selectedSetting.id,
      levelValue: String(selectedSetting.levelValue),
      name: selectedSetting.name,
      isActive: selectedSetting.isActive,
      pedagogicalTrack: selectedSetting.pedagogicalTrack.map((item) => ({
        id: item.id,
        title: item.title,
        focus: item.focus,
        weightPercent: String(item.weightPercent)
      }))
    });
  }, [selectedSetting]);

  async function loadCatalog(currentToken: string, preferredSettingId?: string | null) {
    try {
      setIsLoading(true);
      setError(null);
      const response = await getCourseLevelSettings(currentToken);
      response.availableLevels.sort((left, right) => left.sortOrder - right.sortOrder);
      response.items.sort((left, right) => left.levelValue - right.levelValue || left.sortOrder - right.sortOrder || left.name.localeCompare(right.name));
      setCatalog(response);

      if (response.items.length > 0) {
        setSelectedSettingId((current) => {
          const targetId = preferredSettingId ?? current;
          return response.items.some((item) => item.id === targetId) ? targetId ?? response.items[0].id : response.items[0].id;
        });
      } else {
        resetForm(response);
      }
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar as trilhas pedagógicas.");
    } finally {
      setIsLoading(false);
    }
  }

  function resetForm(source = catalog) {
    const firstLevel = source.availableLevels[0]?.levelValue ?? 2;
    setSelectedSettingId(null);
    setForm(createBlankForm(firstLevel));
    setError(null);
    setNotice(null);
  }

  function updateTrackItem(index: number, patch: Partial<TrackFormItem>) {
    setForm((current) => ({
      ...current,
      pedagogicalTrack: current.pedagogicalTrack.map((item, itemIndex) =>
        itemIndex === index ? { ...item, ...patch } : item)
    }));
  }

  function addTrackItem() {
    setForm((current) => ({
      ...current,
      pedagogicalTrack: [
        ...current.pedagogicalTrack,
        { title: "", focus: "", weightPercent: "0" }
      ]
    }));
  }

  function removeTrackItem(index: number) {
    setForm((current) => ({
      ...current,
      pedagogicalTrack: current.pedagogicalTrack.filter((_, itemIndex) => itemIndex !== index)
    }));
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSaving(true);
      setError(null);
      setNotice(null);

      const response = await upsertCourseLevelSetting(token, {
        id: form.id,
        levelValue: Number(form.levelValue),
        name: form.name,
        isActive: form.isActive,
        pedagogicalTrack: form.pedagogicalTrack.map((item) => ({
          id: item.id,
          title: item.title,
          focus: item.focus,
          weightPercent: Number(item.weightPercent)
        }))
      });

      setNotice(selectedSetting ? "Trilha pedagógica atualizada com sucesso." : "Trilha pedagógica cadastrada com sucesso.");
      await loadCatalog(token, response.settingId);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar a trilha pedagógica.");
    } finally {
      setIsSaving(false);
    }
  }

  async function handleInactivate() {
    if (!token || !selectedSetting) {
      return;
    }

    try {
      setIsSaving(true);
      setError(null);
      setNotice(null);

      const response = await upsertCourseLevelSetting(token, {
        id: selectedSetting.id,
        levelValue: Number(form.levelValue),
        name: form.name,
        isActive: false,
        pedagogicalTrack: form.pedagogicalTrack.map((item) => ({
          id: item.id,
          title: item.title,
          focus: item.focus,
          weightPercent: Number(item.weightPercent)
        }))
      });

      setNotice("Trilha pedagógica inativada com sucesso.");
      await loadCatalog(token, response.settingId);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível inativar a trilha pedagógica.");
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDelete() {
    if (!token || !selectedSetting) {
      return;
    }

    try {
      setIsDeleting(true);
      setError(null);
      setNotice(null);
      await deleteCourseLevelSetting(token, selectedSetting.id);
      setNotice("Trilha pedagógica excluída com sucesso.");
      await loadCatalog(token);
      resetForm();
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível excluir a trilha pedagógica.");
    } finally {
      setIsDeleting(false);
    }
  }

  const totalWeight = form.pedagogicalTrack.reduce((sum, item) => sum + (Number(item.weightPercent) || 0), 0);
  const canDelete = selectedSetting !== null;
  const canInactivate = selectedSetting !== null && form.isActive;

  return (
    <div className="space-y-6">
      <PageHero
        title="Trilhas pedagógicas por nível"
        description="Cadastre quantas trilhas forem necessárias para cada nível. Depois, escolha a trilha exata no cadastro do curso."
        stats={[
          { label: "Trilhas", value: String(catalog.items.length) },
          { label: "Ativas", value: String(catalog.items.filter((item) => item.isActive).length) },
          { label: "Níveis", value: String(new Set(catalog.items.map((item) => item.levelValue)).size) },
          { label: "Módulos", value: String(form.pedagogicalTrack.length) }
        ]}
        statsBelow
      />

      {isLoading ? <LoadingBlock label="Carregando trilhas pedagógicas" /> : null}
      {error ? <ErrorBlock message={error} /> : null}
      {notice ? <div className="rounded-[24px] border border-[var(--q-info)]/30 bg-[var(--q-info-bg)] px-5 py-4 text-sm text-[var(--q-info)]">{notice}</div> : null}

      <GlassCard title="Trilhas cadastradas" description="Cada card representa uma trilha pedagógica. Você pode ter várias trilhas dentro do mesmo nível.">
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {catalog.items.map((item) => (
            <button
              key={item.id}
              type="button"
              onClick={() => setSelectedSettingId(item.id)}
              className={`rounded-[22px] border px-4 py-4 text-left transition ${
                selectedSettingId === item.id
                  ? "border-[var(--q-info)]/40 bg-[var(--q-info-bg)]"
                  : "border-[var(--q-border)] bg-[var(--q-surface-2)] hover:bg-[var(--q-info-bg)]/45"
              }`}
            >
              <div className="flex items-start justify-between gap-3">
                <div>
                  <div className="text-xs uppercase tracking-[0.22em] text-[var(--q-muted)]">
                    {catalog.availableLevels.find((level) => level.levelValue === item.levelValue)?.name ?? `Nível ${item.levelValue}`}
                  </div>
                  <div className="mt-1 text-sm font-semibold text-[var(--q-text)]">{item.name}</div>
                </div>
                <div className={`rounded-full px-3 py-1 text-[11px] uppercase tracking-[0.18em] ${item.isActive ? "border border-[var(--q-success)]/30 bg-[var(--q-success-bg)] text-[var(--q-success)]" : "border border-[var(--q-warning)]/30 bg-[var(--q-warning-bg)] text-[var(--q-text)]"}`}>
                  {item.isActive ? "Ativa" : "Inativa"}
                </div>
              </div>
              <div className="mt-3 text-xs text-[var(--q-text-2)]">{item.pedagogicalTrack.length} módulos configurados</div>
            </button>
          ))}
        </div>
      </GlassCard>

      <GlassCard
        title={selectedSetting ? `Editar trilha · ${selectedSetting.name}` : "Nova trilha pedagógica"}
        description="Defina o nível macro e, dentro dele, a trilha específica que será usada no curso."
      >
        <form className="space-y-6" onSubmit={handleSubmit}>
          <div className="grid gap-4 md:grid-cols-[220px_minmax(0,1fr)]">
            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Nível</span>
              <select
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                value={form.levelValue}
                onChange={(event) => setForm((current) => ({ ...current, levelValue: event.target.value }))}
              >
                {catalog.availableLevels.map((item) => (
                  <option key={item.levelValue} value={item.levelValue}>
                    {item.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Nome da trilha</span>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                value={form.name}
                onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))}
                placeholder="Ex.: Básico Wave, Básico Kids, Básico Freestyle"
                required
              />
            </label>
          </div>

          <div className="rounded-[24px] border border-[var(--q-border)] bg-[var(--q-surface)] p-4">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <div className="text-sm font-semibold text-[var(--q-text)]">Módulos da trilha</div>
                <p className="mt-1 text-sm leading-6 text-[var(--q-text-2)]">
                  Monte a trilha pedagógica completa. A soma dos percentuais deve fechar em 100%.
                </p>
              </div>
              <div className={`rounded-full border px-3 py-1 text-xs uppercase tracking-[0.18em] ${totalWeight === 100 ? "border-[var(--q-success)]/30 bg-[var(--q-success-bg)] text-[var(--q-success)]" : "border-[var(--q-warning)]/30 bg-[var(--q-warning-bg)] text-[var(--q-text)]"}`}>
                Total {totalWeight}%
              </div>
            </div>

            <div className="mt-4 space-y-4">
              {form.pedagogicalTrack.map((item, index) => (
                <div key={item.id ?? `module-${index}`} className="rounded-[20px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                  <div className="grid gap-4 lg:grid-cols-[1fr_1fr_140px_auto]">
                    <label className="grid gap-2 text-sm text-[var(--q-text)]">
                      <span>Título do módulo</span>
                      <input
                        className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                        value={item.title}
                        onChange={(event) => updateTrackItem(index, { title: event.target.value })}
                        required
                      />
                    </label>
                    <label className="grid gap-2 text-sm text-[var(--q-text)]">
                      <span>Foco pedagógico</span>
                      <input
                        className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                        value={item.focus}
                        onChange={(event) => updateTrackItem(index, { focus: event.target.value })}
                      />
                    </label>
                    <label className="grid gap-2 text-sm text-[var(--q-text)]">
                      <span>Peso (%)</span>
                      <input
                        className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                        type="number"
                        min="1"
                        max="100"
                        value={item.weightPercent}
                        onChange={(event) => updateTrackItem(index, { weightPercent: event.target.value })}
                        required
                      />
                    </label>
                    <div className="flex items-end">
                      <button
                        className="rounded-full border border-[var(--q-danger)]/35 bg-[var(--q-danger-bg)] px-4 py-3 text-sm font-medium text-[var(--q-danger)] transition hover:opacity-90"
                        type="button"
                        onClick={() => removeTrackItem(index)}
                        disabled={form.pedagogicalTrack.length <= 1}
                      >
                        Remover
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>

            <button
              className="mt-4 rounded-full border border-[var(--q-border)] bg-[var(--q-surface)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-text)] transition hover:bg-[var(--q-surface-2)]"
              type="button"
              onClick={addTrackItem}
            >
              Adicionar módulo
            </button>
          </div>

          <div className="flex flex-wrap gap-3">
            <button
              className="rounded-full border border-transparent px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95"
              style={{ backgroundImage: "var(--q-grad-brand)", backgroundColor: "var(--q-navy)" }}
              type="submit"
              disabled={isSaving || isDeleting}
            >
              {isSaving ? "Salvando" : "Salvar"}
            </button>
            <button
              className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-text)] transition hover:bg-[var(--q-surface-2)]"
              type="button"
              onClick={() => resetForm()}
              disabled={isSaving || isDeleting}
            >
              Limpar
            </button>
            <button
              className="rounded-full border border-[var(--q-warning)]/40 bg-[var(--q-warning-bg)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-text)] transition hover:opacity-95 disabled:opacity-60"
              type="button"
              onClick={() => void handleInactivate()}
              disabled={!canInactivate || isSaving || isDeleting}
            >
              Inativar
            </button>
            <button
              className="rounded-full border border-[var(--q-danger)]/40 bg-[var(--q-danger-bg)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-danger)] transition hover:opacity-90 disabled:opacity-60"
              type="button"
              onClick={() => void handleDelete()}
              disabled={!canDelete || isSaving || isDeleting}
            >
              {isDeleting ? "Excluindo" : "Excluir"}
            </button>
          </div>
        </form>
      </GlassCard>
    </div>
  );
}
