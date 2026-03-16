export type PageSection = {
  title: string;
  description: string;
  metric: string;
};

export const pageSections: Record<string, PageSection[]> = {
  students: [
    { title: "Cadastro inteligente", description: "Prontuário com contato, observações médicas e histórico de stand-up.", metric: "1 tela para onboarding" },
    { title: "Pipeline operacional", description: "Visão de iniciantes, ativos e alunos em retomada para ação rápida da equipe.", metric: "3 segmentos" }
  ],
  instructors: [
    { title: "Grade e capacidade", description: "Distribuição por agenda, especialidade e disponibilidade operacional.", metric: "tempo ocioso visível" },
    { title: "Performance", description: "Aulas realizadas, remarcações e indicadores de eficiência por instrutor.", metric: "visão por período" }
  ],
  courses: [
    { title: "Catálogo comercial", description: "Cursos por nível, precificação e carga horária padronizados por escola.", metric: "snapshot no contrato" },
    { title: "Oferta flexível", description: "Base pronta para campanhas, combos e extensões futuras.", metric: "preparado para bundles" }
  ],
  enrollments: [
    { title: "Contrato congelado", description: "Carga horária e preço ficam registrados no momento da venda.", metric: "snapshot preservado" },
    { title: "Saldo auditável", description: "O ledger de consumo e estorno torna o saldo explicável e confiável.", metric: "rastreamento por aula" }
  ],
  lessons: [
    { title: "Agenda operacional", description: "Aulas avulsas e de curso convivem na mesma grade com regras diferentes.", metric: "2 fluxos suportados" },
    { title: "Estados da aula", description: "Realizada, remarcada, no-show e cancelamento por vento entram no ciclo operacional e analítico.", metric: "7 status-base" }
  ],
  equipment: [
    { title: "Inventário ativo", description: "Equipamentos por depósito, tipo, tag e condição atual em um painel claro.", metric: "uso acumulado em minutos" },
    { title: "Operação por aula", description: "Checkout/check-in ligado ao contexto da aula para rastreabilidade real.", metric: "histórico por item" }
  ],
  maintenance: [
    { title: "Preventiva e corretiva", description: "Regras por tipo de equipamento e registros de manutenção por item.", metric: "alertas por ciclo" },
    { title: "Risco operacional", description: "A condição final do equipamento afeta imediatamente a disponibilidade.", metric: "decisão na ponta" }
  ],
  finance: [
    { title: "Receita reconhecida", description: "Matrículas e aulas avulsas alimentam uma base financeira consistente.", metric: "margem operacional" },
    { title: "Despesas classificadas", description: "Custos operacionais, manutenção e folha entram em consolidação simples.", metric: "análise por categoria" }
  ],
  school: [
    { title: "Configuração do tenant", description: "Fuso horário, moeda, políticas operacionais e identidade visual por escola.", metric: "tenant-aware" },
    { title: "Controle administrativo", description: "Perfis, acessos e preferências para o backoffice em crescimento.", metric: "governança inicial" }
  ]
};
