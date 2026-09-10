# GET-SET-GO Smart Research

A complete .NET 8 MVC Razor + jQuery MVP for EOD research, personalised risk rules, one-click order preparation, simulated execution and trade tracking. SQL access uses plain ADO.NET (`Microsoft.Data.SqlClient`) with parameterised commands.

## Included

- Cookie login with PBKDF2 password hashing and Client/Admin roles
- Automatic SQL Server database/schema creation and demo seeding
- One active risk profile per client (capital, style, risk/trade, total risk and 1:1–5:1 reward/risk)
- EOD signal filtering by style, validity and minimum reward/risk
- Risk-based quantity: `(capital × risk %) ÷ |entry − stop|`
- Total active-risk limit, duplicate-order prevention and expiry protection
- GET → SET → GO simulation and order tracker
- Research-admin signal publishing with BUY/SELL direction validation
- Interactive 60-candle demo OHLC chart with volume and Entry/Stop/Target lines
- Versioned REST APIs with cookie authentication, role authorization and anti-forgery protection
- SET, GO and Cancel buttons connected to the APIs through jQuery AJAX
- Responsive Razor/jQuery interface and Docker option

## Visual Studio 2022 setup

1. Install the **ASP.NET and web development** workload, .NET 8 SDK and SQL Server 2022/Developer (or use an existing SQL Server).
2. Open `GetSetGo.sln`.
3. In `GetSetGo.Web/appsettings.json`, change the `GetSetGoDb` connection string password/server if needed.
4. Set `GetSetGo.Web` as startup project and press `Ctrl+F5`.
5. The app creates `GetSetGoDb`, its tables and sample records on first run. The SQL login must have database-creation permission.

## Docker setup

From the `GetSetGo` directory:

```bash
docker compose up --build
```

Open `http://localhost:8080` after SQL Server becomes healthy.

## Demo accounts

| Role | Email | Password |
|---|---|---|
| Client | `client@getsetgo.local` | `Client@123` |
| Research admin | `admin@getsetgo.local` | `Admin@123` |

Change these credentials before any shared deployment. This MVP executes orders only inside the application; it does not connect to a broker or place real trades.

## Workflow test

1. Sign in as Client and save Risk Setup (for example ₹100,000, Intraday, 1% per trade, 3% total, 2:1 minimum).
2. Open GET and select a matching signal.
3. Press SET. Quantity and monetary risk are calculated and snapshotted into the order.
4. Review SET / GO / Tracker and press GO within the validity window.
5. Sign out, sign in as Admin and publish another EOD signal.

## REST API routes

All routes are under `/api/v1`. Authentication uses the same secure HTTP-only cookie as the MVC application. Fetch `/api/v1/security/antiforgery` before an API write and send its `requestToken` in the `X-CSRF-TOKEN` header; retain both the auth and anti-forgery cookies.

| Method | Route | Role | Purpose |
|---|---|---|---|
| POST | `/auth/login` | Public | Sign in and create auth cookie |
| POST | `/auth/logout` | Signed in | Sign out |
| GET | `/security/antiforgery` | Public | Get request token and cookie |
| GET | `/risk-profile` | Client | Read active risk profile |
| PUT | `/risk-profile` | Client | Create/update risk profile |
| GET | `/signals` | Client | Get matching EOD signals |
| GET | `/market-data/{symbol}?timeframe=1D&take=60` | Client | Get deterministic demo OHLC candles |
| POST | `/signals/{id}/set` | Client | Prepare an auto-sized SET order |
| GET | `/orders` | Client | Read tracker/orders |
| POST | `/orders/{id}/go` | Client | Execute a valid SET order in simulation |
| POST | `/orders/{id}/cancel` | Client | Cancel a SET order |
| GET | `/admin/signals` | Admin | Read all research signals |
| POST | `/admin/signals` | Admin | Publish a signal |

Use `GetSetGo.Web/GetSetGo.http` in Visual Studio for sample requests. Replace the anti-forgery placeholder for protected write calls with the value returned by the token endpoint.

The market chart is deliberately labelled **Demo**. It uses deterministic sample OHLC and volume rows seeded into `MarketCandles`; it is not a live exchange price feed. Open GET signals and press **View demo chart** to see candlesticks, volume, and the signal's Entry, Stop-loss and Target lines.

## Production follow-ups

- Integrate approved broker order APIs and live execution callbacks.
- Replace seeded accounts with enterprise identity/MFA.
- Replace the static news text with an approved AI/news provider and audit trail.
- Add market calendars, exchange time zone handling, maker-checker approval and compliance disclosures.
- Add unit/integration tests in CI and use Key Vault/user-secrets for all credentials.
"# proptotype" 
