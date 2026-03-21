import { useEffect, useMemo, useState, type ReactNode } from "react";
import { useSession } from "../auth/SessionContext";
import { PageHero } from "../components/PageHero";
import { ErrorBlock, GlassCard, LoadingBlock, StatusBadge, StatGrid } from "../components/OperationsUi";
import {
  formatCurrency,
  formatDateTime,
  fromLocalDateTimeInput,
  toLocalDateTimeInput
} from "../lib/formatters";
import { translateLabel } from "../lib/localization";
import {
  createExpenseEntry,
  createPayableEntry,
  createReceivableEntry,
  createRevenueEntry,
  deleteExpenseEntry,
  deletePayableEntry,
  deleteReceivableEntry,
  deleteRevenueEntry,
  downloadFinanceExport,
  getCostCenters,
  getExpenseEntries,
  getFinanceOverview,
  getFinancialCategories,
  getPayableEntries,
  getReceivableEntries,
  getRevenueEntries,
  getStudents,
  reconcileFinancialEntry,
  registerPayablePayment,
  registerReceivablePayment,
  unreconcileFinancialEntry,
  updateExpenseEntry,
  updatePayableEntry,
  updateReceivableEntry,
  updateRevenueEntry,
  upsertCostCenter,
  upsertFinancialCategory,
  type CostCenter,
  type ExpenseEntry,
  type FinanceOverview,
  type FinancialCategory,
  type PayableEntry,
  type ReceivableEntry,
  type RevenueEntry
} from "../lib/platform-api";

const inputClassName =
  "rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none";
const actionButtonClassName =
  "rounded-full border border-transparent bg-[var(--q-grad-brand)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-white transition hover:opacity-95";
const secondaryButtonClassName =
  "rounded-full border border-[var(--q-border)] bg-[var(--q-surface-2)] px-5 py-3 text-sm font-medium uppercase tracking-[0.24em] text-[var(--q-text)] transition hover:bg-[var(--q-info-bg)]";

const revenueSourceOptions = [
  { value: 1, label: "Venda de matrícula" },
  { value: 2, label: "Aula avulsa" },
  { value: 3, label: "Ajuste manual" },
  { value: 4, label: "Manutenção de terceiro" }
];

const expenseCategoryOptions = [
  { value: 1, label: "Operação" },
  { value: 2, label: "Manutenção" },
  { value: 3, label: "Folha" },
  { value: 4, label: "Logística" },
  { value: 5, label: "Marketing" },
  { value: 6, label: "Outros" }
];

const initialFilters = {
  fromUtc: "",
  toUtc: "",
  categoryId: "",
  costCenterId: "",
  reconciled: ""
};

function createInitialCategoryForm() {
  return { id: "", name: "", direction: "3", sortOrder: "0", isActive: true };
}

function createInitialCostCenterForm() {
  return { id: "", name: "", description: "", isActive: true };
}

function createInitialRevenueForm() {
  return {
    id: "",
    sourceType: "3",
    sourceId: "",
    categoryId: "",
    category: "",
    costCenterId: "",
    amount: "",
    recognizedAtUtc: toLocalDateTimeInput(new Date().toISOString()),
    description: ""
  };
}

function createInitialExpenseForm() {
  return {
    id: "",
    category: "1",
    categoryId: "",
    categoryName: "",
    costCenterId: "",
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
    enrollmentId: "",
    categoryId: "",
    categoryName: "",
    costCenterId: "",
    amount: "",
    dueAtUtc: toLocalDateTimeInput(new Date().toISOString()),
    description: "",
    notes: ""
  };
}

function createInitialPayableForm() {
  return {
    id: "",
    categoryId: "",
    categoryName: "",
    costCenterId: "",
    amount: "",
    dueAtUtc: toLocalDateTimeInput(new Date().toISOString()),
    description: "",
    notes: "",
    vendor: ""
  };
}

function createInitialPaymentForm() {
  return {
    kind: "receivable" as "receivable" | "payable",
    entryId: "",
    amount: "",
    paidAtUtc: toLocalDateTimeInput(new Date().toISOString()),
    note: ""
  };
}

export function FinancePage() {
  const { token } = useSession();
  const [overview, setOverview] = useState<FinanceOverview | null>(null);
  const [categories, setCategories] = useState<FinancialCategory[]>([]);
  const [costCenters, setCostCenters] = useState<CostCenter[]>([]);
  const [students, setStudents] = useState<Array<{ id: string; fullName: string }>>([]);
  const [revenues, setRevenues] = useState<RevenueEntry[]>([]);
  const [expenses, setExpenses] = useState<ExpenseEntry[]>([]);
  const [receivables, setReceivables] = useState<ReceivableEntry[]>([]);
  const [payables, setPayables] = useState<PayableEntry[]>([]);
  const [filters, setFilters] = useState(initialFilters);
  const [categoryForm, setCategoryForm] = useState(createInitialCategoryForm);
  const [costCenterForm, setCostCenterForm] = useState(createInitialCostCenterForm);
  const [revenueForm, setRevenueForm] = useState(createInitialRevenueForm);
  const [expenseForm, setExpenseForm] = useState(createInitialExpenseForm);
  const [receivableForm, setReceivableForm] = useState(createInitialReceivableForm);
  const [payableForm, setPayableForm] = useState(createInitialPayableForm);
  const [paymentForm, setPaymentForm] = useState(createInitialPaymentForm);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const revenueCategories = useMemo(
    () => categories.filter((item) => item.isActive && (item.directionCode === 1 || item.directionCode === 3)),
    [categories]
  );
  const expenseCategories = useMemo(
    () => categories.filter((item) => item.isActive && (item.directionCode === 2 || item.directionCode === 3)),
    [categories]
  );

  useEffect(() => {
    if (!token) {
      return;
    }

    void loadFinance(token, filters);
  }, [token]);

  async function loadFinance(currentToken: string, currentFilters: typeof filters) {
    try {
      setIsLoading(true);
      setError(null);

      const query = {
        fromUtc: fromLocalDateTimeInput(currentFilters.fromUtc) ?? undefined,
        toUtc: fromLocalDateTimeInput(currentFilters.toUtc) ?? undefined,
        categoryId: currentFilters.categoryId || undefined,
        costCenterId: currentFilters.costCenterId || undefined
      };

      const reconciledFilter = currentFilters.reconciled || undefined;

      const [overviewData, categoriesData, costCentersData, studentsData, revenuesData, expensesData, receivablesData, payablesData] = await Promise.all([
        getFinanceOverview(currentToken, query),
        getFinancialCategories(currentToken),
        getCostCenters(currentToken),
        getStudents(currentToken),
        getRevenueEntries(currentToken, { ...query, reconciled: reconciledFilter }),
        getExpenseEntries(currentToken, { ...query, reconciled: reconciledFilter }),
        getReceivableEntries(currentToken, {
          fromDueUtc: query.fromUtc,
          toDueUtc: query.toUtc,
          categoryId: query.categoryId,
          costCenterId: query.costCenterId,
          reconciled: reconciledFilter,
          includeSettled: true
        }),
        getPayableEntries(currentToken, {
          fromDueUtc: query.fromUtc,
          toDueUtc: query.toUtc,
          categoryId: query.categoryId,
          costCenterId: query.costCenterId,
          reconciled: reconciledFilter,
          includeSettled: true
        })
      ]);

      setOverview(overviewData);
      setCategories(categoriesData);
      setCostCenters(costCentersData);
      setStudents(studentsData.map((item) => ({ id: item.id, fullName: item.fullName })));
      setRevenues(revenuesData);
      setExpenses(expensesData);
      setReceivables(receivablesData);
      setPayables(payablesData);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível carregar o financeiro.");
    } finally {
      setIsLoading(false);
    }
  }

  async function reload() {
    if (!token) {
      return;
    }

    await loadFinance(token, filters);
  }

  async function handleCategorySubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSaving("category");
      setError(null);
      await upsertFinancialCategory(token, {
        id: categoryForm.id || null,
        name: categoryForm.name,
        direction: Number(categoryForm.direction),
        isActive: categoryForm.isActive,
        sortOrder: Number(categoryForm.sortOrder) || 0
      });
      setCategoryForm(createInitialCategoryForm());
      await reload();
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar a categoria.");
    } finally {
      setIsSaving(null);
    }
  }

  async function handleCostCenterSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSaving("cost-center");
      setError(null);
      await upsertCostCenter(token, {
        id: costCenterForm.id || null,
        name: costCenterForm.name,
        description: costCenterForm.description || undefined,
        isActive: costCenterForm.isActive
      });
      setCostCenterForm(createInitialCostCenterForm());
      await reload();
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar o centro de custo.");
    } finally {
      setIsSaving(null);
    }
  }

  async function handleRevenueSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSaving("revenue");
      setError(null);

      const selectedCategory = revenueCategories.find((item) => item.id === revenueForm.categoryId);
      const payload = {
        sourceType: Number(revenueForm.sourceType),
        sourceId: revenueForm.sourceId || null,
        categoryId: revenueForm.categoryId || null,
        category: selectedCategory?.name ?? revenueForm.category,
        costCenterId: revenueForm.costCenterId || null,
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
      await reload();
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar a receita.");
    } finally {
      setIsSaving(null);
    }
  }

  async function handleExpenseSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSaving("expense");
      setError(null);

      const selectedCategory = expenseCategories.find((item) => item.id === expenseForm.categoryId);
      const payload = {
        category: Number(expenseForm.category),
        categoryId: expenseForm.categoryId || null,
        categoryName: (selectedCategory?.name ?? expenseForm.categoryName) || undefined,
        costCenterId: expenseForm.costCenterId || null,
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
      await reload();
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar a despesa.");
    } finally {
      setIsSaving(null);
    }
  }

  async function handleReceivableSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSaving("receivable");
      setError(null);

      const selectedStudent = students.find((item) => item.id === receivableForm.studentId);
      const selectedCategory = revenueCategories.find((item) => item.id === receivableForm.categoryId);
      const payload = {
        studentId: receivableForm.studentId,
        studentNameSnapshot: selectedStudent?.fullName ?? "",
        enrollmentId: receivableForm.enrollmentId || null,
        categoryId: receivableForm.categoryId || null,
        categoryName: (selectedCategory?.name ?? receivableForm.categoryName) || undefined,
        costCenterId: receivableForm.costCenterId || null,
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
      await reload();
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar a conta a receber.");
    } finally {
      setIsSaving(null);
    }
  }

  async function handlePayableSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) {
      return;
    }

    try {
      setIsSaving("payable");
      setError(null);

      const selectedCategory = expenseCategories.find((item) => item.id === payableForm.categoryId);
      const payload = {
        categoryId: payableForm.categoryId || null,
        categoryName: (selectedCategory?.name ?? payableForm.categoryName) || undefined,
        costCenterId: payableForm.costCenterId || null,
        amount: parseDecimalInput(payableForm.amount),
        dueAtUtc: fromLocalDateTimeInput(payableForm.dueAtUtc) ?? new Date().toISOString(),
        description: payableForm.description,
        notes: payableForm.notes || undefined,
        vendor: payableForm.vendor || undefined
      };

      if (payableForm.id) {
        await updatePayableEntry(token, payableForm.id, payload);
      } else {
        await createPayableEntry(token, payload);
      }

      setPayableForm(createInitialPayableForm());
      await reload();
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível salvar a conta a pagar.");
    } finally {
      setIsSaving(null);
    }
  }

  async function handlePaymentSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token || !paymentForm.entryId) {
      return;
    }

    try {
      setIsSaving("payment");
      setError(null);

      const payload = {
        amount: parseDecimalInput(paymentForm.amount),
        paidAtUtc: fromLocalDateTimeInput(paymentForm.paidAtUtc) ?? new Date().toISOString(),
        note: paymentForm.note || undefined
      };

      if (paymentForm.kind === "receivable") {
        await registerReceivablePayment(token, paymentForm.entryId, payload);
      } else {
        await registerPayablePayment(token, paymentForm.entryId, payload);
      }

      setPaymentForm(createInitialPaymentForm());
      await reload();
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível registrar a baixa financeira.");
    } finally {
      setIsSaving(null);
    }
  }

  async function handleReconciliation(kind: "revenue" | "expense" | "receivable" | "payable", entryId: string, isReconciled: boolean) {
    if (!token) {
      return;
    }

    try {
      setError(null);
      if (isReconciled) {
        await unreconcileFinancialEntry(token, kind, entryId);
      } else {
        await reconcileFinancialEntry(token, kind, entryId, {});
      }
      await reload();
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível atualizar a conciliação.");
    }
  }

  async function handleExport(kind: "revenues" | "expenses" | "receivables" | "payables") {
    if (!token) {
      return;
    }

    try {
      setError(null);
      const blob = await downloadFinanceExport(token, kind, {
        fromUtc: fromLocalDateTimeInput(filters.fromUtc) ?? undefined,
        toUtc: fromLocalDateTimeInput(filters.toUtc) ?? undefined
      });

      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `${kind}-${new Date().toISOString().slice(0, 10)}.csv`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível exportar os dados.");
    }
  }

  async function handleDeleteRevenue(revenueId: string) {
    if (!token || !window.confirm("Deseja excluir esta receita?")) {
      return;
    }

    try {
      await deleteRevenueEntry(token, revenueId);
      if (revenueForm.id === revenueId) {
        setRevenueForm(createInitialRevenueForm());
      }
      await reload();
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível excluir a receita.");
    }
  }

  async function handleDeleteExpense(expenseId: string) {
    if (!token || !window.confirm("Deseja excluir esta despesa?")) {
      return;
    }

    try {
      await deleteExpenseEntry(token, expenseId);
      if (expenseForm.id === expenseId) {
        setExpenseForm(createInitialExpenseForm());
      }
      await reload();
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível excluir a despesa.");
    }
  }

  async function handleDeleteReceivable(receivableId: string) {
    if (!token || !window.confirm("Deseja excluir esta conta a receber?")) {
      return;
    }

    try {
      await deleteReceivableEntry(token, receivableId);
      if (receivableForm.id === receivableId) {
        setReceivableForm(createInitialReceivableForm());
      }
      await reload();
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível excluir a conta a receber.");
    }
  }

  async function handleDeletePayable(payableId: string) {
    if (!token || !window.confirm("Deseja excluir esta conta a pagar?")) {
      return;
    }

    try {
      await deletePayableEntry(token, payableId);
      if (payableForm.id === payableId) {
        setPayableForm(createInitialPayableForm());
      }
      await reload();
    } catch (nextError) {
      setError(nextError instanceof Error ? nextError.message : "Não foi possível excluir a conta a pagar.");
    }
  }

  const summaryStats = [
    { label: "Receita", value: formatCurrency(overview?.totalRevenue) },
    { label: "Despesa", value: formatCurrency(overview?.totalExpense) },
    { label: "Receber", value: formatCurrency(overview?.receivablesOpenAmount) },
    { label: "Pagar", value: formatCurrency(overview?.payablesOpenAmount) }
  ];

  return (
    <div className="space-y-6">
      <PageHero
        eyebrow="Financeiro"
        title="Operação financeira integrada com matrícula, aula, cobrança e conciliação."
        description="Acompanhe categorias, centros de custo, contas a pagar, contas a receber e margens por curso, instrutor e período sem sair do contexto operacional."
        stats={summaryStats}
        statsBelow
      />

      {isLoading ? <LoadingBlock label="Carregando visão financeira" /> : null}
      {error ? <ErrorBlock message={error} /> : null}
      <GlassCard
        title="Filtro e exportações"
        description="Use a mesma janela para visão gerencial, listas financeiras e exportações em CSV."
      >
        <form className="grid gap-3 xl:grid-cols-[1fr_1fr_0.9fr_0.9fr_0.7fr_auto]" onSubmit={(event) => {
          event.preventDefault();
          void reload();
        }}>
          <label className="grid gap-2 text-sm text-[var(--q-text)]">
            <span>Data inicial</span>
            <input
              className={inputClassName}
              type="datetime-local"
              value={filters.fromUtc}
              onChange={(event) => setFilters((current) => ({ ...current, fromUtc: event.target.value }))}
            />
          </label>
          <label className="grid gap-2 text-sm text-[var(--q-text)]">
            <span>Data final</span>
            <input
              className={inputClassName}
              type="datetime-local"
              value={filters.toUtc}
              onChange={(event) => setFilters((current) => ({ ...current, toUtc: event.target.value }))}
            />
          </label>
          <LabeledInput
            label="Categoria"
            as="select"
            value={filters.categoryId}
            onChange={(value) => setFilters((current) => ({ ...current, categoryId: value }))}
            options={[
              { value: "", label: "Todas" },
              ...categories.filter((item) => item.isActive).map((item) => ({ value: item.id, label: item.name }))
            ]}
          />
          <LabeledInput
            label="Centro de custo"
            as="select"
            value={filters.costCenterId}
            onChange={(value) => setFilters((current) => ({ ...current, costCenterId: value }))}
            options={[
              { value: "", label: "Todos" },
              ...costCenters.filter((item) => item.isActive).map((item) => ({ value: item.id, label: item.name }))
            ]}
          />
          <LabeledInput
            label="Conciliação"
            as="select"
            value={filters.reconciled}
            onChange={(value) => setFilters((current) => ({ ...current, reconciled: value }))}
            options={[
              { value: "", label: "Tudo" },
              { value: "true", label: "Conciliado" },
              { value: "false", label: "Pendente" }
            ]}
          />
          <div className="flex items-end gap-3">
            <button className={actionButtonClassName} type="submit">
              Aplicar
            </button>
            <button
              className={secondaryButtonClassName}
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

        <div className="mt-5 flex flex-wrap gap-3">
          <button className={secondaryButtonClassName} type="button" onClick={() => void handleExport("revenues")}>
            Exportar receitas
          </button>
          <button className={secondaryButtonClassName} type="button" onClick={() => void handleExport("expenses")}>
            Exportar despesas
          </button>
          <button className={secondaryButtonClassName} type="button" onClick={() => void handleExport("receivables")}>
            Exportar recebimentos
          </button>
          <button className={secondaryButtonClassName} type="button" onClick={() => void handleExport("payables")}>
            Exportar pagamentos
          </button>
        </div>
      </GlassCard>

      {!isLoading && overview ? (
        <>
          <StatGrid
            items={[
              { label: "Receber em atraso", value: formatCurrency(overview.receivablesOverdueAmount), tone: "amber" },
              { label: "Pagar em atraso", value: formatCurrency(overview.payablesOverdueAmount), tone: "amber" },
              { label: "Margem", value: formatCurrency(overview.grossMargin), tone: "emerald" },
              { label: "Custo instrutores", value: formatCurrency(overview.instructorPayrollExpense), tone: "cyan" }
            ]}
          />

          <div className="grid gap-4 xl:grid-cols-3">
            <GlassCard title="Situação de recebimento" description="Leitura rápida da carteira de contas a receber e do comportamento de pagamento dos alunos.">
              <div className="grid gap-3 md:grid-cols-2">
                <SummaryMetric label="Em aberto" value={formatCurrency(overview.receivablesOpenAmount)} />
                <SummaryMetric label="Em atraso" value={formatCurrency(overview.receivablesOverdueAmount)} />
                <SummaryMetric label="Alunos inadimplentes" value={String(overview.delinquentStudents)} />
                <SummaryMetric label="Alunos a vencer" value={String(overview.dueSoonStudents)} />
              </div>
            </GlassCard>

            <GlassCard title="Conciliação manual" description="Acompanhe o quanto já foi conciliado e o que ainda precisa de conferência manual.">
              <div className="grid gap-3 md:grid-cols-2">
                <SummaryMetric label="Receita conciliada" value={formatCurrency(overview.reconciledRevenueAmount)} />
                <SummaryMetric label="Receita pendente" value={formatCurrency(overview.unreconciledRevenueAmount)} />
                <SummaryMetric label="Despesa conciliada" value={formatCurrency(overview.reconciledExpenseAmount)} />
                <SummaryMetric label="Despesa pendente" value={formatCurrency(overview.unreconciledExpenseAmount)} />
              </div>
            </GlassCard>

            <GlassCard title="Centros de custo" description="Margem consolidada por centro de custo para entender onde a operação ganha ou consome caixa.">
              <div className="space-y-3">
                {overview.costCenterMargins.length > 0 ? (
                  overview.costCenterMargins.slice(0, 4).map((item) => (
                    <div key={item.costCenterName} className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
                      <div className="font-medium text-[var(--q-text)]">{item.costCenterName}</div>
                      <div className="mt-2 text-sm text-[var(--q-text-2)]">
                        Receita {formatCurrency(item.revenue)} · Despesa {formatCurrency(item.expense)}
                      </div>
                      <div className="mt-3 text-lg font-semibold text-[var(--q-text)]">{formatCurrency(item.grossMargin)}</div>
                    </div>
                  ))
                ) : (
                  <EmptyState message="Nenhum centro de custo impactou o período filtrado." />
                )}
              </div>
            </GlassCard>
          </div>

          <div className="grid gap-4 xl:grid-cols-2">
            <GlassCard title="Margem por curso" description="Combina receita reconhecida com entrega operacional e custo de instrutores para o período.">
              <MarginTable items={overview.marginByCourse} />
            </GlassCard>
            <GlassCard title="Margem por instrutor" description="Mostra a receita atribuída pela operação e o custo real de cada instrutor no período.">
              <MarginTable items={overview.marginByInstructor} />
            </GlassCard>
          </div>
        </>
      ) : null}

      <div className="grid gap-4 xl:grid-cols-2">
        <GlassCard title="Categorias financeiras" description="Configure categorias próprias da escola para receitas, despesas ou uso misto.">
          <form className="grid gap-3" onSubmit={handleCategorySubmit}>
            <div className="grid gap-3 md:grid-cols-[1.4fr_0.8fr_0.6fr]">
              <LabeledInput label="Nome da categoria" value={categoryForm.name} onChange={(value) => setCategoryForm((current) => ({ ...current, name: value }))} />
              <LabeledInput
                label="Direção"
                as="select"
                value={categoryForm.direction}
                onChange={(value) => setCategoryForm((current) => ({ ...current, direction: value }))}
                options={[
                  { value: "1", label: "Receita" },
                  { value: "2", label: "Despesa" },
                  { value: "3", label: "Ambos" }
                ]}
              />
              <LabeledInput
                label="Ordem"
                value={categoryForm.sortOrder}
                onChange={(value) => setCategoryForm((current) => ({ ...current, sortOrder: value }))}
              />
            </div>
            <div className="flex flex-wrap gap-3">
              <button className={actionButtonClassName} type="submit" disabled={isSaving === "category"}>
                {isSaving === "category" ? "Salvando" : "Salvar categoria"}
              </button>
              <button className={secondaryButtonClassName} type="button" onClick={() => setCategoryForm(createInitialCategoryForm())}>
                Limpar
              </button>
            </div>
          </form>
          <div className="mt-5 space-y-3">
            {categories.length > 0 ? categories.map((item) => (
              <div key={item.id} className="flex flex-wrap items-center justify-between gap-3 rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3">
                <div>
                  <div className="font-medium text-[var(--q-text)]">{item.name}</div>
                  <div className="mt-1 text-xs uppercase tracking-[0.2em] text-[var(--q-muted)]">
                    {translateLabel(item.direction)} · ordem {item.sortOrder}
                  </div>
                </div>
                <StatusBadge value={item.isActive ? "Active" : "Inactive"} />
              </div>
            )) : <EmptyState message="Nenhuma categoria financeira configurada para esta escola." />}
          </div>
        </GlassCard>

        <GlassCard title="Centros de custo" description="Classifique receita e despesa por unidade operacional, praia, projeto ou outra visão gerencial.">
          <form className="grid gap-3" onSubmit={handleCostCenterSubmit}>
            <LabeledInput label="Nome do centro de custo" value={costCenterForm.name} onChange={(value) => setCostCenterForm((current) => ({ ...current, name: value }))} />
            <LabeledInput
              label="Descrição"
              value={costCenterForm.description}
              onChange={(value) => setCostCenterForm((current) => ({ ...current, description: value }))}
            />
            <div className="flex flex-wrap gap-3">
              <button className={actionButtonClassName} type="submit" disabled={isSaving === "cost-center"}>
                {isSaving === "cost-center" ? "Salvando" : "Salvar centro"}
              </button>
              <button className={secondaryButtonClassName} type="button" onClick={() => setCostCenterForm(createInitialCostCenterForm())}>
                Limpar
              </button>
            </div>
          </form>
          <div className="mt-5 space-y-3">
            {costCenters.length > 0 ? costCenters.map((item) => (
              <div key={item.id} className="flex flex-wrap items-center justify-between gap-3 rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3">
                <div>
                  <div className="font-medium text-[var(--q-text)]">{item.name}</div>
                  <div className="mt-1 text-sm text-[var(--q-text-2)]">{item.description || "Sem descrição"}</div>
                </div>
                <StatusBadge value={item.isActive ? "Active" : "Inactive"} />
              </div>
            )) : <EmptyState message="Nenhum centro de custo cadastrado ainda." />}
          </div>
        </GlassCard>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <FinanceEntryFormCard
          title={revenueForm.id ? "Editar receita" : "Nova receita"}
          description="Use categorias e centro de custo para manter a receita pronta para análises e exportações."
          footer={(
            <FormActions
              formId="finance-revenue-form"
              isSaving={isSaving === "revenue"}
              saveLabel={revenueForm.id ? "Atualizar receita" : "Salvar receita"}
              onClear={() => setRevenueForm(createInitialRevenueForm())}
            />
          )}
        >
          <form id="finance-revenue-form" className="grid gap-3" onSubmit={handleRevenueSubmit}>
            <div className="grid gap-3 md:grid-cols-2">
              <LabeledInput
                label="Origem"
                as="select"
                value={revenueForm.sourceType}
                onChange={(value) => setRevenueForm((current) => ({ ...current, sourceType: value }))}
                options={revenueSourceOptions.map((item) => ({ value: String(item.value), label: item.label }))}
              />
              <LabeledInput
                label="Centro de custo"
                as="select"
                value={revenueForm.costCenterId}
                onChange={(value) => setRevenueForm((current) => ({ ...current, costCenterId: value }))}
                options={[
                  { value: "", label: "Sem centro de custo" },
                  ...costCenters.filter((item) => item.isActive).map((item) => ({ value: item.id, label: item.name }))
                ]}
              />
            </div>
            <div className="grid gap-3 md:grid-cols-2">
              <LabeledInput
                label="Categoria financeira"
                as="select"
                value={revenueForm.categoryId}
                onChange={(value) => {
                  const selected = revenueCategories.find((item) => item.id === value);
                  setRevenueForm((current) => ({
                    ...current,
                    categoryId: value,
                    category: selected ? selected.name : current.category
                  }));
                }}
                options={[
                  { value: "", label: "Categoria manual" },
                  ...revenueCategories.map((item) => ({ value: item.id, label: item.name }))
                ]}
              />
              <LabeledInput
                label="Categoria exibida"
                value={revenueForm.category}
                onChange={(value) => setRevenueForm((current) => ({ ...current, category: value }))}
                placeholder="Ex.: Matrícula, aula avulsa"
                disabled={Boolean(revenueForm.categoryId)}
              />
            </div>
            <div className="grid gap-3 md:grid-cols-2">
              <LabeledInput label="Valor" value={revenueForm.amount} onChange={(value) => setRevenueForm((current) => ({ ...current, amount: value }))} placeholder="0,00" />
              <LabeledInput label="Reconhecida em" type="datetime-local" value={revenueForm.recognizedAtUtc} onChange={(value) => setRevenueForm((current) => ({ ...current, recognizedAtUtc: value }))} />
            </div>
            <LabeledInput label="Identificador de origem" value={revenueForm.sourceId} onChange={(value) => setRevenueForm((current) => ({ ...current, sourceId: value }))} placeholder="Opcional" />
            <LabeledTextArea label="Descrição" value={revenueForm.description} onChange={(value) => setRevenueForm((current) => ({ ...current, description: value }))} placeholder="Explique o fato econômico registrado." />
          </form>
        </FinanceEntryFormCard>

        <FinanceEntryFormCard
          title={expenseForm.id ? "Editar despesa" : "Nova despesa"}
          description="Despesas manuais convivem com custos automáticos e podem ser classificadas pela escola."
          footer={(
            <FormActions
              formId="finance-expense-form"
              isSaving={isSaving === "expense"}
              saveLabel={expenseForm.id ? "Atualizar despesa" : "Salvar despesa"}
              onClear={() => setExpenseForm(createInitialExpenseForm())}
            />
          )}
        >
          <form id="finance-expense-form" className="grid gap-3" onSubmit={handleExpenseSubmit}>
            <div className="grid gap-3 md:grid-cols-2">
              <LabeledInput
                label="Categoria operacional"
                as="select"
                value={expenseForm.category}
                onChange={(value) => setExpenseForm((current) => ({ ...current, category: value }))}
                options={expenseCategoryOptions.map((item) => ({ value: String(item.value), label: item.label }))}
              />
              <LabeledInput
                label="Centro de custo"
                as="select"
                value={expenseForm.costCenterId}
                onChange={(value) => setExpenseForm((current) => ({ ...current, costCenterId: value }))}
                options={[
                  { value: "", label: "Sem centro de custo" },
                  ...costCenters.filter((item) => item.isActive).map((item) => ({ value: item.id, label: item.name }))
                ]}
              />
            </div>
            <div className="grid gap-3 md:grid-cols-2">
              <LabeledInput
                label="Categoria financeira"
                as="select"
                value={expenseForm.categoryId}
                onChange={(value) => {
                  const selected = expenseCategories.find((item) => item.id === value);
                  setExpenseForm((current) => ({
                    ...current,
                    categoryId: value,
                    categoryName: selected ? selected.name : current.categoryName
                  }));
                }}
                options={[
                  { value: "", label: "Sem categoria financeira" },
                  ...expenseCategories.map((item) => ({ value: item.id, label: item.name }))
                ]}
              />
              <LabeledInput label="Fornecedor" value={expenseForm.vendor} onChange={(value) => setExpenseForm((current) => ({ ...current, vendor: value }))} />
            </div>
            <div className="grid gap-3 md:grid-cols-2">
              <LabeledInput label="Valor" value={expenseForm.amount} onChange={(value) => setExpenseForm((current) => ({ ...current, amount: value }))} placeholder="0,00" />
              <LabeledInput label="Ocorrida em" type="datetime-local" value={expenseForm.occurredAtUtc} onChange={(value) => setExpenseForm((current) => ({ ...current, occurredAtUtc: value }))} />
            </div>
            <LabeledTextArea label="Descrição" value={expenseForm.description} onChange={(value) => setExpenseForm((current) => ({ ...current, description: value }))} placeholder="Explique o gasto registrado." />
          </form>
        </FinanceEntryFormCard>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <FinanceEntryFormCard
          title={receivableForm.id ? "Editar conta a receber" : "Nova conta a receber"}
          description="Contas a receber registram status financeiro real do aluno e alimentam a carteira da escola."
          footer={(
            <FormActions
              formId="finance-receivable-form"
              isSaving={isSaving === "receivable"}
              saveLabel={receivableForm.id ? "Atualizar recebimento" : "Salvar recebimento"}
              onClear={() => setReceivableForm(createInitialReceivableForm())}
            />
          )}
        >
          <form id="finance-receivable-form" className="grid gap-3" onSubmit={handleReceivableSubmit}>
            <LabeledInput
              label="Aluno"
              as="select"
              value={receivableForm.studentId}
              onChange={(value) => setReceivableForm((current) => ({ ...current, studentId: value }))}
              options={[
                { value: "", label: "Selecione o aluno" },
                ...students.map((item) => ({ value: item.id, label: item.fullName }))
              ]}
            />
            <div className="grid gap-3 md:grid-cols-2">
              <LabeledInput
                label="Categoria financeira"
                as="select"
                value={receivableForm.categoryId}
                onChange={(value) => {
                  const selected = revenueCategories.find((item) => item.id === value);
                  setReceivableForm((current) => ({
                    ...current,
                    categoryId: value,
                    categoryName: selected ? selected.name : current.categoryName
                  }));
                }}
                options={[
                  { value: "", label: "Sem categoria financeira" },
                  ...revenueCategories.map((item) => ({ value: item.id, label: item.name }))
                ]}
              />
              <LabeledInput
                label="Centro de custo"
                as="select"
                value={receivableForm.costCenterId}
                onChange={(value) => setReceivableForm((current) => ({ ...current, costCenterId: value }))}
                options={[
                  { value: "", label: "Sem centro de custo" },
                  ...costCenters.filter((item) => item.isActive).map((item) => ({ value: item.id, label: item.name }))
                ]}
              />
            </div>
            <div className="grid gap-3 md:grid-cols-2">
              <LabeledInput label="Valor" value={receivableForm.amount} onChange={(value) => setReceivableForm((current) => ({ ...current, amount: value }))} placeholder="0,00" />
              <LabeledInput label="Vencimento" type="datetime-local" value={receivableForm.dueAtUtc} onChange={(value) => setReceivableForm((current) => ({ ...current, dueAtUtc: value }))} />
            </div>
            <LabeledInput label="Descrição" value={receivableForm.description} onChange={(value) => setReceivableForm((current) => ({ ...current, description: value }))} />
            <LabeledTextArea label="Notas" value={receivableForm.notes} onChange={(value) => setReceivableForm((current) => ({ ...current, notes: value }))} placeholder="Opcional" />
          </form>
        </FinanceEntryFormCard>

        <FinanceEntryFormCard
          title={payableForm.id ? "Editar conta a pagar" : "Nova conta a pagar"}
          description="Controle vencimento, fornecedor e classificação da despesa antes mesmo da baixa financeira."
          footer={(
            <FormActions
              formId="finance-payable-form"
              isSaving={isSaving === "payable"}
              saveLabel={payableForm.id ? "Atualizar pagamento" : "Salvar conta a pagar"}
              onClear={() => setPayableForm(createInitialPayableForm())}
            />
          )}
        >
          <form id="finance-payable-form" className="grid gap-3" onSubmit={handlePayableSubmit}>
            <div className="grid gap-3 md:grid-cols-2">
              <LabeledInput
                label="Categoria financeira"
                as="select"
                value={payableForm.categoryId}
                onChange={(value) => {
                  const selected = expenseCategories.find((item) => item.id === value);
                  setPayableForm((current) => ({
                    ...current,
                    categoryId: value,
                    categoryName: selected ? selected.name : current.categoryName
                  }));
                }}
                options={[
                  { value: "", label: "Sem categoria financeira" },
                  ...expenseCategories.map((item) => ({ value: item.id, label: item.name }))
                ]}
              />
              <LabeledInput
                label="Centro de custo"
                as="select"
                value={payableForm.costCenterId}
                onChange={(value) => setPayableForm((current) => ({ ...current, costCenterId: value }))}
                options={[
                  { value: "", label: "Sem centro de custo" },
                  ...costCenters.filter((item) => item.isActive).map((item) => ({ value: item.id, label: item.name }))
                ]}
              />
            </div>
            <div className="grid gap-3 md:grid-cols-2">
              <LabeledInput label="Valor" value={payableForm.amount} onChange={(value) => setPayableForm((current) => ({ ...current, amount: value }))} placeholder="0,00" />
              <LabeledInput label="Vencimento" type="datetime-local" value={payableForm.dueAtUtc} onChange={(value) => setPayableForm((current) => ({ ...current, dueAtUtc: value }))} />
            </div>
            <LabeledInput label="Fornecedor" value={payableForm.vendor} onChange={(value) => setPayableForm((current) => ({ ...current, vendor: value }))} />
            <LabeledInput label="Descrição" value={payableForm.description} onChange={(value) => setPayableForm((current) => ({ ...current, description: value }))} />
            <LabeledTextArea label="Notas" value={payableForm.notes} onChange={(value) => setPayableForm((current) => ({ ...current, notes: value }))} placeholder="Opcional" />
          </form>
        </FinanceEntryFormCard>
      </div>

      <GlassCard title="Baixa financeira" description="Use este bloco para registrar pagamento recebido ou efetuado em aberto.">
        <form className="grid gap-3 md:grid-cols-[0.8fr_1fr_1fr_auto]" onSubmit={handlePaymentSubmit}>
          <LabeledInput
            label="Tipo"
            as="select"
            value={paymentForm.kind}
            onChange={(value) => setPaymentForm((current) => ({ ...current, kind: value as "receivable" | "payable" }))}
            options={[
              { value: "receivable", label: "Recebimento" },
              { value: "payable", label: "Pagamento" }
            ]}
          />
          <LabeledInput
            label={paymentForm.kind === "receivable" ? "Conta a receber" : "Conta a pagar"}
            as="select"
            value={paymentForm.entryId}
            onChange={(value) => setPaymentForm((current) => ({ ...current, entryId: value }))}
            options={[
              { value: "", label: "Selecione uma conta" },
              ...(paymentForm.kind === "receivable"
                ? receivables.filter((item) => item.remainingAmount > 0).map((item) => ({ value: item.id, label: `${item.studentNameSnapshot} · ${item.description}` }))
                : payables.filter((item) => item.remainingAmount > 0).map((item) => ({ value: item.id, label: `${item.description} · ${item.vendor || "Sem fornecedor"}` })))
            ]}
          />
          <div className="grid gap-3 md:grid-cols-2">
            <LabeledInput label="Valor" value={paymentForm.amount} onChange={(value) => setPaymentForm((current) => ({ ...current, amount: value }))} placeholder="0,00" />
            <LabeledInput label="Quando" type="datetime-local" value={paymentForm.paidAtUtc} onChange={(value) => setPaymentForm((current) => ({ ...current, paidAtUtc: value }))} />
          </div>
          <div className="flex items-end gap-3">
            <button className={actionButtonClassName} type="submit" disabled={isSaving === "payment"}>
              {isSaving === "payment" ? "Salvando" : "Registrar baixa"}
            </button>
          </div>
        </form>
      </GlassCard>

      <div className="grid gap-4 xl:grid-cols-2">
        <GlassCard title="Contas a receber" description="Situação financeira do recebimento com conciliação manual e baixa de pagamento.">
          <ReceivablesTable
            items={receivables}
            onEdit={(entry) => setReceivableForm({
              id: entry.id,
              studentId: entry.studentId,
              enrollmentId: entry.enrollmentId ?? "",
              categoryId: entry.categoryId ?? "",
              categoryName: entry.categoryName ?? "",
              costCenterId: entry.costCenterId ?? "",
              amount: String(entry.amount),
              dueAtUtc: toLocalDateTimeInput(entry.dueAtUtc),
              description: entry.description,
              notes: entry.notes ?? ""
            })}
            onReconcile={(entry) => void handleReconciliation("receivable", entry.id, Boolean(entry.reconciledAtUtc))}
            onReceive={(entry) => setPaymentForm({
              kind: "receivable",
              entryId: entry.id,
              amount: String(entry.remainingAmount),
              paidAtUtc: toLocalDateTimeInput(new Date().toISOString()),
              note: ""
            })}
            onDelete={(entry) => void handleDeleteReceivable(entry.id)}
          />
        </GlassCard>

        <GlassCard title="Contas a pagar" description="Controle manual de compromissos, vencimentos e liquidações da escola.">
          <PayablesTable
            items={payables}
            onEdit={(entry) => setPayableForm({
              id: entry.id,
              categoryId: entry.categoryId ?? "",
              categoryName: entry.categoryName ?? "",
              costCenterId: entry.costCenterId ?? "",
              amount: String(entry.amount),
              dueAtUtc: toLocalDateTimeInput(entry.dueAtUtc),
              description: entry.description,
              notes: entry.notes ?? "",
              vendor: entry.vendor ?? ""
            })}
            onReconcile={(entry) => void handleReconciliation("payable", entry.id, Boolean(entry.reconciledAtUtc))}
            onPay={(entry) => setPaymentForm({
              kind: "payable",
              entryId: entry.id,
              amount: String(entry.remainingAmount),
              paidAtUtc: toLocalDateTimeInput(new Date().toISOString()),
              note: ""
            })}
            onDelete={(entry) => void handleDeletePayable(entry.id)}
          />
        </GlassCard>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <GlassCard title="Receitas" description="Lançamentos financeiros já reconhecidos, com centro de custo e conciliação.">
          <RevenuesTable
            items={revenues}
            onEdit={(entry) => setRevenueForm({
              id: entry.id,
              sourceType: String(entry.sourceTypeCode),
              sourceId: entry.sourceId ?? "",
              categoryId: entry.categoryId ?? "",
              category: entry.category,
              costCenterId: entry.costCenterId ?? "",
              amount: String(entry.amount),
              recognizedAtUtc: toLocalDateTimeInput(entry.recognizedAtUtc),
              description: entry.description
            })}
            onReconcile={(entry) => void handleReconciliation("revenue", entry.id, Boolean(entry.reconciledAtUtc))}
            onDelete={(entry) => void handleDeleteRevenue(entry.id)}
          />
        </GlassCard>

        <GlassCard title="Despesas" description="Saídas financeiras da escola, incluindo classificação operacional e centro de custo.">
          <ExpensesTable
            items={expenses}
            onEdit={(entry) => setExpenseForm({
              id: entry.id,
              category: String(entry.categoryCode),
              categoryId: entry.categoryId ?? "",
              categoryName: entry.categoryName ?? "",
              costCenterId: entry.costCenterId ?? "",
              amount: String(entry.amount),
              occurredAtUtc: toLocalDateTimeInput(entry.occurredAtUtc),
              description: entry.description,
              vendor: entry.vendor ?? ""
            })}
            onReconcile={(entry) => void handleReconciliation("expense", entry.id, Boolean(entry.reconciledAtUtc))}
            onDelete={(entry) => void handleDeleteExpense(entry.id)}
          />
        </GlassCard>
      </div>
    </div>
  );
}

function FinanceEntryFormCard({
  title,
  description,
  children,
  footer
}: {
  title: string;
  description: string;
  children: ReactNode;
  footer: ReactNode;
}) {
  return (
    <GlassCard title={title} description={description}>
      {children}
      <div className="mt-4">{footer}</div>
    </GlassCard>
  );
}

function FormActions({
  formId,
  isSaving,
  saveLabel,
  onClear
}: {
  formId: string;
  isSaving: boolean;
  saveLabel: string;
  onClear: () => void;
}) {
  return (
    <div className="flex flex-wrap gap-3">
      <button className={actionButtonClassName} form={formId} type="submit" disabled={isSaving}>
        {isSaving ? "Salvando" : saveLabel}
      </button>
      <button className={secondaryButtonClassName} type="button" onClick={onClear}>
        Limpar
      </button>
    </div>
  );
}

function LabeledInput({
  label,
  value,
  onChange,
  placeholder,
  type = "text",
  as,
  options,
  disabled
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  type?: string;
  as?: "select";
  options?: Array<{ value: string; label: string }>;
  disabled?: boolean;
}) {
  return (
    <label className="grid gap-2 text-sm text-[var(--q-text)]">
      <span>{label}</span>
      {as === "select" ? (
        <select className={inputClassName} value={value} onChange={(event) => onChange(event.target.value)} disabled={disabled}>
          {options?.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      ) : (
        <input
          className={inputClassName}
          type={type}
          value={value}
          onChange={(event) => onChange(event.target.value)}
          placeholder={placeholder}
          disabled={disabled}
        />
      )}
    </label>
  );
}

function LabeledTextArea({
  label,
  value,
  onChange,
  placeholder
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
}) {
  return (
    <label className="grid gap-2 text-sm text-[var(--q-text)]">
      <span>{label}</span>
      <textarea
        className="min-h-24 rounded-2xl border border-[var(--q-border)] bg-[var(--q-surface-2)] px-4 py-3 text-sm text-[var(--q-text)] outline-none"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
      />
    </label>
  );
}

function SummaryMetric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[22px] border border-[var(--q-border)] bg-[var(--q-surface-2)] p-4">
      <div className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">{label}</div>
      <div className="mt-3 text-2xl font-semibold text-[var(--q-text)]">{value}</div>
    </div>
  );
}

function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-[22px] border border-dashed border-[var(--q-divider)] px-4 py-5 text-sm text-[var(--q-text-2)]">
      {message}
    </div>
  );
}

function MarginTable({ items }: { items: FinanceOverview["marginByCourse"] }) {
  if (items.length === 0) {
    return <EmptyState message="Nenhuma margem encontrada para o período selecionado." />;
  }

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
        <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
          <tr>
            <th className="pb-3">Nome</th>
            <th className="pb-3">Receita</th>
            <th className="pb-3">Folha</th>
            <th className="pb-3">Margem</th>
          </tr>
        </thead>
        <tbody>
          {items.slice(0, 8).map((item) => (
            <tr key={`${item.name}-${item.id ?? "none"}`} className="border-t border-[var(--q-border)]">
              <td className="py-3">
                <div className="font-medium text-[var(--q-text)]">{item.name}</div>
                <div className="mt-1 text-xs text-[var(--q-muted)]">
                  {item.realizedLessons} aula(s) · {formatMinutesAsHours(item.realizedMinutes)}
                </div>
              </td>
              <td className="py-3">{formatCurrency(item.recognizedRevenue || item.deliveredRevenue)}</td>
              <td className="py-3">{formatCurrency(item.payrollExpense)}</td>
              <td className="py-3 font-medium text-[var(--q-text)]">{formatCurrency(item.grossMargin)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ReceivablesTable({
  items,
  onEdit,
  onReconcile,
  onReceive,
  onDelete
}: {
  items: ReceivableEntry[];
  onEdit: (entry: ReceivableEntry) => void;
  onReconcile: (entry: ReceivableEntry) => void;
  onReceive: (entry: ReceivableEntry) => void;
  onDelete: (entry: ReceivableEntry) => void;
}) {
  if (items.length === 0) {
    return <EmptyState message="Nenhuma conta a receber encontrada para o filtro atual." />;
  }

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
        <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
          <tr>
            <th className="pb-3">Aluno</th>
            <th className="pb-3">Categoria</th>
            <th className="pb-3">Saldo</th>
            <th className="pb-3">Situação</th>
            <th className="pb-3">Ações</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id} className="border-t border-[var(--q-border)]">
              <td className="py-3">
                <div className="font-medium text-[var(--q-text)]">{item.studentNameSnapshot}</div>
                <div className="mt-1 text-xs text-[var(--q-muted)]">{item.description}</div>
                <div className="mt-1 text-xs text-[var(--q-muted)]">{formatDateTime(item.dueAtUtc)}</div>
              </td>
              <td className="py-3">
                <div>{item.categoryName || "Sem categoria"}</div>
                <div className="mt-1 text-xs text-[var(--q-muted)]">{item.costCenterName || "Sem centro"}</div>
              </td>
              <td className="py-3">
                <div className="font-medium text-[var(--q-text)]">{formatCurrency(item.remainingAmount)}</div>
                <div className="mt-1 text-xs text-[var(--q-muted)]">
                  Pago {formatCurrency(item.paidAmount)} · {item.paymentsCount} baixa(s)
                </div>
              </td>
              <td className="py-3 space-y-2">
                <StatusBadge value={item.isOverdue ? "Delinquent" : item.status} />
                <StatusBadge value={item.reconciledAtUtc ? "Conciliado" : "Pendente"} />
              </td>
              <td className="py-3">
                <div className="flex flex-wrap gap-2">
                  <TableButton label="Editar" onClick={() => onEdit(item)} />
                  {item.remainingAmount > 0 ? <TableButton label="Receber" onClick={() => onReceive(item)} tone="success" /> : null}
                  <TableButton label={item.reconciledAtUtc ? "Desconciliar" : "Conciliar"} onClick={() => onReconcile(item)} tone="info" />
                  <TableButton label="Excluir" onClick={() => onDelete(item)} tone="danger" />
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function PayablesTable({
  items,
  onEdit,
  onReconcile,
  onPay,
  onDelete
}: {
  items: PayableEntry[];
  onEdit: (entry: PayableEntry) => void;
  onReconcile: (entry: PayableEntry) => void;
  onPay: (entry: PayableEntry) => void;
  onDelete: (entry: PayableEntry) => void;
}) {
  if (items.length === 0) {
    return <EmptyState message="Nenhuma conta a pagar encontrada para o filtro atual." />;
  }

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
        <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
          <tr>
            <th className="pb-3">Descrição</th>
            <th className="pb-3">Categoria</th>
            <th className="pb-3">Saldo</th>
            <th className="pb-3">Situação</th>
            <th className="pb-3">Ações</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id} className="border-t border-[var(--q-border)]">
              <td className="py-3">
                <div className="font-medium text-[var(--q-text)]">{item.description}</div>
                <div className="mt-1 text-xs text-[var(--q-muted)]">{item.vendor || "Sem fornecedor"}</div>
                <div className="mt-1 text-xs text-[var(--q-muted)]">{formatDateTime(item.dueAtUtc)}</div>
              </td>
              <td className="py-3">
                <div>{item.categoryName || "Sem categoria"}</div>
                <div className="mt-1 text-xs text-[var(--q-muted)]">{item.costCenterName || "Sem centro"}</div>
              </td>
              <td className="py-3">
                <div className="font-medium text-[var(--q-text)]">{formatCurrency(item.remainingAmount)}</div>
                <div className="mt-1 text-xs text-[var(--q-muted)]">
                  Pago {formatCurrency(item.paidAmount)} · {item.paymentsCount} baixa(s)
                </div>
              </td>
              <td className="py-3 space-y-2">
                <StatusBadge value={item.isOverdue ? "Attention" : item.status} />
                <StatusBadge value={item.reconciledAtUtc ? "Conciliado" : "Pendente"} />
              </td>
              <td className="py-3">
                <div className="flex flex-wrap gap-2">
                  <TableButton label="Editar" onClick={() => onEdit(item)} />
                  {item.remainingAmount > 0 ? <TableButton label="Pagar" onClick={() => onPay(item)} tone="warning" /> : null}
                  <TableButton label={item.reconciledAtUtc ? "Desconciliar" : "Conciliar"} onClick={() => onReconcile(item)} tone="info" />
                  <TableButton label="Excluir" onClick={() => onDelete(item)} tone="danger" />
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function RevenuesTable({
  items,
  onEdit,
  onReconcile,
  onDelete
}: {
  items: RevenueEntry[];
  onEdit: (entry: RevenueEntry) => void;
  onReconcile: (entry: RevenueEntry) => void;
  onDelete: (entry: RevenueEntry) => void;
}) {
  if (items.length === 0) {
    return <EmptyState message="Nenhuma receita encontrada para o filtro atual." />;
  }

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
        <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
          <tr>
            <th className="pb-3">Origem</th>
            <th className="pb-3">Categoria</th>
            <th className="pb-3">Valor</th>
            <th className="pb-3">Ações</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id} className="border-t border-[var(--q-border)]">
              <td className="py-3">
                <div className="font-medium text-[var(--q-text)]">{translateLabel(item.sourceType)}</div>
                <div className="mt-1 text-xs text-[var(--q-muted)]">{formatDateTime(item.recognizedAtUtc)}</div>
              </td>
              <td className="py-3">
                <div>{item.category}</div>
                <div className="mt-1 text-xs text-[var(--q-muted)]">{item.costCenterName || "Sem centro"}</div>
              </td>
              <td className="py-3">
                <div className="font-medium text-[var(--q-text)]">{formatCurrency(item.amount)}</div>
                <div className="mt-1 text-xs text-[var(--q-muted)]">{item.description}</div>
              </td>
              <td className="py-3">
                <div className="flex flex-wrap gap-2">
                  <TableButton label="Editar" onClick={() => onEdit(item)} />
                  <TableButton label={item.reconciledAtUtc ? "Desconciliar" : "Conciliar"} onClick={() => onReconcile(item)} tone="info" />
                  <TableButton label="Excluir" onClick={() => onDelete(item)} tone="danger" />
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ExpensesTable({
  items,
  onEdit,
  onReconcile,
  onDelete
}: {
  items: ExpenseEntry[];
  onEdit: (entry: ExpenseEntry) => void;
  onReconcile: (entry: ExpenseEntry) => void;
  onDelete: (entry: ExpenseEntry) => void;
}) {
  if (items.length === 0) {
    return <EmptyState message="Nenhuma despesa encontrada para o filtro atual." />;
  }

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full text-left text-sm text-[var(--q-text-2)]">
        <thead className="text-xs uppercase tracking-[0.24em] text-[var(--q-muted)]">
          <tr>
            <th className="pb-3">Categoria</th>
            <th className="pb-3">Descrição</th>
            <th className="pb-3">Valor</th>
            <th className="pb-3">Ações</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id} className="border-t border-[var(--q-border)]">
              <td className="py-3">
                <div className="font-medium text-[var(--q-text)]">{translateLabel(item.category)}</div>
                <div className="mt-1 text-xs text-[var(--q-muted)]">{item.categoryName || "Sem categoria financeira"}</div>
                <div className="mt-1 text-xs text-[var(--q-muted)]">{item.costCenterName || "Sem centro"}</div>
              </td>
              <td className="py-3">
                <div>{item.description}</div>
                <div className="mt-1 text-xs text-[var(--q-muted)]">
                  {item.vendor || "Sem fornecedor"} · {formatDateTime(item.occurredAtUtc)}
                </div>
              </td>
              <td className="py-3 font-medium text-[var(--q-text)]">{formatCurrency(item.amount)}</td>
              <td className="py-3">
                <div className="flex flex-wrap gap-2">
                  <TableButton label="Editar" onClick={() => onEdit(item)} tone="warning" />
                  <TableButton label={item.reconciledAtUtc ? "Desconciliar" : "Conciliar"} onClick={() => onReconcile(item)} tone="info" />
                  <TableButton label="Excluir" onClick={() => onDelete(item)} tone="danger" />
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function TableButton({
  label,
  onClick,
  tone = "default"
}: {
  label: string;
  onClick: () => void;
  tone?: "default" | "info" | "success" | "warning" | "danger";
}) {
  const className =
    tone === "info"
      ? "border-[var(--q-info)]/25 bg-[var(--q-info-bg)] text-[var(--q-info)]"
      : tone === "success"
        ? "border-[var(--q-success)]/25 bg-[var(--q-success-bg)] text-[var(--q-success)]"
        : tone === "warning"
          ? "border-[var(--q-warning)]/25 bg-[var(--q-warning-bg)] text-[#B58100]"
          : tone === "danger"
            ? "border-rose-400/20 bg-rose-400/10 text-rose-600"
            : "border-[var(--q-border)] bg-[var(--q-surface-2)] text-[var(--q-text)]";

  return (
    <button
      className={`rounded-full border px-3 py-1 text-xs uppercase tracking-[0.2em] ${className}`}
      type="button"
      onClick={onClick}
    >
      {label}
    </button>
  );
}

function parseDecimalInput(value: string) {
  return Number((value || "0").replace(",", "."));
}

function formatMinutesAsHours(minutes: number) {
  if (minutes <= 0) {
    return "0h";
  }

  const hours = Math.round((minutes / 60) * 10) / 10;
  return `${hours}h`;
}
