# GetSetGo API Endpoints and Request Bodies

The project contains **13 API endpoints**. This reference was verified against the controllers and request models. Requests have not been tested against a running server.

## Base URL and Postman setup

The local HTTPS URL configured in `GetSetGo.Web/Properties/launchSettings.json` is:

```text
https://localhost:55073
```

Set the Postman environment variable `baseUrl` to this URL. Update it if your application runs on a different host or port.

For JSON requests, select **Body > raw > JSON** and send:

```text
Content-Type: application/json
```

Authentication is **cookie-based**. Preserve the authentication cookie from the login response in Postman's cookie jar. Use an account with the appropriate `Client` or `Admin` role. No bearer token is required.

All POST and PUT endpoints except login require a CSRF token:

1. Log in.
2. Call `GET {{baseUrl}}/api/v1/security/antiforgery`.
3. Save the returned `requestToken` as the Postman variable `csrfToken`.
4. Send the following header along with the cookies from the same session:

```text
X-CSRF-TOKEN: {{csrfToken}}
```

Fetch a new antiforgery token after logging in or switching accounts. GET requests do not require this header.

## 1. Login

**POST** `{{baseUrl}}/api/v1/auth/login`

- Access: Public
- CSRF: Not required
- Body:

```json
{
  "email": "your-email@example.com",
  "password": "your-password"
}
```

Use the actual credentials of an existing account. Both fields are required, and `email` must be a valid email address. A successful response contains `id`, `fullName`, `email`, and `role`, and sets an authentication cookie. Incorrect credentials return `401`.

## 2. Logout

**POST** `{{baseUrl}}/api/v1/auth/logout`

- Access: Logged-in user
- CSRF: Required
- Body: **none**
- Success: `200`, message `Signed out.`

## 3. Get an antiforgery token

**GET** `{{baseUrl}}/api/v1/security/antiforgery`

- Access: Public; call after login when preparing authenticated requests.
- Body: **none**
- Response format:

```json
{
  "requestToken": "<generated-token>",
  "headerName": "X-CSRF-TOKEN"
}
```

Preserve the antiforgery cookie set with this response as well.

## 4. Get the risk profile

**GET** `{{baseUrl}}/api/v1/risk-profile`

- Access: Client
- Body: **none**
- Success: `200`, the current user's risk profile.
- Returns `404` if the profile has not been configured.

## 5. Create or update the risk profile

**PUT** `{{baseUrl}}/api/v1/risk-profile`

- Access: Client
- CSRF: Required
- Body:

```json
{
  "capital": 100000,
  "tradingStyle": "Swing",
  "riskPerTradePercent": 1,
  "maxTotalRiskPercent": 3,
  "minimumRewardRiskRatio": 2
}
```

| Field | Allowed value / rule |
|---|---|
| `capital` | 1000 to 100000000 |
| `tradingStyle` | `Intraday`, `Swing`, `Positional`, `ShortTermDelivery` |
| `riskPerTradePercent` | 0.1 to 10 |
| `maxTotalRiskPercent` | 0.1 to 25; must be at least `riskPerTradePercent` |
| `minimumRewardRiskRatio` | 1 to 5 |

The server obtains `userId` from the logged-in user; do not include it in the body. Success: `200`, with `message` and `profile`.

## 6. Get matching signals

**GET** `{{baseUrl}}/api/v1/signals`

- Access: Client
- Body: **none**
- Success: `200`, a list of matching signals.
- Configure the risk profile first; a missing profile returns `400`.

## 7. Create a SET order from a signal

**POST** `{{baseUrl}}/api/v1/signals/{{signalId}}/set`

- Access: Client
- CSRF: Required
- Path parameter: `signalId` is an existing integer signal ID from the matching signals response.
- Body: **none**

Example path:

```text
{{baseUrl}}/api/v1/signals/1/set
```

Success: `200`, with `message` and `orderId`. Use this `orderId` for subsequent order actions. Service validation failures return `400`.

## 8. Get orders

**GET** `{{baseUrl}}/api/v1/orders`

- Access: Client
- Body: **none**
- Success: `200`, a list of the current user's orders.

## 9. Execute an order (GO)

**POST** `{{baseUrl}}/api/v1/orders/{{orderId}}/go`

- Access: Client
- CSRF: Required
- Path parameter: `orderId` is an existing integer order ID.
- Body: **none**

Example path:

```text
{{baseUrl}}/api/v1/orders/1/go
```

Success: `200`, with `message`. Service validation failures return `400`.

## 10. Cancel an order

**POST** `{{baseUrl}}/api/v1/orders/{{orderId}}/cancel`

- Access: Client
- CSRF: Required
- Path parameter: `orderId` is an existing integer order ID.
- Body: **none**

Example path:

```text
{{baseUrl}}/api/v1/orders/1/cancel
```

Only orders with status `Set` can be cancelled. Other statuses return `400`. If the order is not found for the current user, the API returns `404`. Success: `200`, message `Order cancelled.`

## 11. Get market data / OHLC candles

**GET** `{{baseUrl}}/api/v1/market-data/{{symbol}}?timeframe=1D&take=60`

- Access: Client
- Body: **none**

| Parameter | Location | Value |
|---|---|---|
| `symbol` | Path | Available demo symbol; maximum 30 characters |
| `timeframe` | Query | Optional; default `1D`; only `1D` is supported |
| `take` | Query | Optional integer; default `60` |

Example request (candles are returned only if demo data exists for this symbol):

```text
{{baseUrl}}/api/v1/market-data/RELIANCE?timeframe=1D&take=60
```

Success: `200`, with `symbol`, `timeframe`, `isDemo`, and `candles`. An unsupported timeframe returns `400`. Missing candle data returns `404`.

## 12. Admin: get all research signals

**GET** `{{baseUrl}}/api/v1/admin/signals`

- Access: Admin
- Body: **none**
- Success: `200`, a list of all research signals.

## 13. Admin: publish a research signal

**POST** `{{baseUrl}}/api/v1/admin/signals`

- Access: Admin
- CSRF: Required
- Body: Buy example with sample values:

```json
{
  "symbol": "RELIANCE",
  "side": "Buy",
  "tradingStyle": "Swing",
  "entryPrice": 2500,
  "stopLoss": 2450,
  "targetPrice": 2600,
  "aiNewsSummary": "Demo research signal for API testing.",
  "validFromUtc": "2026-09-10T00:00:00Z",
  "validUntilUtc": "2026-09-17T23:59:59Z"
}
```

Alternative body: Sell example:

```json
{
  "symbol": "RELIANCE",
  "side": "Sell",
  "tradingStyle": "Intraday",
  "entryPrice": 2500,
  "stopLoss": 2550,
  "targetPrice": 2400,
  "aiNewsSummary": "Demo sell signal for API testing.",
  "validFromUtc": "2026-09-10T00:00:00Z",
  "validUntilUtc": "2026-09-10T23:59:59Z"
}
```

Update the dates for your testing period. The `Z` suffix indicates UTC.

| Field / condition | Rule |
|---|---|
| `symbol` | Required, maximum 30 characters |
| `side` | `Buy` or `Sell` |
| `tradingStyle` | `Intraday`, `Swing`, `Positional`, `ShortTermDelivery` |
| `entryPrice`, `stopLoss`, `targetPrice` | Each must be between 0.01 and 10000000 |
| `aiNewsSummary` | Required, maximum 1000 characters |
| Dates | `validUntilUtc` must be after `validFromUtc` |
| Buy prices | `stopLoss < entryPrice < targetPrice` |
| Sell prices | `targetPrice < entryPrice < stopLoss` |
| Reward/risk | Minimum 1:1 |

Success: `201 Created`, with `message` and `signal`. Validation failures return `400`.

## Quick testing flow

1. Log in with a Client account.
2. Fetch an antiforgery token.
3. Create or update the risk profile using PUT.
4. Fetch matching signals and select an actual `signalId`.
5. SET the signal and save the returned `orderId`.
6. Fetch the orders list.
7. Execute (GO) or cancel a SET order. An executed order cannot be cancelled through the cancel endpoint.
8. To test Admin APIs, log in with an Admin account and fetch a new antiforgery token.

Protected APIs return `401` when unauthenticated and `403` when the required role is missing. For endpoints marked Body **none**, select **Body > none** in Postman; an empty JSON object is unnecessary.

## Source files

- `GetSetGo.Web/Controllers/Api/*.cs` - routes, access rules, and actions
- `GetSetGo.Web/Models/ApiDtos.cs` - JSON request fields and validations
- `GetSetGo.Web/Models/DomainModels.cs` - enum values
- `GetSetGo.Web/Program.cs` - cookie authentication and CSRF header configuration
- `GetSetGo.Web/Properties/launchSettings.json` - local URL
