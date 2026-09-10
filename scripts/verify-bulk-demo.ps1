param([string]$BaseUrl = 'http://localhost:55190')
$ErrorActionPreference = 'Stop'
function Login-Demo($Email, $Password) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $null = Invoke-RestMethod "$BaseUrl/api/v1/auth/login" -Method Post -ContentType 'application/json' -Body (@{email=$Email;password=$Password}|ConvertTo-Json) -WebSession $session
    return $session
}
function Assert-Check($Condition, $Message) { if (!$Condition) { throw $Message }; Write-Output "PASS: $Message" }
function Expect-Rejection($Session, $Url, $Headers, $Code) {
    try { $null = Invoke-RestMethod "$BaseUrl$Url" -Method Post -WebSession $Session -Headers $Headers; throw 'Request unexpectedly succeeded' }
    catch { if (!$_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne $Code) { throw }; Write-Output "PASS: rejected $Url ($Code)" }
}
$client = Login-Demo 'client@getsetgo.local' 'Client@123'
$orders = Invoke-RestMethod "$BaseUrl/api/v1/orders" -WebSession $client
$fixtures = @($orders | Where-Object { $_.symbol -match '^DEMO\d{3}$' })
Assert-Check ($fixtures.Count -eq 120) '120 dummy orders persisted for the existing demo client'
foreach ($state in @('Set','Executed','Expired','Cancelled','Closed')) { Assert-Check (@($fixtures|Where-Object status -eq $state).Count -eq 24) "24 $state fixtures" }
foreach ($section in @('Set','Go','Tracker')) {
    $html = (Invoke-WebRequest "$BaseUrl/Orders/$section" -UseBasicParsing -WebSession $client).Content
    $expected = @($fixtures | Where-Object { $section -eq 'Tracker' -or ($section -eq 'Set' -and $_.status -eq 'Set') -or ($section -eq 'Go' -and $_.status -eq 'Executed') })
    foreach ($order in $fixtures) {
        $present = $html.Contains('order-number">#' + $order.id + '</span>')
        if ($present -ne ($expected.id -contains $order.id)) { throw "Incorrect $section membership for $($order.symbol)" }
    }
}
$admin = Login-Demo 'admin@getsetgo.local' 'Admin@123'
$signals = Invoke-RestMethod "$BaseUrl/api/v1/admin/signals" -WebSession $admin
Assert-Check (@($signals|Where-Object symbol -match '^DEMO\d{3}$').Count -eq 120) '120 dummy signals persisted'
foreach ($symbol in @('DEMO001','DEMO060','DEMO120')) {
    $chart = Invoke-RestMethod "$BaseUrl/api/v1/market-data/${symbol}?take=60" -WebSession $client
    Assert-Check ($chart.candles.Count -eq 60) "$symbol has 60 candles"
}
$ops = Login-Demo 'operations-demo@getsetgo.local' 'DemoOps@123'
$tokens = Invoke-RestMethod "$BaseUrl/api/v1/security/antiforgery" -WebSession $ops
$headers = @{'X-CSRF-TOKEN'=$tokens.requestToken}
$existing = Invoke-RestMethod "$BaseUrl/api/v1/orders" -WebSession $ops
Assert-Check (@($existing|Where-Object status -eq 'Set').Count -eq 0 -and @($existing|Where-Object status -eq 'Executed').Count -le 1) 'Operations account is ready for initial run or verification rerun'
$matching = Invoke-RestMethod "$BaseUrl/api/v1/signals" -WebSession $ops
$choices = @($matching | Where-Object symbol -match '^DEMO\d{3}$' | Select-Object -First 5)
Assert-Check ($choices.Count -eq 5) 'Risk-matched dummy signals available'
$prior = $existing | Where-Object status -eq Executed | Select-Object -First 1
$first = if ($prior) { @{orderId=$prior.id} } else { Invoke-RestMethod "$BaseUrl/api/v1/signals/$($choices[0].id)/set" -Method Post -Headers $headers -WebSession $ops }
Expect-Rejection $ops "/api/v1/signals/$($choices[0].id)/set" $headers 400
$noCsrf = Login-Demo 'operations-demo@getsetgo.local' 'DemoOps@123'
Expect-Rejection $noCsrf "/api/v1/orders/$($first.orderId)/go" @{} 400
if (!$prior) { $null = Invoke-RestMethod "$BaseUrl/api/v1/orders/$($first.orderId)/go" -Method Post -Headers $headers -WebSession $ops }
Expect-Rejection $ops "/api/v1/orders/$($first.orderId)/go" $headers 400
Expect-Rejection $ops "/api/v1/orders/$($first.orderId)/cancel" $headers 400
$pending = @()
try {
    foreach ($choice in $choices[1..2]) { $set = Invoke-RestMethod "$BaseUrl/api/v1/signals/$($choice.id)/set" -Method Post -Headers $headers -WebSession $ops; $pending += $set.orderId }
    Expect-Rejection $ops "/api/v1/signals/$($choices[3].id)/set" $headers 400
} finally {
    foreach ($id in $pending) { $null = Invoke-RestMethod "$BaseUrl/api/v1/orders/$id/cancel" -Method Post -Headers $headers -WebSession $ops }
}
$after = Invoke-RestMethod "$BaseUrl/api/v1/orders" -WebSession $ops
$executed = $after | Where-Object id -eq $first.orderId
Assert-Check ($executed.status -eq 'Executed' -and $executed.quantity -eq 1000 -and $executed.riskAmount -eq 1000 -and $executed.executedUtc) 'SET quantity/risk and GO timestamp persisted on same order ID'
Assert-Check (@($after|Where-Object { $_.id -in $pending -and $_.status -eq 'Cancelled' }).Count -eq 2) 'Cancellation persisted for both remaining test reservations'
foreach ($section in @('Set','Go','Tracker')) {
    $html=(Invoke-WebRequest "$BaseUrl/Orders/$section" -UseBasicParsing -WebSession $ops).Content
    Assert-Check ($html.Contains('order-number">#'+$first.orderId+'</span>') -eq ($section -ne 'Set')) "Executed order membership in $section"
}
$other = $orders | Select-Object -First 1
Expect-Rejection $ops "/api/v1/orders/$($other.id)/go" $headers 400
Write-Output "DONE: executed test order #$($first.orderId); original client records unchanged by workflow tests."
