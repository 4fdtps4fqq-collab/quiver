# Visao Arquitetural

## Norte da v2

O KiteFlow v2 sera uma plataforma modular com microsservicos pragmaticos, evitando granularidade artificial. A meta nao e decompor tudo desde o primeiro dia, e sim separar os contextos que ja apresentam regras, ciclo de vida e ritmo de mudanca diferentes.

## Contextos e servicos iniciais

### 1. Identity Service

Responsavel por autenticacao, emissao e validacao de tokens, credenciais, refresh sessions e regras centrais de acesso.

Principais responsabilidades:

- login, troca de senha e sessao
- emissao de JWT com `sub`, `school_id`, `role` e `email`
- bloqueio e ativacao de contas
- preparo para federacao futura

### 2. School & User Management Service

Responsavel pelo cadastro institucional da escola e pelos perfis administrativos e operacionais vinculados ao tenant.

Principais responsabilidades:

- escola e configuracoes da escola
- perfis de usuario da escola
- preferencia de idioma, moeda, timezone e branding
- onboarding administrativo da escola

### 3. Academic & Operations Service

Responsavel pelo nucleo operacional academico.

Principais responsabilidades:

- alunos
- instrutores
- cursos
- matriculas
- aulas e agenda
- saldo de matriculas e regras de consumo/devolucao

### 4. Equipment & Maintenance Service

Responsavel pelo ciclo de vida do equipamento e pela rastreabilidade operacional.

Principais responsabilidades:

- depositos/estoques
- cadastro de equipamentos
- checkout/checkin por aula
- historico de uso por aula
- condicao atual do equipamento
- regras e registros de manutencao

### 5. Finance Service

Responsavel pela visao financeira operacional e por consolidar despesas e receitas reconhecidas.

Principais responsabilidades:

- despesas
- receitas oriundas de matriculas e aulas avulsas
- classificacao financeira
- base para margem operacional

### 6. Reporting Service

Responsavel pela agregacao e exposicao de dashboards. Na fase inicial ele pode operar de forma sincrona, consultando os servicos fonte; no futuro recebera eventos e mantera read models proprios.

Principais responsabilidades:

- dashboard financeiro
- dashboard operacional
- uso de equipamento
- performance de instrutores
- alertas de manutencao

### 7. API Gateway / BFF

Ponto unico de entrada para frontend e clientes externos.

Principais responsabilidades:

- roteamento por servico
- terminacao de autenticacao para clientes
- politicas transversais futuras como rate limit, observabilidade e agregacao BFF

## Estrategia de multi-tenancy

- `SchoolId` continua sendo o tenant key principal
- JWT carrega `school_id`, `role`, `email` e `sub`
- quase todos os recursos sao tenant-scoped
- `SystemAdmin` pode operar sem `school_id`
- cada servico grava `SchoolId` em entidades relevantes e indexa consultas por tenant
- relacoes entre servicos nao usam foreign key fisica entre bancos; usam IDs externos e validacao aplicacional

## Estrategia de autenticacao e autorizacao

- autenticacao centralizada no `Identity Service`
- JWT assinado com chave simetrica na fundacao inicial
- validacao do token em todos os servicos e no gateway
- autorizacao baseada em roles: `SystemAdmin`, `Owner`, `Instructor`, `Student`
- politicas futuras por permissao podem ser adicionadas sem quebrar roles atuais

## Comunicacao entre servicos

### Fase inicial

- HTTP sincrono entre gateway e servicos
- Reporting agregando consultas sincronas
- Finance recebendo dados inicialmente por API

### Evolucao recomendada

- eventos de `EnrollmentCreated`, `LessonStatusChanged`, `EquipmentCheckedIn`, `MaintenanceRecorded`
- projections assincronas para Finance e Reporting
- outbox por servico para confiabilidade transacional

## Principios de implementacao

- .NET 8 + ASP.NET Core Web API
- EF Core + PostgreSQL
- DTOs e services apenas onde adicionam clareza
- evitar camadas artificiais e repositorios genericos
- contratos simples e evolutiveis
