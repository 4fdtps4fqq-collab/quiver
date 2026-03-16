import { type FormEvent, useEffect, useMemo, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock, StatusBadge } from "../components/OperationsUi";
import { formatCurrency, formatDateTime } from "../lib/formatters";
import {
  batchRescheduleLessons,
  createLesson,
  createScheduleBlock,
  deleteScheduleBlock,
  getAssistedLessonSuggestions,
  getEnrollments,
  getInstructors,
  getLessons,
  getScheduleBlocks,
  getStudents,
  markLessonNoShow,
  operationalConfirmLesson,
  updateLesson,
  type AssistedLessonSuggestion,
  type Enrollment,
  type Instructor,
  type Lesson,
  type ScheduleBlock,
  type Student
} from "../lib/platform-api";

const statusOptions = [{ value: "1", label: "Agendada" }, { value: "2", label: "Confirmada" }, { value: "3", label: "Realizada" }, { value: "4", label: "Remarcada" }, { value: "5", label: "Cancelada" }, { value: "6", label: "Cancelada por vento" }, { value: "7", label: "Não compareceu" }];
const kindOptions = [{ value: "1", label: "Avulsa" }, { value: "2", label: "Curso" }];
const scopeOptions = [{ value: "1", label: "Escola inteira" }, { value: "2", label: "Instrutor específico" }];
const initialForm = { studentId: "", instructorId: "", kind: "1", status: "1", enrollmentId: "", singleLessonPrice: "", startAtUtc: "", durationMinutes: "90", notes: "" };
const initialBlock = { scope: "1", instructorId: "", title: "", notes: "", startAtUtc: "", endAtUtc: "" };

export function LessonsPage() {
  const { token } = useSession();
  const [students, setStudents] = useState<Student[]>([]);
  const [instructors, setInstructors] = useState<Instructor[]>([]);
  const [enrollments, setEnrollments] = useState<Enrollment[]>([]);
  const [lessons, setLessons] = useState<Lesson[]>([]);
  const [blocks, setBlocks] = useState<ScheduleBlock[]>([]);
  const [warnings, setWarnings] = useState<string[]>([]);
  const [form, setForm] = useState(initialForm);
  const [blockForm, setBlockForm] = useState(initialBlock);
  const [selectedLessonId, setSelectedLessonId] = useState("");
  const [selectedStatus, setSelectedStatus] = useState("1");
  const [selectedLessonIds, setSelectedLessonIds] = useState<string[]>([]);
  const [operationNote, setOperationNote] = useState("");
  const [batchStartAtUtc, setBatchStartAtUtc] = useState("");
  const [batchInstructorId, setBatchInstructorId] = useState("");
  const [suggestions, setSuggestions] = useState<AssistedLessonSuggestion[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false);
  const [isApplyingOperation, setIsApplyingOperation] = useState(false);
  const [isLoadingSuggestions, setIsLoadingSuggestions] = useState(false);
  const [isBatchRescheduling, setIsBatchRescheduling] = useState(false);
  const [isSavingBlock, setIsSavingBlock] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const selectedLesson = useMemo(() => lessons.find((item) => item.id === selectedLessonId) ?? null, [lessons, selectedLessonId]);
  const availableEnrollments = useMemo(() => enrollments.filter((item) => item.studentId === form.studentId && item.status === "Active"), [enrollments, form.studentId]);

  useEffect(() => { if (token) { void loadData(token); } }, [token]);

  async function loadData(currentToken: string) {
    try {
      setIsLoading(true); setError(null);
      const [studentsResult, instructorsResult, enrollmentsResult, lessonsResult, blocksResult] = await Promise.allSettled([getStudents(currentToken), getInstructors(currentToken), getEnrollments(currentToken), getLessons(currentToken), getScheduleBlocks(currentToken)]);
      const nextWarnings: string[] = [];
      if (studentsResult.status === "fulfilled") setStudents(studentsResult.value); else { setStudents([]); nextWarnings.push("Leitura de alunos indisponível para este perfil."); }
      if (instructorsResult.status === "fulfilled") setInstructors(instructorsResult.value); else { setInstructors([]); nextWarnings.push("Leitura de instrutores indisponível para este perfil."); }
      if (enrollmentsResult.status === "fulfilled") setEnrollments(enrollmentsResult.value); else { setEnrollments([]); nextWarnings.push("Leitura de matrículas indisponível para este perfil."); }
      if (lessonsResult.status === "rejected") throw lessonsResult.reason;
      setLessons(lessonsResult.value);
      if (blocksResult.status === "fulfilled") setBlocks(blocksResult.value); else { setBlocks([]); nextWarnings.push("Bloqueios indisponíveis no momento."); }
      setWarnings(nextWarnings);
      const nextSelected = lessonsResult.value.some((item) => item.id === selectedLessonId) ? selectedLessonId : lessonsResult.value[0]?.id ?? "";
      setSelectedLessonId(nextSelected);
      const nextLesson = lessonsResult.value.find((item) => item.id === nextSelected);
      if (nextLesson) setSelectedStatus(String(statusToValue(nextLesson.status)));
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar a agenda.");
    } finally { setIsLoading(false); }
  }

  async function handleCreateLesson(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!token) return;
    try {
      setIsSaving(true); setError(null);
      await createLesson(token, { studentId: form.studentId, instructorId: form.instructorId, kind: Number(form.kind), status: Number(form.status), enrollmentId: Number(form.kind) === 2 ? form.enrollmentId || null : null, singleLessonPrice: Number(form.kind) === 1 && form.singleLessonPrice ? Number(form.singleLessonPrice) : null, startAtUtc: new Date(form.startAtUtc).toISOString(), durationMinutes: Number(form.durationMinutes), notes: form.notes || undefined });
      setForm(initialForm); setSuggestions([]); await loadData(token);
    } catch (nextError) { setError(nextError instanceof Error ? nextError.message : "Não foi possível criar a aula."); } finally { setIsSaving(false); }
  }

  async function handleUpdateStatus() {
    if (!token || !selectedLesson) return;
    try {
      setIsUpdatingStatus(true); setError(null);
      await updateLesson(token, selectedLesson.id, { studentId: selectedLesson.studentId, instructorId: selectedLesson.instructorId, kind: selectedLesson.kind === "Course" ? 2 : 1, status: Number(selectedStatus), enrollmentId: selectedLesson.enrollmentId ?? null, singleLessonPrice: selectedLesson.singleLessonPrice ?? null, startAtUtc: selectedLesson.startAtUtc, durationMinutes: selectedLesson.durationMinutes, notes: selectedLesson.notes || undefined });
      await loadData(token);
    } catch (nextError) { setError(nextError instanceof Error ? nextError.message : "Não foi possível atualizar o status."); } finally { setIsUpdatingStatus(false); }
  }

  async function handleOperationalConfirm() {
    if (!token || !selectedLesson) return;
    try { setIsApplyingOperation(true); setError(null); await operationalConfirmLesson(token, selectedLesson.id, { note: operationNote || undefined }); setOperationNote(""); await loadData(token); } catch (nextError) { setError(nextError instanceof Error ? nextError.message : "Não foi possível confirmar a aula."); } finally { setIsApplyingOperation(false); }
  }

  async function handleNoShow() {
    if (!token || !selectedLesson) return;
    try { setIsApplyingOperation(true); setError(null); await markLessonNoShow(token, selectedLesson.id, { note: operationNote || undefined }); setOperationNote(""); await loadData(token); } catch (nextError) { setError(nextError instanceof Error ? nextError.message : "Não foi possível marcar o no-show."); } finally { setIsApplyingOperation(false); }
  }

  async function handleLoadSuggestions() {
    if (!token || !selectedLesson) return;
    try { setIsLoadingSuggestions(true); setError(null); const response = await getAssistedLessonSuggestions(token, selectedLesson.id, { startSearchAtUtc: selectedLesson.startAtUtc, daysToSearch: 7, instructorId: batchInstructorId || null }); setSuggestions(response.slots); } catch (nextError) { setError(nextError instanceof Error ? nextError.message : "Não foi possível buscar sugestões."); } finally { setIsLoadingSuggestions(false); }
  }

  async function handleApplySuggestion(slot: AssistedLessonSuggestion) {
    if (!token || !selectedLesson) return;
    try { setIsBatchRescheduling(true); setError(null); await batchRescheduleLessons(token, { lessonIds: [selectedLesson.id], newStartAtUtc: slot.startAtUtc, instructorId: slot.instructorId }); setSuggestions([]); await loadData(token); } catch (nextError) { setError(nextError instanceof Error ? nextError.message : "Não foi possível aplicar a sugestão."); } finally { setIsBatchRescheduling(false); }
  }

  async function handleBatchReschedule() {
    if (!token || selectedLessonIds.length === 0 || !batchStartAtUtc) return;
    try { setIsBatchRescheduling(true); setError(null); await batchRescheduleLessons(token, { lessonIds: selectedLessonIds, newStartAtUtc: new Date(batchStartAtUtc).toISOString(), instructorId: batchInstructorId || null }); setBatchStartAtUtc(""); setSelectedLessonIds([]); await loadData(token); } catch (nextError) { setError(nextError instanceof Error ? nextError.message : "Não foi possível remarcar o lote."); } finally { setIsBatchRescheduling(false); }
  }

  async function handleCreateBlock(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!token) return;
    try { setIsSavingBlock(true); setError(null); await createScheduleBlock(token, { scope: Number(blockForm.scope), instructorId: blockForm.scope === "2" ? blockForm.instructorId || null : null, title: blockForm.title, notes: blockForm.notes || undefined, startAtUtc: new Date(blockForm.startAtUtc).toISOString(), endAtUtc: new Date(blockForm.endAtUtc).toISOString() }); setBlockForm(initialBlock); await loadData(token); } catch (nextError) { setError(nextError instanceof Error ? nextError.message : "Não foi possível criar o bloqueio."); } finally { setIsSavingBlock(false); }
  }

  async function handleDeleteBlock(blockId: string) {
    if (!token) return;
    try { setError(null); await deleteScheduleBlock(token, blockId); await loadData(token); } catch (nextError) { setError(nextError instanceof Error ? nextError.message : "Não foi possível remover o bloqueio."); }
  }

  return (
    <div className="space-y-6">
      <PageHero eyebrow="Agenda" title="Agenda operacional com disponibilidade real, bloqueios e remarcação assistida." description="Planeje aulas com validação automática de conflitos, confirme operação, trate no-show e use sugestões de reagendamento para manter a rotina fluindo." stats={[{ label: "Hoje", value: String(lessons.filter((item) => isSameDay(item.startAtUtc)).length) }, { label: "Realizadas", value: String(lessons.filter((item) => item.status === "Realized").length) }, { label: "Confirmadas", value: String(lessons.filter((item) => item.operationalConfirmedAtUtc).length) }, { label: "No-show", value: String(lessons.filter((item) => item.status === "NoShow").length) }]} />
      {isLoading ? <LoadingBlock label="Carregando agenda" /> : null}
      {error ? <ErrorBlock message={error} /> : null}
      {warnings.length > 0 ? <GlassCard title="Leitura parcial da agenda">{warnings.map((warning) => <div key={warning} className="mb-2 rounded-2xl border border-[var(--q-warning)]/30 bg-[var(--q-warning-bg)] px-4 py-3 text-sm text-[#B58100] last:mb-0">{warning}</div>)}</GlassCard> : null}

      <div className="grid gap-4 xl:grid-cols-[0.9fr_1.1fr]">
        <GlassCard title="Nova aula" description="A criação já considera disponibilidade, buffers e bloqueios configurados para a escola.">
          <form className="grid gap-4" onSubmit={handleCreateLesson}>
            <SelectField label="Aluno" value={form.studentId} onChange={(value) => setForm((current) => ({ ...current, studentId: value, enrollmentId: "" }))} options={[{ value: "", label: "Selecione o aluno" }, ...students.map((student) => ({ value: student.id, label: student.fullName }))]} required />
            <SelectField label="Instrutor" value={form.instructorId} onChange={(value) => setForm((current) => ({ ...current, instructorId: value }))} options={[{ value: "", label: "Selecione o instrutor" }, ...instructors.map((instructor) => ({ value: instructor.id, label: instructor.fullName }))]} required />
            <div className="grid gap-4 md:grid-cols-2"><SelectField label="Tipo de aula" value={form.kind} onChange={(value) => setForm((current) => ({ ...current, kind: value, enrollmentId: "", singleLessonPrice: "" }))} options={kindOptions} /><SelectField label="Status inicial" value={form.status} onChange={(value) => setForm((current) => ({ ...current, status: value }))} options={statusOptions} /></div>
            {Number(form.kind) === 2 ? <SelectField label="Matrícula" value={form.enrollmentId} onChange={(value) => setForm((current) => ({ ...current, enrollmentId: value }))} options={[{ value: "", label: "Selecione a matrícula" }, ...availableEnrollments.map((item) => ({ value: item.id, label: `${item.courseName} · ${item.currentModule}` }))]} required /> : <InputField label="Preço da aula avulsa" value={form.singleLessonPrice} onChange={(value) => setForm((current) => ({ ...current, singleLessonPrice: value }))} type="number" min="0" step="0.01" />}
            <div className="grid gap-4 md:grid-cols-[1.2fr_0.8fr]"><InputField label="Início" value={form.startAtUtc} onChange={(value) => setForm((current) => ({ ...current, startAtUtc: value }))} type="datetime-local" required /><InputField label="Duração em minutos" value={form.durationMinutes} onChange={(value) => setForm((current) => ({ ...current, durationMinutes: value }))} type="number" min="30" step="15" required /></div>
            <InputField label="Observações" value={form.notes} onChange={(value) => setForm((current) => ({ ...current, notes: value }))} />
            <button className="rounded-full border border-transparent bg-[var(--q-grad-brand)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white" type="submit" disabled={isSaving || students.length === 0 || instructors.length === 0}>{isSaving ? "Criando aula" : "Criar aula"}</button>
          </form>
        </GlassCard>

        <GlassCard title="Agenda do período" description="Selecione uma aula para operar confirmação, no-show e reagendamento.">
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
              <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]"><tr><th className="pb-3 pr-3">Lote</th><th className="pb-3 pr-3">Início</th><th className="pb-3 pr-3">Aluno</th><th className="pb-3 pr-3">Instrutor</th><th className="pb-3 pr-3">Tipo</th><th className="pb-3 pr-3">Status</th><th className="pb-3">Operação</th></tr></thead>
              <tbody>{lessons.map((lesson) => <tr key={lesson.id} className={`border-t border-[var(--q-border)] ${lesson.id === selectedLessonId ? "bg-[var(--q-info-bg)]" : ""}`}><td className="py-3 pr-3"><input type="checkbox" checked={selectedLessonIds.includes(lesson.id)} onChange={(event) => setSelectedLessonIds((current) => event.target.checked ? [...current, lesson.id] : current.filter((item) => item !== lesson.id))} /></td><td className="cursor-pointer py-3 pr-3 font-medium text-[var(--q-text)]" onClick={() => { setSelectedLessonId(lesson.id); setSelectedStatus(String(statusToValue(lesson.status))); }}>{formatDateTime(lesson.startAtUtc)}</td><td className="py-3 pr-3">{lesson.studentName}</td><td className="py-3 pr-3">{lesson.instructorName}</td><td className="py-3 pr-3"><StatusBadge value={lesson.kind} /></td><td className="py-3 pr-3"><StatusBadge value={lesson.status} /></td><td className="py-3">{lesson.operationalConfirmedAtUtc ? <span className="text-xs uppercase tracking-[0.2em] text-[var(--q-success)]">Confirmada</span> : <span className="text-xs uppercase tracking-[0.2em] text-[var(--q-muted)]">Pendente</span>}</td></tr>)}</tbody>
            </table>
          </div>
        </GlassCard>
      </div>

      <div className="grid gap-4 xl:grid-cols-[0.84fr_1.16fr]">
        <GlassCard title="Controle operacional">
          {selectedLesson ? <div className="space-y-4"><div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4"><div className="text-lg font-semibold text-[var(--q-text)]">{selectedLesson.studentName}</div><div className="mt-1 text-sm text-[var(--q-text-2)]">{selectedLesson.instructorName}</div><div className="mt-3 flex flex-wrap gap-2"><StatusBadge value={selectedLesson.kind} /><StatusBadge value={selectedLesson.status} /></div><div className="mt-3 space-y-1 text-sm text-[var(--q-text-2)]"><div>Início: {formatDateTime(selectedLesson.startAtUtc)}</div><div>Duração: {selectedLesson.durationMinutes} min</div>{selectedLesson.singleLessonPrice ? <div>Receita avulsa: {formatCurrency(selectedLesson.singleLessonPrice)}</div> : null}<div>Confirmação operacional: {selectedLesson.operationalConfirmedAtUtc ? formatDateTime(selectedLesson.operationalConfirmedAtUtc) : "Pendente"}</div><div>No-show: {selectedLesson.noShowMarkedAtUtc ? formatDateTime(selectedLesson.noShowMarkedAtUtc) : "Não marcado"}</div></div></div><SelectField label="Status da aula" value={selectedStatus} onChange={setSelectedStatus} options={statusOptions} /><InputField label="Nota operacional" value={operationNote} onChange={setOperationNote} placeholder="Observações da equipe" /><button className="w-full rounded-full border border-[var(--q-warning)]/25 bg-[var(--q-warning-bg)] px-5 py-3 text-sm font-medium uppercase tracking-[0.22em] text-[#B58100]" type="button" onClick={handleUpdateStatus} disabled={isUpdatingStatus}>{isUpdatingStatus ? "Atualizando status" : "Atualizar status"}</button><div className="grid gap-3 md:grid-cols-2"><button className="rounded-full border border-[var(--q-info)]/25 bg-[var(--q-info-bg)] px-5 py-3 text-sm font-medium uppercase tracking-[0.2em] text-[var(--q-info)]" type="button" onClick={handleOperationalConfirm} disabled={isApplyingOperation}>Confirmar operação</button><button className="rounded-full border border-[var(--q-danger)]/25 bg-[var(--q-danger-bg)] px-5 py-3 text-sm font-medium uppercase tracking-[0.2em] text-[var(--q-danger)]" type="button" onClick={handleNoShow} disabled={isApplyingOperation}>Marcar no-show</button></div></div> : <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">Selecione uma aula para operar.</div>}
        </GlassCard>

        <GlassCard title="Reagendamento assistido e em lote">
          <div className="space-y-4">
            <div className="grid gap-4 md:grid-cols-[1fr_0.9fr_auto]"><InputField label="Novo início-base" value={batchStartAtUtc} onChange={setBatchStartAtUtc} type="datetime-local" /><SelectField label="Instrutor do lote" value={batchInstructorId} onChange={setBatchInstructorId} options={[{ value: "", label: "Manter instrutores atuais" }, ...instructors.map((item) => ({ value: item.id, label: item.fullName }))]} /><div className="grid content-end"><button className="rounded-full border border-transparent bg-[var(--q-grad-brand)] px-5 py-3 text-sm font-medium uppercase tracking-[0.2em] text-white" type="button" onClick={handleBatchReschedule} disabled={selectedLessonIds.length === 0 || !batchStartAtUtc || isBatchRescheduling}>{isBatchRescheduling ? "Aplicando" : "Remarcar lote"}</button></div></div>
            <div className="flex flex-wrap items-center gap-3"><button className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-2 text-xs uppercase tracking-[0.2em] text-[var(--q-text)]" type="button" onClick={handleLoadSuggestions} disabled={!selectedLesson || isLoadingSuggestions}>{isLoadingSuggestions ? "Buscando slots" : "Buscar sugestões"}</button><div className="text-xs uppercase tracking-[0.2em] text-[var(--q-muted)]">{selectedLessonIds.length} aula(s) no lote</div></div>
            {suggestions.length > 0 ? <div className="space-y-3">{suggestions.map((slot) => <button key={`${slot.startAtUtc}-${slot.instructorId}`} className="flex w-full items-center justify-between gap-3 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-left text-sm text-[var(--q-text)]" type="button" onClick={() => handleApplySuggestion(slot)}><span>{formatDateTime(slot.startAtUtc)} · {slot.instructorName}</span><span className="text-xs uppercase tracking-[0.2em] text-[var(--q-muted)]">{slot.availabilityLabel}</span></button>)}</div> : <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">As sugestões aparecem aqui com base na disponibilidade e nos bloqueios.</div>}
          </div>
        </GlassCard>
      </div>

      <div className="grid gap-4 xl:grid-cols-[0.82fr_1.18fr]">
        <GlassCard title="Bloqueios de agenda">
          <form className="grid gap-4" onSubmit={handleCreateBlock}>
            <div className="grid gap-4 md:grid-cols-2"><SelectField label="Escopo" value={blockForm.scope} onChange={(value) => setBlockForm((current) => ({ ...current, scope: value, instructorId: "" }))} options={scopeOptions} /><SelectField label="Instrutor" value={blockForm.instructorId} onChange={(value) => setBlockForm((current) => ({ ...current, instructorId: value }))} options={[{ value: "", label: "Selecione o instrutor" }, ...instructors.map((item) => ({ value: item.id, label: item.fullName }))]} disabled={blockForm.scope !== "2"} /></div>
            <InputField label="Título do bloqueio" value={blockForm.title} onChange={(value) => setBlockForm((current) => ({ ...current, title: value }))} required />
            <InputField label="Observações" value={blockForm.notes} onChange={(value) => setBlockForm((current) => ({ ...current, notes: value }))} />
            <div className="grid gap-4 md:grid-cols-2"><InputField label="Início" value={blockForm.startAtUtc} onChange={(value) => setBlockForm((current) => ({ ...current, startAtUtc: value }))} type="datetime-local" required /><InputField label="Fim" value={blockForm.endAtUtc} onChange={(value) => setBlockForm((current) => ({ ...current, endAtUtc: value }))} type="datetime-local" required /></div>
            <button className="rounded-full border border-transparent bg-[var(--q-grad-brand)] px-5 py-3 text-sm font-medium uppercase tracking-[0.2em] text-white" type="submit" disabled={isSavingBlock}>{isSavingBlock ? "Criando bloqueio" : "Criar bloqueio"}</button>
          </form>
        </GlassCard>

        <GlassCard title="Bloqueios ativos">
          <div className="space-y-3">{blocks.length === 0 ? <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">Nenhum bloqueio cadastrado.</div> : blocks.map((block) => <div key={block.id} className="flex items-center justify-between gap-4 rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface)] px-4 py-4"><div className="space-y-1"><div className="font-medium text-[var(--q-text)]">{block.title}</div><div className="text-sm text-[var(--q-text-2)]">{formatDateTime(block.startAtUtc)} até {formatDateTime(block.endAtUtc)}</div><div className="flex flex-wrap gap-2"><StatusBadge value={block.scope} />{block.instructorName ? <StatusBadge value={block.instructorName} /> : null}</div></div><button className="rounded-full border border-[var(--q-danger)]/25 bg-[var(--q-danger-bg)] px-4 py-2 text-xs uppercase tracking-[0.2em] text-[var(--q-danger)]" type="button" onClick={() => handleDeleteBlock(block.id)}>Remover</button></div>)}</div>
        </GlassCard>
      </div>
    </div>
  );
}

function InputField({ label, value, onChange, type = "text", placeholder, min, step, disabled = false, required = false }: { label: string; value: string; onChange: (value: string) => void; type?: string; placeholder?: string; min?: string; step?: string; disabled?: boolean; required?: boolean; }) {
  return <label className="grid gap-2 text-sm text-[var(--q-text)]"><span>{label}</span><input className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none disabled:opacity-70" value={value} onChange={(event) => onChange(event.target.value)} type={type} placeholder={placeholder} min={min} step={step} disabled={disabled} required={required} /></label>;
}

function SelectField({ label, value, onChange, options, disabled = false, required = false }: { label: string; value: string; onChange: (value: string) => void; options: Array<{ value: string; label: string }>; disabled?: boolean; required?: boolean; }) {
  return <label className="grid gap-2 text-sm text-[var(--q-text)]"><span>{label}</span><select className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none disabled:opacity-70" value={value} onChange={(event) => onChange(event.target.value)} disabled={disabled} required={required}>{options.map((option) => <option key={`${label}-${option.value}`} value={option.value}>{option.label}</option>)}</select></label>;
}

function statusToValue(status: string) { return statusOptions.find((item) => item.label === status)?.value ?? "1"; }
function isSameDay(value: string) { const date = new Date(value); const now = new Date(); return date.getDate() === now.getDate() && date.getMonth() === now.getMonth() && date.getFullYear() === now.getFullYear(); }
