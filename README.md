# KiteFlow Platform v2

Nova fundacao da plataforma KiteFlow, desenhada para evoluir o monolito atual para uma arquitetura baseada em microsservicos, mantendo as regras centrais do dominio operacional de escolas de kite e watersports.

## Objetivos desta base

- preservar multi-tenancy por `SchoolId`
- centralizar autenticacao e autorizacao com JWT
- separar responsabilidades por contexto de negocio
- permitir crescimento incremental sem fragmentacao excessiva
- introduzir frontend moderno em React + TypeScript + Vite

## Estrutura

- `docs/architecture`: visao arquitetural, modelagem inicial e roadmap
- `infra`: docker-compose e configuracoes locais
- `src/backend`: solution .NET 8 com gateway, servicos e building blocks
- `src/frontend/app`: frontend React responsivo e preparado para backoffice premium

## Estado atual

Esta base contem o scaffold inicial da nova solucao. O sistema atual segue preservado nos diretorios irmaos `../KiteFlow.Api` e `../KiteFlow.Web`.
