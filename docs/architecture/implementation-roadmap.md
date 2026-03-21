# Roadmap Faseado

## Diretrizes de execucao

Este roadmap segue os principios da arquitetura alvo:

- comecar com separacao pragmatica, sem granularidade artificial
- explicitar dono do fato operacional e dono do fato financeiro
- manter o gateway fino por padrao
- adotar multi-tenancy por `SchoolId` como regra estrutural
- evoluir de HTTP sincrono para eventos apenas quando os fluxos estabilizarem

## Matriz de acompanhamento

Legenda:
- `Concluido`: implementado e validado na base atual
- `Parcial`: implementado em parte, com debitos tecnicos ou evolucao ainda pendente
- `Pendente`: ainda nao implementado de forma relevante

| Fase | Status | Leitura atual |
| --- | --- | --- |
| Fase 0. Fundacao | Concluido | Base pronta com solution, gateway, JWT, multi-tenancy, Docker local, correlation id, health checks, timeouts e migrations formais por servico, sem DDL residual no startup. |
| Fase 1. Identity + School Administration | Concluido | Onboarding central, owner, school administration, perfis, branding, auditoria, reset/troca de senha, sessao segura e base de permissoes finas estao operacionais. |
| Fase 2. Academic Core | Concluido | Alunos, instrutores, cursos, matriculas, agenda, aulas, regras de consumo e portal do aluno estao implementados. |
| Fase 3. Equipment & Maintenance | Concluido | Depositos, equipamentos, reservas, kits, manutencao, vida util e integracao com financeiro estao implementados. |
| Fase 4. Finance | Concluido | Receitas, despesas, contas a pagar/receber, categorias, centros de custo, conciliacao, margem e exportacoes estao implementados. |
| Fase 5. Reporting | Concluido | Dashboards, agregacoes orientadas a painel, cache leve, snapshots persistidos por tenant/periodo e alertas operacionais/financeiros agregados estao implementados. |
| Fase 6. Integracao e Escala | Pendente | Ainda faltam outbox/inbox, eventos assincronos, tracing distribuido, testes de contrato e politicas formais de resiliencia avancada. |
| Fase 7. Maturidade Operacional | Parcial | Permissoes granulares, hardening multi-tenant, readiness/liveness e auditoria avancaram bem. Ainda faltam metricas mais fortes, versionamento de contratos e maturidade operacional mais formal. |

## Regra de manutencao da matriz

- Sempre que um backlog relevante for concluido, esta matriz deve ser revisada no mesmo pacote de alteracoes.
- O status da fase deve refletir implementacao real no codigo, e nao apenas intencao de roadmap.
- Quando uma fase estiver `Parcial`, o debito remanescente deve ser descrito de forma objetiva na coluna `Leitura atual`.

## Fase 0. Fundacao

- criar nova solution paralela ao monolito
- definir servicos, gateway e frontend
- padronizar JWT, multi-tenancy e configuracao PostgreSQL
- subir infraestrutura local via Docker
- definir `SchoolId` como tenant key padrao
- padronizar correlation id, logs estruturados e health checks
- definir timeouts explicitos entre servicos
- registrar a regra de persistencia por servico, sem compartilhamento de tabelas

Estado atual:

- startup dos servicos faz apenas `MigrateAsync()` e bootstrap legitimo de aplicacao
- o schema evolui por migrations formais em `Identity`, `Schools`, `Academics`, `Equipment`, `Finance` e `Reporting`

## Fase 1. Identity + School Administration

- concluir onboarding central de escola e owner
- separar claramente `Identity Account` de `School Membership / User Profile`
- login, refresh token rotation, troca de senha e revogacao de sessao
- reset de senha e trilha de auditoria de autenticacao
- perfis e configuracoes da escola
- branding, timezone, moeda e preferencias do tenant
- regras de acesso por role como mecanismo inicial
- base pronta para permissoes finas por policy/claims

## Fase 2. Academic Core

- migrar alunos, instrutores, cursos e matriculas
- implementar agenda e aulas
- implementar ledger de saldo de matricula
- garantir regras `Single` x `Course`
- consolidar `Academic & Operations` como contexto unido na fase inicial
- registrar desde ja a possibilidade futura de divisao entre:
  - `Academic Service`
  - `Scheduling / Lesson Operations Service`

## Fase 3. Equipment & Maintenance

- depositos, equipamentos e checkouts
- disponibilidade e reserva previa por agenda
- historico de uso por aula
- manutencao preventiva e corretiva
- custos, receitas e rastreabilidade da manutencao
- alertas iniciais de manutencao
- historico de vida util do equipamento

## Fase 4. Finance

- modelar `Finance` como dono do fato financeiro
- receber fatos operacionais do core inicialmente por API
- despesas
- receitas de matriculas e aulas avulsas
- contas a pagar e contas a receber
- classificacao financeira
- visao de margem operacional

## Fase 5. Reporting

- dashboards agregados
- performance de instrutores
- utilizacao de equipamento
- alertas operacionais e financeiros
- comecar com agregacao sincrona orientada a dashboard
- limitar profundidade de composicao em tempo real
- aplicar cache onde fizer sentido
- preparar read models proprios assim que os paineis estabilizarem

Estado atual:

- `Reporting` possui snapshots persistidos por tenant e janela de consulta
- dashboards, leituras operacionais e leituras financeiras podem reutilizar read models recentes
- alertas operacionais e financeiros ja saem agregados do proprio servico

## Fase 6. Integracao e Escala

- eventos assincronos
- outbox/inbox
- read models para Finance e Reporting
- consumidores idempotentes e rastreaveis
- cache seletivo
- observabilidade e tracing distribuidos
- retries apenas em operacoes idempotentes
- circuit breaking e fallback controlado
- testes de contrato entre servicos

## Fase 7. Maturidade Operacional

- politicas de permissao mais granulares alem de role
- endurecimento multi-tenant com regressao automatizada
- auditoria expandida de autenticacao e acessos
- readiness/liveness por servico
- metricas basicas por contexto
- versionamento de APIs e contratos de eventos quando necessario
