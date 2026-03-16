import { LoaderCircle, Save, ShieldCheck, UserRound } from "lucide-react";
import { useEffect, useState } from "react";
import { useSession } from "../auth/SessionContext";
import {
  getStudentPortalProfile,
  updateStudentPortalProfile,
  type StudentPortalProfileResponse
} from "../lib/platform-api";
import { formatDateTime } from "../lib/formatters";
import { resolveStudentPortalTheme } from "../lib/student-portal-theme";

export function StudentPortalProfilePage() {
  const { token, school } = useSession();
  const theme = resolveStudentPortalTheme(school);
  const [payload, setPayload] = useState<StudentPortalProfileResponse | null>(null);
  const [form, setForm] = useState({
    fullName: "",
    phone: "",
    birthDate: "",
    medicalNotes: "",
    emergencyContactName: "",
    emergencyContactPhone: ""
  });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      return;
    }

    void loadProfile(token);
  }, [token]);

  async function loadProfile(sessionToken: string) {
    try {
      setLoading(true);
      setError(null);
      const data = await getStudentPortalProfile(sessionToken);
      setPayload(data);
      setForm({
        fullName: data.student.fullName ?? "",
        phone: data.student.phone ?? "",
        birthDate: data.student.birthDate ?? "",
        medicalNotes: data.student.medicalNotes ?? "",
        emergencyContactName: data.student.emergencyContactName ?? "",
        emergencyContactPhone: data.student.emergencyContactPhone ?? ""
      });
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Não foi possível carregar o perfil do aluno.");
    } finally {
      setLoading(false);
    }
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setSaving(true);
      setError(null);
      setFeedback(null);
      await updateStudentPortalProfile(token, {
        fullName: form.fullName,
        phone: form.phone || undefined,
        birthDate: form.birthDate || null,
        medicalNotes: form.medicalNotes || undefined,
        emergencyContactName: form.emergencyContactName || undefined,
        emergencyContactPhone: form.emergencyContactPhone || undefined
      });
      setFeedback("Perfil atualizado com sucesso.");
      await loadProfile(token);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : "Não foi possível atualizar o perfil.");
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <div className="flex min-h-[50vh] items-center justify-center rounded-[32px] border border-white/40 bg-white/55 text-sm uppercase tracking-[0.35em] text-slate-700 shadow-xl backdrop-blur-xl">
        <LoaderCircle size={18} className="mr-3 animate-spin" />
        Carregando perfil
      </div>
    );
  }

  if (!payload) {
    return (
      <div className="rounded-[32px] border border-rose-300/40 bg-white/70 p-8 text-sm text-rose-900 shadow-xl backdrop-blur-xl">
        {error ?? "Não foi possível carregar o perfil do aluno."}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <section className="rounded-[32px] p-6 shadow-xl backdrop-blur-2xl" style={{ border: `1px solid ${theme.frame}`, background: theme.heroBackground }}>
        <div className="grid gap-4 xl:grid-cols-[1fr_0.8fr]">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full px-3 py-1 text-xs uppercase tracking-[0.35em] text-slate-700" style={{ border: `1px solid ${theme.frame}`, backgroundColor: "rgba(255,255,255,0.72)" }}>
              <UserRound size={14} />
              Perfil do aluno
            </div>
            <h2 className="mt-4 text-3xl font-semibold tracking-tight text-slate-950">
              Dados que ajudam a equipe a operar seu treinamento com contexto
            </h2>
            <p className="mt-3 max-w-3xl text-sm leading-6 text-slate-600">
              Mantendo este perfil atualizado, a escola consegue atender melhor em agenda, segurança,
              comunicação e acompanhamento da sua evolução.
            </p>
          </div>

          <div className="grid gap-3 sm:grid-cols-3">
            <ProfileStat label="Perfil completo" value={`${payload.summary.profileCompleteness}%`} />
            <ProfileStat label="Aulas realizadas" value={String(payload.summary.realizedLessons)} />
            <ProfileStat label="Aulas futuras" value={String(payload.summary.upcomingLessons)} />
          </div>
        </div>
      </section>

      <div className="grid gap-6 xl:grid-cols-[1.05fr_0.95fr]">
        <form onSubmit={handleSubmit} className="rounded-[32px] p-6 shadow-xl backdrop-blur-2xl" style={{ border: `1px solid ${theme.frame}`, background: theme.cardBackground }}>
          <div className="grid gap-4 md:grid-cols-2">
            <label className="grid gap-2 text-sm text-slate-800 md:col-span-2">
              <span>Nome completo</span>
              <input
                value={form.fullName}
                onChange={(event) => setForm((current) => ({ ...current, fullName: event.target.value }))}
                className="rounded-2xl border border-slate-900/10 bg-white px-4 py-3 outline-none transition focus:border-cyan-400"
                placeholder="Nome exibido no portal"
                required
              />
            </label>

            <label className="grid gap-2 text-sm text-slate-800">
              <span>E-mail de acesso</span>
              <input
                value={payload.student.email ?? ""}
                className="rounded-2xl border border-slate-900/10 bg-slate-100 px-4 py-3 outline-none"
                disabled
              />
            </label>

            <label className="grid gap-2 text-sm text-slate-800">
              <span>Telefone principal</span>
              <input
                value={form.phone}
                onChange={(event) => setForm((current) => ({ ...current, phone: event.target.value }))}
                className="rounded-2xl border border-slate-900/10 bg-white px-4 py-3 outline-none transition focus:border-cyan-400"
                placeholder="Contato para agenda e avisos"
              />
            </label>

            <label className="grid gap-2 text-sm text-slate-800">
              <span>Data de nascimento</span>
              <input
                type="date"
                value={form.birthDate}
                onChange={(event) => setForm((current) => ({ ...current, birthDate: event.target.value }))}
                className="rounded-2xl border border-slate-900/10 bg-white px-4 py-3 outline-none transition focus:border-cyan-400"
              />
            </label>

            <label className="grid gap-2 text-sm text-slate-800">
              <span>Primeiro contato de emergência</span>
              <input
                value={form.emergencyContactName}
                onChange={(event) =>
                  setForm((current) => ({ ...current, emergencyContactName: event.target.value }))
                }
                className="rounded-2xl border border-slate-900/10 bg-white px-4 py-3 outline-none transition focus:border-cyan-400"
                placeholder="Nome de quem a escola pode acionar"
              />
            </label>

            <label className="grid gap-2 text-sm text-slate-800 md:col-span-2">
              <span>Telefone do contato de emergência</span>
              <input
                value={form.emergencyContactPhone}
                onChange={(event) =>
                  setForm((current) => ({ ...current, emergencyContactPhone: event.target.value }))
                }
                className="rounded-2xl border border-slate-900/10 bg-white px-4 py-3 outline-none transition focus:border-cyan-400"
                placeholder="Telefone usado apenas quando necessário"
              />
            </label>

            <label className="grid gap-2 text-sm text-slate-800 md:col-span-2">
              <span>Observações médicas ou cuidados importantes</span>
              <textarea
                value={form.medicalNotes}
                onChange={(event) => setForm((current) => ({ ...current, medicalNotes: event.target.value }))}
                className="min-h-32 rounded-2xl border border-slate-900/10 bg-white px-4 py-3 outline-none transition focus:border-cyan-400"
                placeholder="Ex.: alergias, restrições, cuidados ou informações relevantes para a operação."
              />
            </label>
          </div>

          <div className="mt-5 flex flex-wrap items-center gap-3">
            <button
              type="submit"
              disabled={saving}
              className="inline-flex items-center gap-2 rounded-2xl bg-slate-950 px-5 py-3 text-sm font-medium text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {saving ? <LoaderCircle size={16} className="animate-spin" /> : <Save size={16} />}
              Salvar perfil
            </button>
            {feedback ? <span className="text-sm text-emerald-700">{feedback}</span> : null}
            {error ? <span className="text-sm text-rose-700">{error}</span> : null}
          </div>
        </form>

        <aside className="space-y-4">
          <div className="rounded-[32px] p-6 shadow-xl backdrop-blur-2xl" style={{ border: `1px solid ${theme.frame}`, background: theme.cardBackground }}>
            <div className="inline-flex items-center gap-2 text-xs uppercase tracking-[0.35em] text-slate-500">
              <ShieldCheck size={14} />
              Resumo do perfil
            </div>
            <div className="mt-4 space-y-4">
              <ProfileInfo label="Aluno" value={payload.student.fullName} />
              <ProfileInfo label="E-mail" value={payload.student.email ?? "-"} />
              <ProfileInfo label="Telefone" value={payload.student.phone ?? "Ainda não informado"} />
              <ProfileInfo
                label="Primeiro stand-up"
                value={payload.student.firstStandUpAtUtc ? formatDateTime(payload.student.firstStandUpAtUtc) : "Ainda não registrado"}
              />
              <ProfileInfo label="Cadastro criado em" value={formatDateTime(payload.student.createdAtUtc)} />
            </div>
          </div>

          <div className="rounded-[32px] p-6 shadow-xl" style={{ border: `1px solid ${theme.frame}`, background: theme.mutedCardBackground }}>
            <div className="text-xs uppercase tracking-[0.35em] text-emerald-800">Por que manter isso em dia</div>
            <div className="mt-3 text-sm leading-6 text-slate-700">
              Informações corretas ajudam a escola a operar agenda, segurança, comunicação e acompanhamento do seu treinamento de forma mais responsiva.
            </div>
          </div>
        </aside>
      </div>
    </div>
  );
}

function ProfileStat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[22px] border border-slate-950/8 bg-white/75 px-4 py-4 text-center">
      <div className="text-xs uppercase tracking-[0.25em] text-slate-500">{label}</div>
      <div className="mt-2 text-2xl font-semibold text-slate-950">{value}</div>
    </div>
  );
}

function ProfileInfo({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[22px] border border-slate-950/8 bg-slate-50 px-4 py-4">
      <div className="text-xs uppercase tracking-[0.25em] text-slate-500">{label}</div>
      <div className="mt-2 text-sm font-medium text-slate-900">{value}</div>
    </div>
  );
}
