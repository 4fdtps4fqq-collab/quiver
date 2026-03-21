import { useEffect, useMemo, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { createScheduleBlock, deleteScheduleBlock, getInstructors, getScheduleBlocks, type Instructor } from "../lib/platform-api";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock } from "../components/OperationsUi";
import {
  addDays,
  buildWeekDays,
  combineDateAndTimeToUtc,
  formatBlockWindow,
  formatWeekRangeLabel,
  isSameLocalDay,
  isWithinWeek,
  startOfWeek,
  TextField,
  TimeField,
  toDateInputValue
} from "./school-admin-shared";

const CALENDAR_START_HOUR = 9;
const CALENDAR_END_HOUR = 18;
const HOUR_ROW_HEIGHT = 56;

export function SchoolInstructorSchedulePage() {
  const { token } = useSession();
  const [instructors, setInstructors] = useState<Instructor[]>([]);
  const [scheduleBlocks, setScheduleBlocks] = useState<Awaited<ReturnType<typeof getScheduleBlocks>>>([]);
  const [selectedInstructorId, setSelectedInstructorId] = useState("");
  const [weekStart, setWeekStart] = useState(() => startOfWeek(new Date()));
  const [viewMode, setViewMode] = useState<"week" | "day">("week");
  const [selectedDate, setSelectedDate] = useState(() => startOfWeek(new Date()));
  const [blockForm, setBlockForm] = useState(() => ({
    date: toDateInputValue(new Date()),
    startTime: "08:00",
    endTime: "18:00",
    title: "Indisponível",
    notes: ""
  }));
  const [isLoading, setIsLoading] = useState(true);
  const [isSavingBlock, setIsSavingBlock] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const selectedInstructor = instructors.find((item) => item.id === selectedInstructorId) ?? null;
  const weekDays = useMemo(() => buildWeekDays(weekStart), [weekStart]);
  const visibleDays = useMemo(
    () => (viewMode === "day" ? [{ date: selectedDate, dateKey: toDateInputValue(selectedDate), weekdayLabel: new Intl.DateTimeFormat("pt-BR", { weekday: "short" }).format(selectedDate).replace(".", ""), dateLabel: new Intl.DateTimeFormat("pt-BR", { day: "2-digit", month: "2-digit" }).format(selectedDate) }] : weekDays),
    [selectedDate, viewMode, weekDays]
  );
  const instructorBlocks = useMemo(
    () =>
      selectedInstructor
        ? scheduleBlocks
            .filter((item) => item.scope === "Instructor" && item.instructorId === selectedInstructor.id)
            .sort((left, right) => left.startAtUtc.localeCompare(right.startAtUtc))
        : [],
    [scheduleBlocks, selectedInstructor]
  );
  const blocksInWeek = useMemo(
    () => instructorBlocks.filter((item) => isWithinWeek(item.startAtUtc, weekStart)),
    [instructorBlocks, weekStart]
  );
  const timeSlots = useMemo(
    () => Array.from({ length: CALENDAR_END_HOUR - CALENDAR_START_HOUR + 1 }, (_, index) => CALENDAR_START_HOUR + index),
    []
  );

  useEffect(() => {
    if (!token) {
      return;
    }
    void loadData(token);
  }, [token]);

  useEffect(() => {
    setBlockForm((current) => ({ ...current, date: toDateInputValue(weekStart) }));
    if (!isSameCalendarDate(selectedDate, weekStart) && viewMode === "week") {
      setSelectedDate(weekStart);
    }
  }, [weekStart]);

  async function loadData(currentToken: string) {
    try {
      setIsLoading(true);
      setError(null);
      const [instructorsData, blocksData] = await Promise.all([
        getInstructors(currentToken),
        getScheduleBlocks(currentToken)
      ]);
      setInstructors(instructorsData);
      setScheduleBlocks(blocksData);
      setSelectedInstructorId((current) => instructorsData.some((item) => item.id === current) ? current : instructorsData[0]?.id ?? "");
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar a agenda dos instrutores.");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleQuickBlock(date: Date, hour: number, minute: number) {
    if (!token || !selectedInstructor) {
      return;
    }

    try {
      setIsSavingBlock(true);
      setError(null);
      setNotice(null);
      const start = new Date(date);
      start.setHours(hour, minute, 0, 0);
      const end = new Date(start);
      end.setMinutes(end.getMinutes() + 60);

      await createScheduleBlock(token, {
        scope: 2,
        instructorId: selectedInstructor.id,
        title: blockForm.title.trim() || "Indisponível",
        notes: blockForm.notes.trim() || undefined,
        startAtUtc: start.toISOString(),
        endAtUtc: end.toISOString()
      });

      setBlockForm((current) => ({
        ...current,
        date: toDateInputValue(date),
        startTime: `${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")}`,
        endTime: toTimeValue(end)
      }));
      setNotice(`Bloqueio rápido criado em ${toDateInputValue(date)} às ${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")}.`);
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível criar o bloqueio rápido.");
    } finally {
      setIsSavingBlock(false);
    }
  }

  async function handleCreateBlock() {
    if (!token || !selectedInstructor) {
      return;
    }

    try {
      setIsSavingBlock(true);
      setError(null);
      setNotice(null);
      const startAtUtc = combineDateAndTimeToUtc(blockForm.date, blockForm.startTime);
      const endAtUtc = combineDateAndTimeToUtc(blockForm.date, blockForm.endTime);
      if (!startAtUtc || !endAtUtc || endAtUtc <= startAtUtc) {
        setError("Defina uma data e um intervalo válido para o bloqueio.");
        return;
      }

      await createScheduleBlock(token, {
        scope: 2,
        instructorId: selectedInstructor.id,
        title: blockForm.title.trim() || "Indisponível",
        notes: blockForm.notes.trim() || undefined,
        startAtUtc: startAtUtc.toISOString(),
        endAtUtc: endAtUtc.toISOString()
      });
      setNotice("Bloqueio salvo na agenda do instrutor.");
      setBlockForm((current) => ({ ...current, notes: "" }));
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar o bloqueio.");
    } finally {
      setIsSavingBlock(false);
    }
  }

  async function handleDeleteBlock(blockId: string) {
    if (!token) {
      return;
    }

    try {
      setError(null);
      setNotice(null);
      await deleteScheduleBlock(token, blockId);
      setNotice("Bloqueio removido da agenda do instrutor.");
      await loadData(token);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível remover o bloqueio.");
    }
  }

  return (
    <div className="space-y-6">
      <PageHero
        title="Agenda e disponibilidade dos instrutores"
        description="Todos os horários da semana nascem disponíveis. Aqui você só informa as indisponibilidades reais do instrutor, por data."
        stats={[
          { label: "Instrutores", value: String(instructors.length) },
          { label: "Ativos", value: String(instructors.filter((item) => item.isActive).length) },
          { label: "Semana", value: formatWeekRangeLabel(weekStart) },
          { label: "Bloqueios", value: String(blocksInWeek.length) }
        ]}
        statsBelow
      />

      {isLoading ? <LoadingBlock label="Carregando agenda dos instrutores" /> : null}
      {error ? <ErrorBlock message={error} /> : null}
      {notice ? <div className="rounded-[24px] border border-[var(--q-info)]/30 bg-[var(--q-info-bg)] px-5 py-4 text-sm text-[var(--q-info)]">{notice}</div> : null}

      <GlassCard title="Selecionar instrutor" description="Escolha o instrutor que você quer operar nesta semana.">
        <div className="grid gap-4 md:grid-cols-[1fr_auto_auto_auto_auto] md:items-end">
          <label className="grid gap-2 text-sm text-[var(--q-text)]">
            <span>Instrutor</span>
            <select className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none" value={selectedInstructorId} onChange={(event) => setSelectedInstructorId(event.target.value)}>
              <option value="">Selecione o instrutor</option>
              {instructors.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.fullName}
                </option>
              ))}
            </select>
          </label>
          <button className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-3 text-xs uppercase tracking-[0.2em] text-[var(--q-text)]" type="button" onClick={() => setWeekStart((current) => addDays(current, -7))}>
            Semana anterior
          </button>
          <button className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-3 text-xs uppercase tracking-[0.2em] text-[var(--q-text)]" type="button" onClick={() => setWeekStart(startOfWeek(new Date()))}>
            Semana atual
          </button>
          <button className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-3 text-xs uppercase tracking-[0.2em] text-[var(--q-text)]" type="button" onClick={() => setWeekStart((current) => addDays(current, 7))}>
            Próxima semana
          </button>
          <div className="flex rounded-full border border-[var(--q-border)] bg-[var(--q-surface)] p-1">
            <button className={`rounded-full px-4 py-2 text-xs uppercase tracking-[0.2em] ${viewMode === "week" ? "bg-[var(--q-navy)] text-white" : "text-[var(--q-text)]"}`} type="button" onClick={() => setViewMode("week")}>
              Semana
            </button>
            <button className={`rounded-full px-4 py-2 text-xs uppercase tracking-[0.2em] ${viewMode === "day" ? "bg-[var(--q-navy)] text-white" : "text-[var(--q-text)]"}`} type="button" onClick={() => setViewMode("day")}>
              Dia
            </button>
          </div>
        </div>
      </GlassCard>

      {selectedInstructor ? (
        <GlassCard title="Semana operacional do instrutor" description="Visualize cada dia da semana e bloqueie indisponibilidades reais por data.">
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div className="text-sm font-medium text-[var(--q-text)]">
              {viewMode === "week"
                ? formatWeekRangeLabel(weekStart)
                : new Intl.DateTimeFormat("pt-BR", { dateStyle: "full" }).format(selectedDate)}
            </div>
            {viewMode === "day" ? (
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Data</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  type="date"
                  value={toDateInputValue(selectedDate)}
                  onChange={(event) => setSelectedDate(new Date(`${event.target.value}T00:00:00`))}
                />
              </label>
            ) : null}
          </div>
          <div className="space-y-4">
            <div className="overflow-x-auto rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface)] p-4">
              <div className={viewMode === "day" ? "min-w-[440px]" : "min-w-[1040px]"}>
                <div
                  className="grid gap-0"
                  style={{ gridTemplateColumns: `88px repeat(${visibleDays.length}, minmax(0, 1fr))` }}
                >
                  <div className="border-b border-[var(--q-border)] bg-[var(--q-surface)]" />
                  {visibleDays.map((day) => (
                    <div key={day.dateKey} className="border-b border-l border-[var(--q-border)] bg-[var(--q-surface-2)] px-3 py-3">
                      <div className="text-[11px] uppercase tracking-[0.22em] text-[var(--q-muted)]">{day.weekdayLabel}</div>
                      <div className="mt-1 text-sm font-semibold text-[var(--q-text)]">{day.dateLabel}</div>
                      <div className="mt-2 text-xs text-[var(--q-success)]">Disponível até surgir um bloqueio</div>
                    </div>
                  ))}
                </div>

                <div
                  className="grid"
                  style={{ gridTemplateColumns: `88px repeat(${visibleDays.length}, minmax(0, 1fr))` }}
                >
                  <div
                    className="relative bg-[var(--q-surface)]"
                    style={{ height: `${(CALENDAR_END_HOUR - CALENDAR_START_HOUR) * HOUR_ROW_HEIGHT}px` }}
                  >
                    {timeSlots.slice(0, -1).map((hour) => (
                      <div
                        key={hour}
                        className="absolute inset-x-0 border-t border-[var(--q-border)] px-3 text-xs text-[var(--q-muted)]"
                        style={{ top: `${(hour - CALENDAR_START_HOUR) * HOUR_ROW_HEIGHT}px` }}
                      >
                        <div
                          className="flex h-full -translate-y-1/2 items-center bg-[var(--q-surface)] pr-2"
                          style={{ height: `${HOUR_ROW_HEIGHT}px` }}
                        >
                          {`${String(hour).padStart(2, "0")}:00`}
                        </div>
                      </div>
                    ))}
                  </div>

                  {visibleDays.map((day) => {
                    const dayBlocks = blocksInWeek.filter((block) => isSameLocalDay(block.startAtUtc, day.date));

                    return (
                      <div
                        key={day.dateKey}
                        className="relative border-l border-[var(--q-border)] bg-[var(--q-surface)]"
                        style={{ height: `${(CALENDAR_END_HOUR - CALENDAR_START_HOUR) * HOUR_ROW_HEIGHT}px` }}
                      >
                        <div className="absolute inset-x-0 top-0 h-[33.333%] bg-[linear-gradient(180deg,rgba(194,234,255,0.28),rgba(194,234,255,0.12))]" />
                        <div className="absolute inset-x-0 top-[33.333%] h-[33.333%] bg-[linear-gradient(180deg,rgba(255,228,179,0.20),rgba(255,228,179,0.08))]" />
                        <div className="absolute inset-x-0 bottom-0 h-[33.333%] bg-[linear-gradient(180deg,rgba(207,214,255,0.16),rgba(207,214,255,0.07))]" />

                        {timeSlots.slice(0, -1).map((hour) => (
                          <div
                            key={`${day.dateKey}-${hour}`}
                            className="absolute inset-x-0 border-t border-[var(--q-border)]/80"
                            style={{ top: `${(hour - CALENDAR_START_HOUR) * HOUR_ROW_HEIGHT}px` }}
                          />
                        ))}

                        {Array.from({ length: (CALENDAR_END_HOUR - CALENDAR_START_HOUR) * 2 }, (_, index) => {
                          const slotMinutes = index * 30;
                          const hour = CALENDAR_START_HOUR + Math.floor(slotMinutes / 60);
                          const minute = slotMinutes % 60;

                          return (
                            <button
                              key={`${day.dateKey}-slot-${index}`}
                              className="absolute inset-x-0 z-[1] border-t border-transparent transition hover:bg-[rgba(46,212,167,0.12)]"
                              style={{
                                top: `${(slotMinutes / 60) * HOUR_ROW_HEIGHT}px`,
                                height: `${HOUR_ROW_HEIGHT / 2}px`
                              }}
                              type="button"
                              title={`Bloquear ${String(hour).padStart(2, "0")}:${String(minute).padStart(2, "0")} por 1h`}
                              onClick={() => void handleQuickBlock(day.date, hour, minute)}
                            />
                          );
                        })}

                        {dayBlocks.map((block) => (
                          <button
                            key={block.id}
                            className="absolute left-2 right-2 z-[2] overflow-hidden rounded-2xl border border-[var(--q-danger)]/30 bg-[var(--q-danger-bg)] px-3 py-2 text-left shadow-[0_10px_22px_rgba(180,38,38,0.08)]"
                            style={buildBlockStyle(block.startAtUtc, block.endAtUtc)}
                            type="button"
                            onClick={() => void handleDeleteBlock(block.id)}
                            title="Clique para remover o bloqueio"
                          >
                            <div className="text-[11px] uppercase tracking-[0.2em] text-[var(--q-danger)]">Bloqueado</div>
                            <div className="mt-1 text-xs font-semibold text-[var(--q-text)]">{formatBlockWindow(block.startAtUtc, block.endAtUtc)}</div>
                            <div className="mt-1 text-xs text-[var(--q-text-2)]">{block.title}</div>
                          </button>
                        ))}

                        {dayBlocks.length === 0 ? (
                          <div className="absolute inset-x-3 top-3 rounded-xl border border-dashed border-[var(--q-success)]/20 bg-[var(--q-success-bg)]/70 px-3 py-2 text-[11px] text-[var(--q-success)]">
                            Disponível
                          </div>
                        ) : null}
                      </div>
                    );
                  })}
                </div>
              </div>
            </div>

            <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface)] p-4">
              <div className="text-sm font-medium text-[var(--q-text)]">Bloquear horário</div>
              <div className="mt-1 text-xs text-[var(--q-text-2)]">
                Clique direto na grade para criar um bloqueio rápido de 1 hora, ou use este formulário para lançar um bloqueio customizado. As cores da grade diferenciam manhã, tarde e noite.
              </div>
              <div className="mt-4 grid gap-3">
                <TextField label="Data" type="date" value={blockForm.date} onChange={(value) => setBlockForm((current) => ({ ...current, date: value }))} />
                <div className="grid gap-3 sm:grid-cols-2">
                  <TimeField label="Início" value={blockForm.startTime} onChange={(value) => setBlockForm((current) => ({ ...current, startTime: value }))} />
                  <TimeField label="Fim" value={blockForm.endTime} onChange={(value) => setBlockForm((current) => ({ ...current, endTime: value }))} />
                </div>
                <TextField label="Título" value={blockForm.title} onChange={(value) => setBlockForm((current) => ({ ...current, title: value }))} placeholder="Ex.: Atendimento externo" />
                <TextField label="Observação" value={blockForm.notes} onChange={(value) => setBlockForm((current) => ({ ...current, notes: value }))} placeholder="Motivo do bloqueio" />
                <button className="rounded-full border border-transparent px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95" style={{ backgroundImage: "var(--q-grad-brand)", backgroundColor: "var(--q-navy)" }} type="button" onClick={() => void handleCreateBlock()} disabled={isSavingBlock}>
                  {isSavingBlock ? "Salvando" : "Bloquear horário"}
                </button>
              </div>
            </div>
          </div>
        </GlassCard>
      ) : (
        <GlassCard title="Agenda do instrutor" description="Selecione um instrutor para abrir o calendário semanal.">
          <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
            Escolha um instrutor no topo da página para começar.
          </div>
        </GlassCard>
      )}
    </div>
  );
}

function isSameCalendarDate(left: Date, right: Date) {
  return (
    left.getFullYear() === right.getFullYear() &&
    left.getMonth() === right.getMonth() &&
    left.getDate() === right.getDate()
  );
}

function buildBlockStyle(startAtUtc: string, endAtUtc: string) {
  const start = new Date(startAtUtc);
  const end = new Date(endAtUtc);
  const startMinutes = clampMinutes(start.getHours() * 60 + start.getMinutes());
  const endMinutes = clampMinutes(end.getHours() * 60 + end.getMinutes());
  return {
    top: `${((startMinutes / 60) - CALENDAR_START_HOUR) * HOUR_ROW_HEIGHT}px`,
    height: `${Math.max(((endMinutes - startMinutes) / 60) * HOUR_ROW_HEIGHT, 42)}px`
  };
}

function clampMinutes(value: number) {
  const min = CALENDAR_START_HOUR * 60;
  const max = CALENDAR_END_HOUR * 60;
  return Math.min(Math.max(value, min), max);
}

function toTimeValue(value: Date) {
  return `${String(value.getHours()).padStart(2, "0")}:${String(value.getMinutes()).padStart(2, "0")}`;
}
