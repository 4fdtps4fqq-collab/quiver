# Smoke Manual - Equipamentos e Manutenção

## Objetivo

Validar manualmente:

- disponibilidade de equipamento por agenda
- reserva prévia antes do checkout
- kits de equipamento
- manutenção de item próprio gerando despesa
- manutenção de item de terceiro gerando receita
- isolamento multi-escola
- histórico de vida útil do equipamento

## Pré-requisitos

- ambiente local no ar
- acesso com `SystemAdmin`
- pelo menos 2 escolas criadas
- acesso de owner disponível para as 2 escolas

## Cenário recomendado

- `Escola A`
- `Escola B`

Na `Escola A`, criar:

- 1 depósito
- 2 alunos
- 2 instrutores
- 3 equipamentos:
  - 1 `Kite` da escola
  - 1 `Prancha` de terceiro
  - 1 `Colete` da escola
- 1 kit com `Kite + Colete`
- 2 aulas em horário sobreposto

Na `Escola B`, criar:

- 1 depósito
- 1 equipamento qualquer

## Passo a passo

### 1. Cadastro base

Na `Escola A`:

- criar o depósito
- criar dois alunos
- criar dois instrutores com disponibilidade ativa
- criar os três equipamentos
- confirmar:
  - categoria correta
  - propriedade `Escola` ou `Terceiro`
  - depósito correto

Resultado esperado:

- os equipamentos aparecem na lista
- os itens mostram disponibilidade inicial livre

### 2. Kit de equipamento

Na tela de equipamentos:

- criar um kit com o `Kite` e o `Colete`

Resultado esperado:

- o kit aparece na lista
- os itens mostram vínculo com kit quando aplicável

### 3. Agenda e reserva prévia

Na `Escola A`:

- criar `Aula 1`
- criar `Aula 2` em horário parcialmente sobreposto
- na `Aula 1`, reservar:
  - a `Prancha`
  - o kit `Kite + Colete`

Resultado esperado:

- a reserva é salva
- o estado da aula mostra os itens reservados
- os equipamentos reservados aparecem como indisponíveis no mesmo intervalo

### 4. Conflito de reserva

Na `Aula 2`:

- tentar reservar o mesmo `Kite` já usado pela `Aula 1`

Resultado esperado:

- a operação deve ser bloqueada
- deve aparecer conflito de disponibilidade

### 5. Checkout

Na `Aula 1`:

- executar checkout dos itens reservados

Resultado esperado:

- a reserva da mesma aula é liberada automaticamente
- o checkout fica registrado
- o histórico do equipamento passa a mostrar uso operacional

### 6. Manutenção de item da escola

Na manutenção:

- abrir serviço para o `Kite` da escola
- informar custo
- concluir a manutenção

Resultado esperado:

- o resumo de manutenção soma esse valor em despesa
- o financeiro da `Escola A` reflete a despesa automática
- o histórico do equipamento mostra o evento de manutenção

### 7. Manutenção de item de terceiro

Na manutenção:

- abrir serviço para a `Prancha` de terceiro
- informar valor de serviço cobrado
- concluir a manutenção

Resultado esperado:

- o resumo de manutenção soma esse valor em receita
- o financeiro da `Escola A` reflete a receita automática
- o histórico do equipamento mostra o evento de manutenção

### 8. Alertas e severidade

- cadastrar ou editar uma regra de manutenção com limites curtos
- ajustar o uso/condição do item para disparar alerta

Resultado esperado:

- o alerta aparece com severidade coerente
- o resumo destaca o item com recomendação operacional

### 9. Histórico de vida útil

Na tela de equipamentos:

- abrir o histórico do `Kite`
- abrir o histórico da `Prancha`

Resultado esperado:

- timeline com uso, reserva e manutenção
- totais financeiros coerentes por item

### 10. Isolamento multi-escola

Entrar na `Escola B`:

- abrir equipamentos
- abrir manutenção
- abrir financeiro

Resultado esperado:

- nenhum item da `Escola A` aparece
- nenhuma manutenção da `Escola A` aparece
- nenhum reflexo financeiro da `Escola A` aparece

## Critérios de aprovação

- reserva prévia funciona antes do checkout
- conflito de reserva impede uso simultâneo do mesmo item
- manutenção de item da escola gera despesa
- manutenção de item de terceiro gera receita
- histórico do equipamento mostra toda a vida útil
- dados e financeiros permanecem isolados por escola

## Evidências sugeridas

- print da reserva da `Aula 1`
- print do conflito da `Aula 2`
- print do financeiro após manutenção própria
- print do financeiro após manutenção de terceiro
- print do histórico do equipamento
- print da `Escola B` sem acesso aos dados da `Escola A`
