# Roadmap Faseado

## Fase 0. Fundacao

- criar nova solution paralela ao monolito
- definir servicos, gateway e frontend
- padronizar JWT, multi-tenancy e configuracao PostgreSQL
- subir infraestrutura local via Docker

## Fase 1. Identity + Schools

- concluir onboarding de escola e owner
- login, refresh token e troca de senha
- perfis e configuracoes da escola
- regras de acesso por role

## Fase 2. Academic Core

- migrar alunos, instrutores, cursos e matriculas
- implementar agenda e aulas
- implementar ledger de saldo de matricula
- garantir regras `Single` x `Course`

## Fase 3. Equipment & Maintenance

- depositos, equipamentos e checkouts
- historico de uso por aula
- manutencao preventiva e corretiva
- alertas iniciais de manutencao

## Fase 4. Finance

- despesas
- receitas de matriculas e aulas avulsas
- visao de margem operacional

## Fase 5. Reporting

- dashboards agregados
- performance de instrutores
- utilizacao de equipamento
- alertas operacionais e financeiros

## Fase 6. Integracao e Escala

- eventos assincronos
- outbox/inbox
- cache seletivo
- observabilidade e tracing
- testes de contrato entre servicos
