# OrdersMini — .NET (9/8) + EF Core + JWT + Testes

Mini‑plataforma de **Pedidos** com **API REST**, **EF Core**, **JWT** e **Testes**.  
Foco: **arquitetura limpa**, **boas práticas**, **performance** e **CI**. Compatível com .NET **9** (recomendado) e **8**.

---

## ✨ Visão Geral

- **Domínios:** Clientes, Produtos e Pedidos (com itens).
- **Autenticação:** JWT Bearer (login “fake” com usuários seed: `admin` e `user`).
- **Autorização:** Rotas de escrita exigem Bearer; `Admin` pode excluir, `User` só cria/lista.
- **Qualidade:** DTOs, validação, ProblemDetails, Serilog, Swagger.
- **Testes:** xUnit + FluentAssertions (unit) e WebApplicationFactory (integração).
- **Performance:** projeções LINQ (evitar N+1), índice `(CustomerId, CreatedAt)`, `AsNoTracking`.

---

## 🧱 Arquitetura & Stack

**Camadas**  
```
OrdersMini.Domain         // Entidades e enums
OrdersMini.Application    // DTOs, Profiles, Validators
OrdersMini.Infrastructure // DbContext, Migrations, Seed
OrdersMini.Web            // API (Minimal APIs), Auth, DI, Swagger, Serilog
```

**Tecnologias**  
- **.NET 9** (ou .NET 8), **Minimal APIs**
- **EF Core 8** + **SQLite** (pode trocar por SQL Server/Postgres)
- **FluentValidation**, **AutoMapper**, **Serilog**, **Swagger**
- **JWT Bearer** (Microsoft.AspNetCore.Authentication.JwtBearer)
- **xUnit**, **FluentAssertions**, **Moq**, **Microsoft.AspNetCore.Mvc.Testing**

---

## 🔧 Pré‑requisitos

- SDK **.NET 9** (ou 8)
- **SQLite** (opcional: apenas usa arquivo `.db` local)
- CLI EF: `dotnet tool update -g dotnet-ef`

---

## ⚙️ Configuração

`OrdersMini.Web/appsettings.json` (exemplo):
```json
{
  "ConnectionStrings": { "Default": "Data Source=orders-mini.db" },
  "Jwt": {
    "Key": "super-secret-key-change-me",
    "Issuer": "orders-mini",
    "Audience": "orders-mini"
  },
  "Serilog": { "MinimumLevel": "Information" }
}
```

> Dica: use variáveis de ambiente para **Jwt:Key** em produção.

---

## 🚀 Como executar (dev)

1) **Restaurar, migrar e rodar**
```bash
dotnet restore
dotnet ef migrations add InitialCreate -p OrdersMini.Infrastructure -s OrdersMini.Web
dotnet ef database update            -p OrdersMini.Infrastructure -s OrdersMini.Web
dotnet run --project OrdersMini.Web
# Swagger: http://localhost:5193/swagger  (porta pode variar)
```

2) **Seed automático**  
Ao subir a API a primeira vez:
- **Clientes:** 5 (Alice, Bob, …)
- **Produtos:** 10 (`Product 1..10`, preço 11..20)
- **Pedidos:** 3 exemplos
- **Usuários de Login (fake):**
  - `admin / Password123!` (Role = Admin)
  - `user  / Password123!` (Role = User)

---

## 🔐 Autenticação & Autorização

- **Login:** `POST /api/auth/login` → `{ access_token }`
- **Use o token** no Swagger (`Authorize`) ou no header:
  - `Authorization: Bearer <token>`
- **Regras:**
  - `WriteAccess`: exige usuário autenticado (POST/PUT).
  - `CanDelete`: exige **Role = Admin** (DELETE).

**.NET 9 (recomendado):**
```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("WriteAccess", p => p.RequireAuthenticatedUser())
    .AddPolicy("CanDelete",  p => p.RequireRole("Admin"));
```

**Tratamento de erros (ProblemDetails + Handler genérico):**
```csharp
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>(); // .NET 9
app.UseExceptionHandler();
```

---

## 📚 Endpoints

### Clientes
- `POST /api/customers` — criar (Body: `{ name, email }`) **(auth)**
- `GET  /api/customers?search=&page=&pageSize=` — paginação + filtro por nome/email
- `GET  /api/customers/{id}` — detalhe
- `PUT  /api/customers/{id}` — atualizar **(auth)**
- `DELETE /api/customers/{id}` — **soft delete** **(role Admin)**

### Produtos
- `POST /api/products` — criar `{ description, price }` **(auth)**
- `GET  /api/products?minPrice=&maxPrice=&search=&sort=`
  - `sort`: `price_asc | price_desc | desc_asc | desc_desc`
- `GET  /api/products/{id}`
- `PUT  /api/products/{id}` — atualizar **(auth)**
- `DELETE /api/products/{id}` — excluir **(role Admin)**

### Pedidos
- `POST /api/orders` **(auth)**
```json
{
  "customerId": 1,
  "items": [
    { "productId": 10, "quantity": 2 },
    { "productId": 11, "quantity": 1 }
  ]
}
```
  - Valida cliente/produtos
  - Calcula `Total = Σ (quantity * unitPrice)`
  - Persiste em **transação**

- `GET /api/orders/{id}` — detalhe (itens, valores, status)
- `GET /api/orders?customerId=&from=&to=` — filtro por cliente/período

---

## 🧪 Testes

### Unit (exemplos)
- Cálculo do **Total** do pedido
- Validações (FluentValidation) de DTOs

### Integração (WebApplicationFactory)
- `POST /api/orders` cria pedido com **total correto**
- `GET  /api/products?minPrice=...` aplica filtros
- Usa **SQLite in‑memory** na fixture

**Rodar:**
```bash
dotnet test
```

> Dica: ative logs EF nas integrações para inspecionar SQL e planos de execução.

---

## ⚡ Trilha de Performance

**Problema (antes):** N+1 e materialização desnecessária.
```csharp
var orders = await db.Orders
  .Include(o => o.Items).ThenInclude(i => i.Product)
  .ToListAsync(); // carrega tudo

var dto = orders.Select(o => new {
  o.Id, o.CustomerId, o.CreatedAt,
  Total = o.Items.Sum(i => i.Quantity * i.Product.Price) // soma em memória
}).ToList();
```

**Solução (depois):** projeção direta para DTO + índice + `AsNoTracking`.
```csharp
var dto = await db.Orders.AsNoTracking()
  .OrderByDescending(o => o.CreatedAt)
  .Select(o => new {
    o.Id, o.CustomerId, o.CreatedAt,
    Total = o.Items.Sum(i => i.Quantity * i.UnitPrice) // soma no SQL
  })
  .ToListAsync();
```

**Índice:** definido em `OnModelCreating`: `(CustomerId, CreatedAt)`  
**README — Relato sugerido:**  
> “Com 1k pedidos: de ~120ms (antes) para ~30ms (depois) no meu ambiente local.”  
*(Ajuste com seus números reais — capture com logs EF, `dotnet-trace` ou MiniProfiler.)*

---

## 🔎 Exemplos rápidos (cURL)

```bash
# Login (admin)
TOKEN=$(curl -s -X POST http://localhost:5193/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"Password123!"}' | jq -r .access_token)

# Criar produto
curl -s -X POST http://localhost:5193/api/products \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"description":"Mouse", "price": 99.9}' | jq

# Criar cliente
curl -s -X POST http://localhost:5193/api/customers \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"Cliente X","email":"x@example.com"}' | jq

# Criar pedido
curl -s -X POST http://localhost:5193/api/orders \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"customerId":1,"items":[{"productId":1,"quantity":2}]}' | jq

# Listar produtos (filtro + ordenação)
curl -s "http://localhost:5193/api/products?minPrice=50&sort=price_desc" | jq
```

---

## 🧰 Observabilidade & Logs

- **Serilog** com sink de console.  
- Adapte nível por ambiente via `Serilog.MinimumLevel` e **enrichers** (RequestId, UserName etc.).  
- Ative logs EF: `Microsoft.EntityFrameworkCore.Database.Command: Information` para ver SQL.

---

## 🧪 Qualidade (checklist)

- [x] Camadas: Domain, Application, Infrastructure, Web(API)
- [x] SOLID (SRP/DIP) aplicado
- [x] DTOs + AutoMapper (ou mapeamento manual limpo)
- [x] Validações (FluentValidation)
- [x] ProblemDetails + ExceptionHandler
- [x] Logging estruturado (Serilog)
- [x] Swagger + SecurityScheme Bearer
- [x] Filtros/Ordenação/Paginação eficientes
- [x] Transação no `POST /api/orders`
- [x] `AsNoTracking()` em GETs
- [x] Evitar `ToList()` antes de `Skip/Take`
- [x] `Include/ThenInclude` só no que precisa (detalhe do pedido)
- [x] Testes unitários e de integração
- [x] Trilha de performance (projeção + índice)

---

## 🛠️ CI (GitHub Actions — opcional)

`.github/workflows/ci.yml`:
```yaml
name: ci
on: [push, pull_request]
jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '9.0.x' }
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test  --no-build   -c Release
```

---

## ❗ Troubleshooting (.NET 9)

**Erro:**  
`The type arguments for method 'AddExceptionHandler<T>(...)' cannot be inferred...`

**Causa:** no .NET 9, `AddExceptionHandler` é **genérico**.  
**Correção:** registre um tipo concreto:
```csharp
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
app.UseExceptionHandler();
```

---

## 📄 Licença

Uso livre para fins educacionais e técnicos — ajuste conforme sua necessidade.
