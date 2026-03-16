import { useEffect, useState } from "react";
import { useSession } from "../auth/SessionContext";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock, StatusBadge } from "../components/OperationsUi";
import {
  formatCurrency,
  formatDateTime,
  fromLocalDateTimeInput,
  toLocalDateTimeInput
} from "../lib/formatters";
import { translateLabel } from "../lib/localization";
import {
  createReceivableEntry,
  createExpenseEntry,
  createRevenueEntry,
  deleteReceivableEntry,
  deleteExpenseEntry,
  deleteRevenueEntry,
  getExpenseEntries,
  getFinanceOverview,
  getReceivableEntries,
  getRevenueEntries,
  getStudents,
  registerReceivablePayment,
  updateReceivableEntry,
  updateExpenseEntry,
  updateRevenueEntry,
  type ExpenseEntry,
  type FinanceOverview,
  type ReceivableEntry,
  type RevenueEntry
} from "../lib/platform-api";

const revenueSourceOptions = [
  { value: 1, label: "Venda de matrícula" },
  { value: 2, label: "Aula avulsa" },
  { value: 3, label: "Ajuste manual" }
];

const expenseCategoryOptions = [
  { value: 1, label: "Operação" },
  { value: 2, label: "Manutenção" },
  { value: 3, label: "Folha" },
  { value: 4, label: "Logistica" },
  { value: 5, label: "Marketing" },
  { value: 6, label: "Outros" }
];

const initialFilters = {
  fromUtc: "",
  toUtc: ""
};

function createInitialRevenueForm() {
  return {
    id: "",
    sourceType: "3",
    sourceId: "",
    category: "",
    amount: "",
    recognizedAtUtc: toLocalDateTimeInput(new Date().toISOString()),
    description: ""
  };
}

function createInitialExpenseForm() {
  return {
    id: "",
    category: "1",
    amount: "",
    occurredAtUtc: toLocalDateTimeInput(new Date().toISOString()),
    description: "",
    vendor: ""
  };
}

function createInitialReceivableForm() {
  return {
    id: "",
    studentId: "",
    studentNameSnapshot: "",
    enrollmentId: "",
    amount: "",
    dueAtUtc: toLocalDateTimeInput(new Date().toISOString()),
    description: "",
    notes: ""
  };
}

function createInitialPaymentForm() {
  return {
    receivableId: "",
    amount: "",
    paidAtUtc: toLocalDateTimeInput(new Date().toISOString()),
    note: ""
  };
}

export function FinancePage() {
  const { token } = useSession();
  const [overview, setOverview] = useState<FinanceOverview | null>(null);
  const [revenues, setRevenues] = useState<RevenueEntry[]>([]);
  const [expenses, setExpenses] = useState<ExpenseEntry[]>([]);
  const [students, setStudents] = useState<Array<{ id: string; fullName: string }>>([]);
  const [receivables, setReceivables] = useState<ReceivableEntry[]>([]);
  const [filters, setFilters] = useState(initialFilters);
  const [revenueForm, setRevenueForm] = useState(createInitialRevenueForm);
  const [expenseForm, setExpenseForm] = useState(createInitialExpenseForm);
  const [receivableForm, setReceivableForm] = useState(createInitialReceivableForm);
  const [paymentForm, setPaymentForm] = useState(createInitialPaymentForm);
  const [isLoading, setIsLoading] = useState(true);
  const [isSavingRevenue, setIsSavingRevenue] = useState(false);
  const [isSavingExpense, setIsSavingExpense] = useState(false);
  const [isSavingReceivable, setIsSavingReceivable] = useState(false);
  const [isSavingPayment, setIsSavingPayment] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      return;
    }

    void loadFinance(token, filters);
  }, [token]);

  async function loadFinance(
    currentToken: string,
    currentFilters: typeof filters
  ) {
    try {
      setIsLoading(true);
      setError(null);

      const query = {
        fromUtc: fromLocalDateTimeInput(currentFilters.fromUtc) ?? undefined,
        toUtc: fromLocalDateTimeInput(currentFilters.toUtc) ?? undefined
      };

      const [overviewData, revenuesData, expensesData, receivablesData, studentsData] = await Promise.all([
        getFinanceOverview(currentToken, query),
        getRevenueEntries(currentToken, query),
        getExpenseEntries(currentToken, query),
        getReceivableEntries(currentToken, {
          fromDueUtc: query.fromUtc,
          toDueUtc: query.toUtc,
          includeSettled: true
        }),
        getStudents(currentToken)
      ]);

      setOverview(overviewData);
      setRevenues(revenuesData);
      setExpenses(expensesData);
      setReceivables(receivablesData);
      setStudents(studentsData.map((item) => ({ id: item.id, fullName: item.fullName })));
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar o financeiro.");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleApplyFilters(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    await loadFinance(token, filters);
  }

  async function handleRevenueSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSavingRevenue(true);
      setError(null);

      const payload = {
        sourceType: Number(revenueForm.sourceType),
        sourceId: revenueForm.sourceId || null,
        category: revenueForm.category,
        amount: parseDecimalInput(revenueForm.amount),
        recognizedAtUtc: fromLocalDateTimeInput(revenueForm.recognizedAtUtc) ?? new Date().toISOString(),
        description: revenueForm.description
      };

      if (revenueForm.id) {
        await updateRevenueEntry(token, revenueForm.id, payload);
      } else {
        await createRevenueEntry(token, payload);
      }

      setRevenueForm(createInitialRevenueForm());
      await loadFinance(token, filters);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar a receita.");
    } finally {
      setIsSavingRevenue(false);
    }
  }

  async function handleExpenseSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSavingExpense(true);
      setError(null);

      const payload = {
        category: Number(expenseForm.category),
        amount: parseDecimalInput(expenseForm.amount),
        occurredAtUtc: fromLocalDateTimeInput(expenseForm.occurredAtUtc) ?? new Date().toISOString(),
        description: expenseForm.description,
        vendor: expenseForm.vendor || undefined
      };

      if (expenseForm.id) {
        await updateExpenseEntry(token, expenseForm.id, payload);
      } else {
        await createExpenseEntry(token, payload);
      }

      setExpenseForm(createInitialExpenseForm());
      await loadFinance(token, filters);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar a despesa.");
    } finally {
      setIsSavingExpense(false);
    }
  }

  async function handleDeleteRevenue(revenueId: string) {
    if (!token || !window.confirm("Deseja excluir esta receita?")) {
      return;
    }

    try {
      setError(null);
      await deleteRevenueEntry(token, revenueId);
      if (revenueForm.id === revenueId) {
        setRevenueForm(createInitialRevenueForm());
      }
      await loadFinance(token, filters);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível excluir a receita.");
    }
  }

  async function handleDeleteExpense(expenseId: string) {
    if (!token || !window.confirm("Deseja excluir esta despesa?")) {
      return;
    }

    try {
      setError(null);
      await deleteExpenseEntry(token, expenseId);
      if (expenseForm.id === expenseId) {
        setExpenseForm(createInitialExpenseForm());
      }
      await loadFinance(token, filters);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível excluir a despesa.");
    }
  }

  async function handleReceivableSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSavingReceivable(true);
      setError(null);

      const selectedStudent = students.find((item) => item.id === receivableForm.studentId);
      const payload = {
        studentId: receivableForm.studentId,
        studentNameSnapshot: selectedStudent?.fullName ?? receivableForm.studentNameSnapshot,
        enrollmentId: receivableForm.enrollmentId || null,
        amount: parseDecimalInput(receivableForm.amount),
        dueAtUtc: fromLocalDateTimeInput(receivableForm.dueAtUtc) ?? new Date().toISOString(),
        description: receivableForm.description,
        notes: receivableForm.notes || undefined
      };

      if (receivableForm.id) {
        await updateReceivableEntry(token, receivableForm.id, payload);
      } else {
        await createReceivableEntry(token, payload);
      }

      setReceivableForm(createInitialReceivableForm());
      await loadFinance(token, filters);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar a cobrança.");
    } finally {
      setIsSavingReceivable(false);
    }
  }

  async function handleReceivablePaymentSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token || !paymentForm.receivableId) {
      return;
    }

    try {
      setIsSavingPayment(true);
      setError(null);
      await registerReceivablePayment(token, paymentForm.receivableId, {
        amount: parseDecimalInput(paymentForm.amount),
        paidAtUtc: fromLocalDateTimeInput(paymentForm.paidAtUtc) ?? new Date().toISOString(),
        note: paymentForm.note || undefined
      });

      setPaymentForm(createInitialPaymentForm());
      await loadFinance(token, filters);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível registrar o pagamento.");
    } finally {
      setIsSavingPayment(false);
    }
  }

  async function handleDeleteReceivable(receivableId: string) {
    if (!token || !window.confirm("Deseja excluir esta cobrança?")) {
      return;
    }

    try {
      setError(null);
      await deleteReceivableEntry(token, receivableId);
      if (receivableForm.id === receivableId) {
        setReceivableForm(createInitialReceivableForm());
      }
      if (paymentForm.receivableId === receivableId) {
        setPaymentForm(createInitialPaymentForm());
      }
      await loadFinance(token, filters);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível excluir a cobrança.");
    }
  }

  const biggestRevenueSource = overview?.revenueBySource[0]?.sourceType;
  const biggestExpenseCategory = overview?.expenseByCategory[0]?.category;

  return (
    <div className="space-y-6">
      <PageHero
        eyebrow="Financeiro"
        title="Lance receitas e despesas reais com leitura clara de margem, origem e categoria."
        description="Este módulo financeiro combina lançamentos manuais com o custo calculado das aulas realizadas, usando a hora/aula cadastrada para cada instrutor."
        stats={[
          { label: "Receita", value: formatCurrency(overview?.totalRevenue) },
          { label: "Despesa", value: formatCurrency(overview?.totalExpense) },
          { label: "Em aberto", value: formatCurrency(overview?.receivablesOpenAmount) },
          { label: "Em atraso", value: formatCurrency(overview?.receivablesOverdueAmount) },
          { label: "Custo instrutores", value: formatCurrency(overview?.instructorPayrollExpense) },
          { label: "Margem", value: formatCurrency(overview?.grossMargin) }
        ]}
      />

      {isLoading ? <LoadingBlock label="Carregando financeiro operacional" /> : null}
      {error ? <ErrorBlock message={error} /> : null}

      <GlassCard
        title="Período analisado"
        description="Use este filtro para limitar os lançamentos e os indicadores a uma janela específica."
      >
        <form className="grid gap-3 md:grid-cols-[1fr_1fr_auto]" onSubmit={handleApplyFilters}>
          <label className="grid gap-2 text-sm text-[var(--q-text)]">
            <span>Data e hora inicial do período</span>
            <input
              className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
              type="datetime-local"
              value={filters.fromUtc}
              onChange={(event) => setFilters((current) => ({ ...current, fromUtc: event.target.value }))}
            />
          </label>
          <label className="grid gap-2 text-sm text-[var(--q-text)]">
            <span>Data e hora final do período</span>
            <input
              className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
              type="datetime-local"
              value={filters.toUtc}
              onChange={(event) => setFilters((current) => ({ ...current, toUtc: event.target.value }))}
            />
          </label>
          <div className="flex items-end gap-3">
            <button
              className="rounded-full border border-transparent bg-[var(--q-grad-brand)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95"
              type="submit"
            >
              Aplicar filtro
            </button>
            <button
              className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface-2)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-text)] transition hover:bg-[var(--q-info-bg)]"
              type="button"
              onClick={() => {
                setFilters(initialFilters);
                if (token) {
                  void loadFinance(token, initialFilters);
                }
              }}
            >
              Limpar
            </button>
          </div>
        </form>
      </GlassCard>

      {!isLoading ? (
        <div className="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
          <GlassCard
            title="Resumo do período"
            description="Leitura executiva para identificar rapidamente a composição da margem e o principal peso financeiro."
          >
            <div className="grid gap-3 md:grid-cols-2">
              <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">Receitas no período</div>
                <div className="mt-3 text-2xl font-semibold text-[var(--q-text)]">{overview?.revenueEntries ?? 0}</div>
                <div className="mt-2 text-sm text-[var(--q-text-2)]">
                  Maior origem: {biggestRevenueSource ? translateLabel(biggestRevenueSource) : "-"}
                </div>
              </div>
              <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">Despesas no período</div>
                <div className="mt-3 text-2xl font-semibold text-[var(--q-text)]">{overview?.expenseEntries ?? 0}</div>
                <div className="mt-2 text-sm text-[var(--q-text-2)]">
                  Maior categoria: {biggestExpenseCategory ? translateLabel(biggestExpenseCategory) : "-"}
                </div>
                <div className="mt-2 text-sm text-[var(--q-text-2)]">
                  Despesa manual: {formatCurrency(overview?.manualExpenseTotal)}
                </div>
              </div>
              <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">Maior origem de receita</div>
                <div className="mt-3 text-base font-semibold text-[var(--q-text)]">
                  {overview?.revenueBySource[0] ? translateLabel(overview.revenueBySource[0].sourceType) : "-"}
                </div>
                <div className="mt-2 text-sm text-[var(--q-text-2)]">
                  {overview?.revenueBySource[0]
                    ? `${formatCurrency(overview.revenueBySource[0].totalAmount)} em ${overview.revenueBySource[0].entries} lançamentos`
                    : "Nenhuma receita no período"}
                </div>
              </div>
              <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">Maior categoria de despesa</div>
                <div className="mt-3 text-base font-semibold text-[var(--q-text)]">
                  {overview?.expenseByCategory[0] ? translateLabel(overview.expenseByCategory[0].category) : "-"}
                </div>
                <div className="mt-2 text-sm text-[var(--q-text-2)]">
                  {overview?.expenseByCategory[0]
                    ? `${formatCurrency(overview.expenseByCategory[0].totalAmount)} em ${overview.expenseByCategory[0].entries} lançamentos`
                    : "Nenhuma despesa no período"}
                </div>
              </div>
              <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4 md:col-span-2">
                <div className="grid gap-3 md:grid-cols-2">
                  <div>
                    <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">Custo calculado de instrutores</div>
                    <div className="mt-3 text-2xl font-semibold text-[var(--q-text)]">
                      {formatCurrency(overview?.instructorPayrollExpense)}
                    </div>
                    <div className="mt-2 text-sm text-[var(--q-text-2)]">
                      Baseado nas aulas realizadas e no valor da hora/aula cadastrado para cada instrutor.
                    </div>
                  </div>
                  <div>
                    <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">Carga horária realizada</div>
                    <div className="mt-3 text-2xl font-semibold text-[var(--q-text)]">
                      {formatInstructionHours(overview?.realizedInstructionMinutes)}
                    </div>
                    <div className="mt-2 text-sm text-[var(--q-text-2)]">
                      Esta leitura entra automaticamente na despesa total do período e afeta a margem da escola.
                    </div>
                  </div>
                </div>
              </div>
              <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4 md:col-span-2">
                <div className="grid gap-3 md:grid-cols-3">
                  <div>
                    <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">Carteira em aberto</div>
                    <div className="mt-3 text-2xl font-semibold text-[var(--q-text)]">
                      {formatCurrency(overview?.receivablesOpenAmount)}
                    </div>
                    <div className="mt-2 text-sm text-[var(--q-text-2)]">
                      {overview?.receivablesOpenEntries ?? 0} cobranças aguardando pagamento.
                    </div>
                  </div>
                  <div>
                    <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">Em atraso</div>
                    <div className="mt-3 text-2xl font-semibold text-[var(--q-danger)]">
                      {formatCurrency(overview?.receivablesOverdueAmount)}
                    </div>
                    <div className="mt-2 text-sm text-[var(--q-text-2)]">
                      {overview?.delinquentStudents ?? 0} alunos com atraso financeiro.
                    </div>
                  </div>
                  <div>
                    <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">A vencer</div>
                    <div className="mt-3 text-2xl font-semibold text-[#B58100]">
                      {overview?.dueSoonStudents ?? 0}
                    </div>
                    <div className="mt-2 text-sm text-[var(--q-text-2)]">
                      Alunos com cobranças abertas, ainda dentro do prazo.
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </GlassCard>

          <GlassCard
            title="Leitura de margem"
            description="A margem bruta compara tudo o que entrou com receitas manuais e tudo o que saiu em despesas manuais mais custo de instrutores."
          >
            <div className="space-y-4">
              <div
                className="rounded-[22px] border p-4"
                style={{
                  borderColor: overview && overview.grossMargin >= 0 ? "rgba(46, 212, 167, 0.28)" : "rgba(235, 87, 87, 0.28)",
                  background:
                    overview && overview.grossMargin >= 0
                      ? "linear-gradient(180deg, rgba(230,251,245,0.96), rgba(214,244,239,0.92))"
                      : "linear-gradient(180deg, rgba(255,227,227,0.96), rgba(255,240,240,0.92))"
                }}
              >
                <div
                  className="text-xs uppercase tracking-[0.24em]"
                  style={{ color: overview && overview.grossMargin >= 0 ? "#167E63" : "#B94141" }}
                >
                  Margem bruta
                </div>
                <div
                  className="mt-3 text-3xl font-semibold"
                  style={{ color: overview && overview.grossMargin >= 0 ? "#0B3C5D" : "#8F2D2D" }}
                >
                  {formatCurrency(overview?.grossMargin)}
                </div>
                <div className="mt-2 text-sm text-[var(--q-text-2)]">
                  {overview
                    ? `Receita de ${formatCurrency(overview.totalRevenue)} menos despesas totais de ${formatCurrency(overview.totalExpense)}, incluindo ${formatCurrency(overview.instructorPayrollExpense)} de instrutores.`
                    : "-"}
                </div>
              </div>
              <div className="space-y-3">
                {overview?.revenueBySource.slice(0, 3).map((item) => (
                  <div key={item.sourceType} className="rounded-[18px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-3">
                    <div className="flex items-center justify-between gap-3">
                      <span className="text-sm text-[var(--q-text)]">{translateLabel(item.sourceType)}</span>
                      <StatusBadge value={item.sourceType} />
                    </div>
                    <div className="mt-2 text-sm text-[var(--q-text-2)]">
                      {formatCurrency(item.totalAmount)} em {item.entries} lancamentos
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </GlassCard>
        </div>
      ) : null}

      <div className="grid gap-4 xl:grid-cols-[1.08fr_0.92fr]">
        <GlassCard
          title={receivableForm.id ? "Editar cobrança" : "Nova cobrança"}
          description="Use este bloco para lançar parcelas, mensalidades ou cobranças avulsas vinculadas ao aluno."
        >
          <form className="grid gap-3" onSubmit={handleReceivableSubmit}>
            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Aluno responsável pela cobrança</span>
              <select
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                value={receivableForm.studentId}
                onChange={(event) =>
                  setReceivableForm((current) => ({
                    ...current,
                    studentId: event.target.value,
                    studentNameSnapshot: students.find((item) => item.id === event.target.value)?.fullName ?? ""
                  }))
                }
                required
              >
                <option value="">Selecione um aluno</option>
                {students.map((student) => (
                  <option key={student.id} value={student.id}>
                    {student.fullName}
                  </option>
                ))}
              </select>
            </label>

            <div className="grid gap-3 md:grid-cols-2">
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Valor da cobrança</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  placeholder="0,00"
                  inputMode="decimal"
                  value={receivableForm.amount}
                  onChange={(event) => setReceivableForm((current) => ({ ...current, amount: event.target.value }))}
                  required
                />
              </label>
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Vencimento</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  type="datetime-local"
                  value={receivableForm.dueAtUtc}
                  onChange={(event) => setReceivableForm((current) => ({ ...current, dueAtUtc: event.target.value }))}
                  required
                />
              </label>
            </div>

            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Descrição da cobrança</span>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Ex.: Parcela 1 do curso, saldo remanescente, cobrança extra"
                value={receivableForm.description}
                onChange={(event) => setReceivableForm((current) => ({ ...current, description: event.target.value }))}
                required
              />
            </label>

            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Notas internas</span>
              <textarea
                className="min-h-24 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Informações complementares sobre a cobrança."
                value={receivableForm.notes}
                onChange={(event) => setReceivableForm((current) => ({ ...current, notes: event.target.value }))}
              />
            </label>

            <div className="flex flex-wrap gap-3">
              <button
                className="rounded-full border border-transparent bg-[var(--q-grad-brand)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95"
                type="submit"
                disabled={isSavingReceivable}
              >
                {isSavingReceivable ? "Salvando" : receivableForm.id ? "Atualizar cobrança" : "Criar cobrança"}
              </button>
              {receivableForm.id ? (
                <button
                  className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface-2)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-text)] transition hover:bg-[var(--q-info-bg)]"
                  type="button"
                  onClick={() => setReceivableForm(createInitialReceivableForm())}
                >
                  Cancelar edição
                </button>
              ) : null}
            </div>
          </form>
        </GlassCard>

        <GlassCard
          title={paymentForm.receivableId ? "Registrar pagamento" : "Baixa de pagamento"}
          description="Selecione uma cobrança na lista para registrar baixa parcial ou total e atualizar a situação do aluno."
        >
          <form className="grid gap-3" onSubmit={handleReceivablePaymentSubmit}>
            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Cobrança selecionada</span>
              <select
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                value={paymentForm.receivableId}
                onChange={(event) => {
                  const nextReceivable = receivables.find((item) => item.id === event.target.value);
                  setPaymentForm((current) => ({
                    ...current,
                    receivableId: event.target.value,
                    amount: nextReceivable ? String(nextReceivable.remainingAmount) : ""
                  }));
                }}
                required
              >
                <option value="">Selecione uma cobrança em aberto</option>
                {receivables
                  .filter((item) => item.remainingAmount > 0 && item.status !== "Cancelled")
                  .map((receivable) => (
                    <option key={receivable.id} value={receivable.id}>
                      {receivable.studentNameSnapshot} - {receivable.description}
                    </option>
                  ))}
              </select>
            </label>

            <div className="grid gap-3 md:grid-cols-2">
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Valor recebido</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  placeholder="0,00"
                  inputMode="decimal"
                  value={paymentForm.amount}
                  onChange={(event) => setPaymentForm((current) => ({ ...current, amount: event.target.value }))}
                  required
                />
              </label>
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Data e hora do pagamento</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  type="datetime-local"
                  value={paymentForm.paidAtUtc}
                  onChange={(event) => setPaymentForm((current) => ({ ...current, paidAtUtc: event.target.value }))}
                  required
                />
              </label>
            </div>

            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Observação da baixa</span>
              <textarea
                className="min-h-24 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Opcional: meio de pagamento, observação interna ou referência de comprovante."
                value={paymentForm.note}
                onChange={(event) => setPaymentForm((current) => ({ ...current, note: event.target.value }))}
              />
            </label>

            <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
              <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">Carteira financeira</div>
              <div className="mt-3 grid gap-3 md:grid-cols-2">
                <div>
                  <div className="text-sm text-[var(--q-text-2)]">Em aberto</div>
                  <div className="mt-1 text-xl font-semibold text-[var(--q-text)]">
                    {formatCurrency(overview?.receivablesOpenAmount)}
                  </div>
                </div>
                <div>
                  <div className="text-sm text-[var(--q-text-2)]">Em atraso</div>
                  <div className="mt-1 text-xl font-semibold text-[var(--q-danger)]">
                    {formatCurrency(overview?.receivablesOverdueAmount)}
                  </div>
                </div>
              </div>
            </div>

            <div className="flex flex-wrap gap-3">
              <button
                className="rounded-full border border-[var(--q-success)]/25 bg-[var(--q-success-bg)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-success)] transition hover:opacity-95"
                type="submit"
                disabled={isSavingPayment}
              >
                {isSavingPayment ? "Registrando" : "Registrar pagamento"}
              </button>
              {paymentForm.receivableId ? (
                <button
                  className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface-2)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-text)] transition hover:bg-[var(--q-info-bg)]"
                  type="button"
                  onClick={() => setPaymentForm(createInitialPaymentForm())}
                >
                  Limpar
                </button>
              ) : null}
            </div>
          </form>
        </GlassCard>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <GlassCard
          title={revenueForm.id ? "Editar receita" : "Nova receita"}
          description="Registre entradas como venda de matrícula, aula avulsa ou ajuste manual."
        >
          <form className="grid gap-3" onSubmit={handleRevenueSubmit}>
            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Origem da receita</span>
              <select
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                value={revenueForm.sourceType}
                onChange={(event) => setRevenueForm((current) => ({ ...current, sourceType: event.target.value }))}
              >
                {revenueSourceOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>

            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Categoria comercial da receita</span>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Exemplo: Curso Discovery, Pacote particular, Ajuste de caixa"
                value={revenueForm.category}
                onChange={(event) => setRevenueForm((current) => ({ ...current, category: event.target.value }))}
                required
              />
            </label>

            <div className="grid gap-3 md:grid-cols-2">
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Valor da receita</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  placeholder="0,00"
                  inputMode="decimal"
                  value={revenueForm.amount}
                  onChange={(event) => setRevenueForm((current) => ({ ...current, amount: event.target.value }))}
                  required
                />
              </label>
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Data e hora do reconhecimento</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  type="datetime-local"
                  value={revenueForm.recognizedAtUtc}
                  onChange={(event) =>
                    setRevenueForm((current) => ({ ...current, recognizedAtUtc: event.target.value }))
                  }
                  required
                />
              </label>
            </div>

            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Identificador de origem, se existir</span>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Opcional: id da matrícula, aula ou ajuste relacionado"
                value={revenueForm.sourceId}
                onChange={(event) => setRevenueForm((current) => ({ ...current, sourceId: event.target.value }))}
              />
            </label>

            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Descrição operacional</span>
              <textarea
                className="min-h-28 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Explique rapidamente o que entrou, para quem e em qual contexto."
                value={revenueForm.description}
                onChange={(event) => setRevenueForm((current) => ({ ...current, description: event.target.value }))}
                required
              />
            </label>

            <div className="flex flex-wrap gap-3">
              <button
                className="rounded-full border border-transparent bg-[var(--q-grad-brand)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95"
                type="submit"
                disabled={isSavingRevenue}
              >
                {isSavingRevenue ? "Salvando" : revenueForm.id ? "Atualizar receita" : "Criar receita"}
              </button>
              {revenueForm.id ? (
                <button
                  className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface-2)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-text)] transition hover:bg-[var(--q-info-bg)]"
                  type="button"
                  onClick={() => setRevenueForm(createInitialRevenueForm())}
                >
                  Cancelar edicao
                </button>
              ) : null}
            </div>
          </form>
        </GlassCard>

        <GlassCard
          title={expenseForm.id ? "Editar despesa" : "Nova despesa"}
          description="Registre saídas com categoria operacional, fornecedor e momento em que o gasto ocorreu."
        >
          <form className="grid gap-3" onSubmit={handleExpenseSubmit}>
            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Categoria da despesa</span>
              <select
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                value={expenseForm.category}
                onChange={(event) => setExpenseForm((current) => ({ ...current, category: event.target.value }))}
              >
                {expenseCategoryOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>

            <div className="grid gap-3 md:grid-cols-2">
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Valor da despesa</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  placeholder="0,00"
                  inputMode="decimal"
                  value={expenseForm.amount}
                  onChange={(event) => setExpenseForm((current) => ({ ...current, amount: event.target.value }))}
                  required
                />
              </label>
              <label className="grid gap-2 text-sm text-[var(--q-text)]">
                <span>Data e hora em que a despesa ocorreu</span>
                <input
                  className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                  type="datetime-local"
                  value={expenseForm.occurredAtUtc}
                  onChange={(event) =>
                    setExpenseForm((current) => ({ ...current, occurredAtUtc: event.target.value }))
                  }
                  required
                />
              </label>
            </div>

            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Fornecedor ou responsável pelo gasto</span>
              <input
                className="rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Opcional: oficina, colaborador, parceiro logistico"
                value={expenseForm.vendor}
                onChange={(event) => setExpenseForm((current) => ({ ...current, vendor: event.target.value }))}
              />
            </label>

            <label className="grid gap-2 text-sm text-[var(--q-text)]">
              <span>Descrição operacional</span>
              <textarea
                className="min-h-28 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
                placeholder="Explique rapidamente o motivo do gasto e o que foi pago."
                value={expenseForm.description}
                onChange={(event) => setExpenseForm((current) => ({ ...current, description: event.target.value }))}
                required
              />
            </label>

            <div className="flex flex-wrap gap-3">
              <button
                className="rounded-full border border-[var(--q-warning)]/25 bg-[var(--q-warning-bg)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-[#B58100] transition hover:opacity-95"
                type="submit"
                disabled={isSavingExpense}
              >
                {isSavingExpense ? "Salvando" : expenseForm.id ? "Atualizar despesa" : "Criar despesa"}
              </button>
              {expenseForm.id ? (
                <button
                  className="rounded-full border border-[var(--q-border)] bg-[var(--q-surface-2)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-text)] transition hover:bg-[var(--q-info-bg)]"
                  type="button"
                  onClick={() => setExpenseForm(createInitialExpenseForm())}
                >
                  Cancelar edicao
                </button>
              ) : null}
            </div>
          </form>
        </GlassCard>
      </div>

      <GlassCard
        title="Contas a receber"
        description="Acompanhe cobranças abertas, vencimentos e baixas sem perder o contexto financeiro da escola."
      >
        <div className="overflow-x-auto">
          <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
            <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
              <tr>
                <th className="pb-3">Aluno</th>
                <th className="pb-3">Descrição</th>
                <th className="pb-3">Vencimento</th>
                <th className="pb-3">Saldo</th>
                <th className="pb-3">Situação</th>
                <th className="pb-3">Ações</th>
              </tr>
            </thead>
            <tbody>
              {receivables.map((receivable) => (
                <tr key={receivable.id} className="border-t border-[var(--q-border)]">
                  <td className="py-3">
                    <div className="font-medium text-[var(--q-text)]">{receivable.studentNameSnapshot}</div>
                    <div className="mt-1 text-xs text-[var(--q-muted)]">
                      {receivable.paymentsCount} pagamento(s) registrado(s)
                    </div>
                  </td>
                  <td className="py-3">
                    <div className="font-medium text-[var(--q-text)]">{receivable.description}</div>
                    <div className="mt-1 text-xs text-[var(--q-muted)]">
                      Total {formatCurrency(receivable.amount)} · Pago {formatCurrency(receivable.paidAmount)}
                    </div>
                  </td>
                  <td className="py-3">
                    <div>{formatDateTime(receivable.dueAtUtc)}</div>
                    {receivable.lastPaymentAtUtc ? (
                      <div className="mt-1 text-xs text-[var(--q-muted)]">
                        Último pagamento: {formatDateTime(receivable.lastPaymentAtUtc)}
                      </div>
                    ) : null}
                  </td>
                  <td className="py-3 font-medium text-[var(--q-text)]">
                    {formatCurrency(receivable.remainingAmount)}
                  </td>
                  <td className="py-3">
                    <div className={resolveReceivableTone(receivable)}>
                      {receivable.isOverdue ? "Inadimplente" : translateLabel(receivable.status)}
                    </div>
                  </td>
                  <td className="py-3">
                    <div className="flex flex-wrap gap-2">
                      <button
                        className="rounded-full border border-[var(--q-info)]/25 bg-[var(--q-info-bg)] px-3 py-1 text-xs uppercase tracking-[0.2em] text-[var(--q-info)]"
                        type="button"
                        onClick={() =>
                          setReceivableForm({
                            id: receivable.id,
                            studentId: receivable.studentId,
                            studentNameSnapshot: receivable.studentNameSnapshot,
                            enrollmentId: receivable.enrollmentId ?? "",
                            amount: String(receivable.amount),
                            dueAtUtc: toLocalDateTimeInput(receivable.dueAtUtc),
                            description: receivable.description,
                            notes: receivable.notes ?? ""
                          })
                        }
                      >
                        Editar
                      </button>
                      {receivable.remainingAmount > 0 ? (
                        <button
                          className="rounded-full border border-[var(--q-success)]/25 bg-[var(--q-success-bg)] px-3 py-1 text-xs uppercase tracking-[0.2em] text-[var(--q-success)]"
                          type="button"
                          onClick={() =>
                            setPaymentForm({
                              receivableId: receivable.id,
                              amount: String(receivable.remainingAmount),
                              paidAtUtc: toLocalDateTimeInput(new Date().toISOString()),
                              note: ""
                            })
                          }
                        >
                          Receber
                        </button>
                      ) : null}
                      <button
                        className="rounded-full border border-rose-400/20 bg-rose-400/10 px-3 py-1 text-xs uppercase tracking-[0.2em] text-rose-600"
                        type="button"
                        onClick={() => void handleDeleteReceivable(receivable.id)}
                      >
                        Excluir
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {receivables.length === 0 ? (
          <div className="mt-4 rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
            Nenhuma cobrança encontrada para o período selecionado.
          </div>
        ) : null}
      </GlassCard>

      <div className="grid gap-4 xl:grid-cols-2">
        <GlassCard
          title="Receitas lancadas"
          description="Clique em uma receita para editar o lançamento sem perder o contexto do período filtrado."
        >
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
              <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                <tr>
                  <th className="pb-3">Origem</th>
                  <th className="pb-3">Categoria</th>
                  <th className="pb-3">Valor</th>
                  <th className="pb-3">Quando</th>
                  <th className="pb-3">Ações</th>
                </tr>
              </thead>
              <tbody>
                {revenues.map((revenue) => (
                  <tr key={revenue.id} className="border-t border-[var(--q-border)]">
                    <td className="py-3">
                      <div className="font-medium text-[var(--q-text)]">{translateLabel(revenue.sourceType)}</div>
                      <div className="mt-1 text-xs text-[var(--q-muted)]">{revenue.sourceId ?? "Sem origem vinculada"}</div>
                    </td>
                    <td className="py-3">
                      <div>{revenue.category}</div>
                      <div className="mt-1 text-xs text-[var(--q-muted)]">{revenue.description}</div>
                    </td>
                    <td className="py-3 font-medium text-[var(--q-text)]">{formatCurrency(revenue.amount)}</td>
                    <td className="py-3">{formatDateTime(revenue.recognizedAtUtc)}</td>
                    <td className="py-3">
                      <div className="flex flex-wrap gap-2">
                        <button
                          className="rounded-full border border-[var(--q-info)]/25 bg-[var(--q-info-bg)] px-3 py-1 text-xs uppercase tracking-[0.2em] text-[var(--q-info)]"
                          type="button"
                          onClick={() =>
                            setRevenueForm({
                              id: revenue.id,
                              sourceType: String(revenue.sourceTypeCode),
                              sourceId: revenue.sourceId ?? "",
                              category: revenue.category,
                              amount: String(revenue.amount),
                              recognizedAtUtc: toLocalDateTimeInput(revenue.recognizedAtUtc),
                              description: revenue.description
                            })
                          }
                        >
                          Editar
                        </button>
                        <button
                          className="rounded-full border border-rose-400/20 bg-rose-400/10 px-3 py-1 text-xs uppercase tracking-[0.2em] text-rose-100"
                          type="button"
                          onClick={() => void handleDeleteRevenue(revenue.id)}
                        >
                          Excluir
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {revenues.length === 0 ? (
            <div className="mt-4 rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
              Nenhuma receita encontrada para o período selecionado.
            </div>
          ) : null}
        </GlassCard>

        <GlassCard
          title="Despesas lancadas"
          description="A lista ajuda a revisar onde o caixa está sendo consumido e a corrigir classificações rapidamente."
        >
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
              <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
                <tr>
                  <th className="pb-3">Categoria</th>
                  <th className="pb-3">Descrição</th>
                  <th className="pb-3">Valor</th>
                  <th className="pb-3">Quando</th>
                  <th className="pb-3">Ações</th>
                </tr>
              </thead>
              <tbody>
                {expenses.map((expense) => (
                  <tr key={expense.id} className="border-t border-[var(--q-border)]">
                    <td className="py-3">
                      <div className="font-medium text-[var(--q-text)]">{translateLabel(expense.category)}</div>
                      <div className="mt-1 text-xs text-[var(--q-muted)]">{expense.vendor || "Sem fornecedor informado"}</div>
                    </td>
                    <td className="py-3">{expense.description}</td>
                    <td className="py-3 font-medium text-[var(--q-text)]">{formatCurrency(expense.amount)}</td>
                    <td className="py-3">{formatDateTime(expense.occurredAtUtc)}</td>
                    <td className="py-3">
                      <div className="flex flex-wrap gap-2">
                        <button
                          className="rounded-full border border-[var(--q-warning)]/25 bg-[var(--q-warning-bg)] px-3 py-1 text-xs uppercase tracking-[0.2em] text-[#B58100]"
                          type="button"
                          onClick={() =>
                            setExpenseForm({
                              id: expense.id,
                              category: String(expense.categoryCode),
                              amount: String(expense.amount),
                              occurredAtUtc: toLocalDateTimeInput(expense.occurredAtUtc),
                              description: expense.description,
                              vendor: expense.vendor ?? ""
                            })
                          }
                        >
                          Editar
                        </button>
                        <button
                          className="rounded-full border border-rose-400/20 bg-rose-400/10 px-3 py-1 text-xs uppercase tracking-[0.2em] text-rose-100"
                          type="button"
                          onClick={() => void handleDeleteExpense(expense.id)}
                        >
                          Excluir
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {expenses.length === 0 ? (
            <div className="mt-4 rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
              Nenhuma despesa encontrada para o período selecionado.
            </div>
          ) : null}
        </GlassCard>
      </div>
    </div>
  );
}

function formatInstructionHours(minutes?: number) {
  if (!minutes || minutes <= 0) {
    return "0h";
  }

  const hours = Math.round((minutes / 60) * 10) / 10;
  return `${hours}h`;
}

function resolveReceivableTone(receivable: ReceivableEntry) {
  if (receivable.isOverdue) {
    return "font-medium text-[var(--q-danger)]";
  }

  if (receivable.status === "Paid") {
    return "font-medium text-[var(--q-success)]";
  }

  if (receivable.status === "PartiallyPaid") {
    return "font-medium text-[#B58100]";
  }

  return "font-medium text-[var(--q-info)]";
}

function parseDecimalInput(value: string) {
  return Number(value.replace(",", "."));
}
