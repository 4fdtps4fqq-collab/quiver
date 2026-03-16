# Desenvolvimento Local

## Subir tudo

```powershell
.\scripts\start-dev.ps1
```

Se voce ja compilou a solution antes e quer apenas levantar os servicos:

```powershell
.\scripts\start-dev.ps1 -SkipBuild
```

## Parar tudo

```powershell
.\scripts\stop-dev.ps1
```

## Enderecos

- Frontend: `http://localhost:5174`
- Gateway: `http://localhost:7000`
- pgAdmin: `http://localhost:5050`
- Swagger Identity: `http://localhost:7001/swagger`
- Swagger Schools: `http://localhost:7002/swagger`
- Swagger Academics: `http://localhost:7003/swagger`
- Swagger Equipment: `http://localhost:7004/swagger`
- Swagger Finance: `http://localhost:7005/swagger`
- Swagger Reporting: `http://localhost:7006/swagger`

## Fluxo recomendado de teste

1. Suba todo o ambiente com `.\scripts\start-dev.ps1 -SkipBuild`.
2. Abra o frontend em `http://localhost:5174`.
3. Entre como `SystemAdmin` para cadastrar novas escolas:
   - e-mail: `admin@quiver.local`
   - senha: `Admin123!`
4. Use o login da escola ou do aluno conforme o fluxo que voce quer validar.

## Senha temporaria do proprietario

No ambiente local, a senha temporaria do proprietario nao e enviada por SMTP. Ela fica gravada no outbox do gateway:

- `src/backend/gateway/KiteFlow.Gateway/temp/email-outbox`

Cada arquivo gerado traz:
- URL do login
- e-mail do proprietario
- senha temporaria

No primeiro acesso, a troca da senha e obrigatoria.

## Preparar acesso do portal do aluno

Quando voce quiser liberar rapidamente um aluno real da base para testar o portal, rode:

```powershell
dotnet run --project .\tools\StudentPortalAccessTool\StudentPortalAccessTool.csproj -- --host localhost --port 5432 --username postgres --password postgres --student-name Pedro --set-password 1234
```

Esse utilitario:
- encontra o aluno pelo nome
- garante e-mail de login
- cria ou atualiza a conta no `Identity`
- vincula o aluno ao `IdentityUserId`

Depois disso, o aluno pode entrar em `http://localhost:5174/login`.
