# Bulk demo fixtures

The development-only importer adds 120 synthetic signals (`DEMO001`–`DEMO120`), 120 orders for `client@getsetgo.local`, and 60 daily candles per symbol (7,200 candles). Each order state has 24 fixtures. Existing rows are retained; repeat imports do not duplicate fixtures or reset their status/validity.

These are direct database fixtures for list demonstrations, not orders created under the client's risk rules. The pending/executed fixtures count toward active risk just like other simulated orders. No existing risk profile is changed.

Run explicitly from the project root:

```powershell
dotnet run --project GetSetGo.Web -- --DemoData:SeedBulk=true --Development:OpenBrowserTabs=false
```

The switch is ignored outside Development and is not enabled in appsettings. Normal startup does not import bulk fixtures. Fixture validity is relative to the first import and is not refreshed on rerun.

## Verification

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-bulk-demo.ps1 -BaseUrl http://localhost:55190
node scripts/verify-records.cjs
```

Use the actual URL of your running development instance. The workflow verifier uses the dedicated local demo account `operations-demo@getsetgo.local` / `DemoOps@123`. It leaves one executed test order and cancels its remaining SET reservations. It validates fixture counts, list membership, candles, risk sizing, execution, duplicate prevention, cancellation, risk-limit rejection, missing CSRF and order ownership. Repeated checks can add cancelled test history for that account.

The Node check exercises search, combined filters, numeric/date sorting, null dates, reset and pagination with 127 in-memory rows. This is a DOM-adapter functional check, not a real browser visual test.
