# Visao Arquitetural

## Norte da v2

O KiteFlow v2 sera uma plataforma modular com microsservicos pragmaticos, evitando granularidade artificial. A meta nao e decompor tudo desde o primeiro dia, e sim separar os contextos que ja apresentam regras, ciclo de vida e ritmo de mudanca diferentes.

O principio orientador e simples:

- separar dominios que ja possuem regra propria e alta chance de evolucao independente
- evitar distribuicao prematura onde um modulo interno ainda atende bem
- comecar com integracoes sincronas claras
- evoluir para eventos, read models e automacao assincrona quando os fluxos estabilizarem

## Contextos e servicos iniciais

### 1. Identity Service

Responsavel por autenticacao, credenciais, emissao e validacao de tokens, sessoes e seguranca de acesso.

Entidade logica principal:

- `Identity Account`
  - email de acesso
  - password hash
  - refresh sessions
  - lockout
  - politica de senha
  - revogacao de sessao
  - auditoria de autenticacao

Principais responsabilidades:

- login, troca de senha, reset e sessao
- emissao de JWT com `sub`, `school_id`, `role`, `email` e claims de permissao
- refresh token rotation e revogacao
- bloqueio e ativacao de contas
- trilha de auditoria de login e acesso
- preparo para federation/OIDC real e MFA futuro

### 2. School Administration Service

Responsavel pelo cadastro institucional da escola, configuracoes do tenant e membership operacional vinculado a cada escola.

Entidade logica principal:

- `School Membership / User Profile`
  - nome
  - papel na escola
  - preferencias
  - branding contextual
  - vinculo com tenant
  - estado operacional dentro da escola

Principais responsabilidades:

- escola e configuracoes da escola
- perfis administrativos e operacionais vinculados ao tenant
- preferencia de idioma, moeda, timezone e branding
- onboarding administrativo da escola
- membership e permissoes contextuais por escola

Fronteira com `Identity Service`:

- `Identity` e dono da conta de acesso e das credenciais
- `School Administration` e dono do perfil institucional e do vinculo com a escola
- reset de senha, lockout e sessao pertencem a `Identity`
- nome, preferencia, papel no tenant e branding pertencem a `School Administration`

### 3. Academic & Operations Service

Responsavel pelo nucleo operacional academico.

Principais responsabilidades:

- alunos
- instrutores
- cursos
- matriculas
- aulas e agenda
- saldo de matriculas e regras de consumo/devolucao

Observacao evolutiva:

- esse e o contexto mais pesado da plataforma e pode continuar unido na fase inicial
- a divisao futura mais provavel, se o dominio crescer, e:
  - `Academic Service`: alunos, instrutores, cursos, matriculas
  - `Scheduling / Lesson Operations Service`: agenda, aulas, execucao operacional e consumo

### 4. Equipment & Maintenance Service

Responsavel pelo ciclo de vida do equipamento e pela rastreabilidade operacional.

Principais responsabilidades:

- depositos/estoques
- cadastro de equipamentos
- checkout/checkin por aula
- reserva previa e disponibilidade operacional
- historico de uso por aula
- condicao atual do equipamento
- regras e registros de manutencao

### 5. Finance Service

Responsavel pelo fato financeiro, classificacao e visao economica operacional.

Principais responsabilidades:

- despesas
- receitas oriundas de matriculas, aulas avulsas e servicos
- classificacao financeira
- contas a pagar e contas a receber
- base para margem operacional

Definicao de autoria:

- `Academic & Operations` e dono do fato operacional
- `Finance` e dono do fato financeiro
- a integracao entre os dois comeca sincrona por API
- a evolucao natural e migrar para eventos e reconhecimento financeiro mais desacoplado

### 6. Reporting Service

Responsavel pela agregacao e exposicao de dashboards. Na fase inicial ele pode operar de forma sincrona, consultando os servicos fonte; no futuro recebera eventos e mantera read models proprios.

Principais responsabilidades:

- dashboard financeiro
- dashboard operacional
- uso de equipamento
- performance de instrutores
- alertas de manutencao

Limites operacionais na fase sincrona:

- endpoints orientados a dashboard, nao a consultas arbitrarias
- profundidade limitada de agregacao em tempo real
- cache quando fizer sentido
- migracao para read models assim que os paineis estabilizarem

### 7. API Gateway / BFF

Ponto unico de entrada para frontend e clientes externos.

Posicionamento inicial:

- gateway fino por padrao
- roteamento, autenticacao e politicas transversais como responsabilidade principal
- poucos endpoints compostos apenas quando a UI realmente precisar
- evitar um BFF gordo cedo demais

Principais responsabilidades:

- roteamento por servico
- terminacao de autenticacao para clientes
- composicao leve para casos especificos de frontend
- politicas transversais futuras como rate limit, observabilidade e seguranca

## Estrategia de multi-tenancy

- `SchoolId` continua sendo o tenant key principal
- JWT carrega `school_id`, `role`, `email` e `sub`
- quase todos os recursos sao tenant-scoped por padrao
- `SystemAdmin` pode operar sem `school_id`
- cada servico grava `SchoolId` em entidades relevantes e indexa consultas por tenant
- relacoes entre servicos nao usam foreign key fisica entre bancos; usam IDs externos e validacao aplicacional
- isolamento entre escolas e regra estrutural do SaaS, nao apenas convencao de implementacao

## Estrategia de autenticacao e autorizacao

- autenticacao centralizada no `Identity Service`
- JWT assinado com chave simetrica na fundacao inicial
- validacao do token em todos os servicos e no gateway
- autorizacao baseada em roles: `SystemAdmin`, `Owner`, `Admin`, `Instructor`, `Student`
- roles sao o mecanismo inicial de coarse-grained authorization
- regras contextuais e permissoes finas podem ser implementadas por policy/claims e autorizacao contextual por recurso

## Comunicacao entre servicos

### Fase inicial

- HTTP sincrono entre gateway e servicos
- Reporting agregando consultas sincronas com parcimonia
- Finance recebendo dados inicialmente por API

### Evolucao recomendada

- eventos de `EnrollmentCreated`, `LessonStatusChanged`, `EquipmentCheckedIn`, `MaintenanceRecorded`
- projections assincronas para Finance e Reporting
- outbox/inbox por servico para confiabilidade transacional
- consumidores idempotentes e eventos rastreaveis

## Decisoes arquiteturais complementares

### Persistencia por servico

- cada servico possui banco ou schema proprio
- nao ha compartilhamento direto de tabelas entre servicos
- integracao apenas por API e eventos

### Consistencia

- consistencia forte apenas dentro do banco/servico local
- consistencia eventual entre contextos
- compensacao quando houver falha em fluxos distribuidos
- eventos e comandos distribuidos devem ser idempotentes

### Observabilidade

- correlation id por requisicao e propagacao entre servicos
- logs estruturados
- health checks e readiness/liveness
- metricas basicas por servico

### Resiliencia

- timeout explicito entre servicos
- retry apenas em operacoes idempotentes
- circuit breaking quando necessario
- fallback controlado em consultas agregadas

### Contratos

- versionamento de APIs quando necessario
- eventos com schema evolutivo
- evitar acoplamento ao modelo interno de persistencia

## Estrategia de implantacao

Mesmo com a arquitetura alvo em microsservicos, a implantacao deve ser evolutiva.

Diretriz pratica:

- nao e obrigatorio nascer com todos os deployables totalmente independentes no primeiro corte
- modulos ainda em amadurecimento podem permanecer juntos fisicamente no inicio, desde que a fronteira logica esteja clara
- as separacoes mais valiosas desde cedo sao:
  - `Identity`
  - `Operations`
  - `Equipment`
  - `Reporting`
- `Finance` pode amadurecer primeiro como modulo bem separado e depois como fronteira operacional mais autonoma

## Principios de implementacao

- .NET 8 + ASP.NET Core Web API
- EF Core + PostgreSQL
- DTOs e services apenas onde adicionam clareza
- evitar camadas artificiais e repositorios genericos
- contratos simples e evolutiveis
