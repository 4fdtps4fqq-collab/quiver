# Roadmap Faseado

## Diretrizes de execucao

Este roadmap segue os principios da arquitetura alvo:

- comecar com separacao pragmatica, sem granularidade artificial
- explicitar dono do fato operacional e dono do fato financeiro
- manter o gateway fino por padrao
- adotar multi-tenancy por `SchoolId` como regra estrutural
- evoluir de HTTP sincrono para eventos apenas quando os fluxos estabilizarem

## Fase 0. Fundacao

- criar nova solution paralela ao monolito
- definir servicos, gateway e frontend
- padronizar JWT, multi-tenancy e configuracao PostgreSQL
- subir infraestrutura local via Docker
- definir `SchoolId` como tenant key padrao
- padronizar correlation id, logs estruturados e health checks
- definir timeouts explicitos entre servicos
- registrar a regra de persistencia por servico, sem compartilhamento de tabelas

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
