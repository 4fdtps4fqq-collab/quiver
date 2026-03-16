import { Component, type ErrorInfo, type ReactNode } from "react";

type SectionErrorBoundaryProps = {
  children: ReactNode;
  fallbackTitle?: string;
  fallbackMessage?: string;
};

type SectionErrorBoundaryState = {
  hasError: boolean;
};

export class SectionErrorBoundary extends Component<
  SectionErrorBoundaryProps,
  SectionErrorBoundaryState
> {
  constructor(props: SectionErrorBoundaryProps) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(): SectionErrorBoundaryState {
    return { hasError: true };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error("Erro ao renderizar seção do backoffice:", error, errorInfo);
  }

  render() {
    if (!this.state.hasError) {
      return this.props.children;
    }

    return (
      <div className="rounded-[28px] border border-[var(--q-danger)]/25 bg-[var(--q-danger-bg)] p-5 text-[var(--q-text)] shadow-[var(--app-shadow-soft)]">
        <div className="text-sm font-semibold uppercase tracking-[0.24em] text-[var(--q-danger)]">
          {this.props.fallbackTitle ?? "Seção indisponível"}
        </div>
        <p className="mt-3 text-sm leading-6 text-[var(--q-text-2)]">
          {this.props.fallbackMessage ??
            "Não foi possível renderizar este bloco agora. Atualize a página e tente novamente."}
        </p>
      </div>
    );
  }
}
