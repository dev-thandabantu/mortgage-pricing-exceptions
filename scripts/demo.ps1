param(
    [string]$BaseUrl = 'http://localhost:5019',
    [string]$ApplicationId = 'app-1001'
)
$ErrorActionPreference = 'Stop'
$rm = @{ 'X-User-Id' = 'rm-alex'; 'Idempotency-Key' = [guid]::NewGuid().ToString() }
$reviewer = @{ 'X-User-Id' = 'reviewer-jules' }
$body = @{ applicationId = $ApplicationId; discountBps = 25; reason = 'Customer retention' } | ConvertTo-Json
$created = Invoke-RestMethod "$BaseUrl/pricing-exceptions" -Method Post -Headers $rm -ContentType 'application/json' -Body $body
$replay = Invoke-RestMethod "$BaseUrl/pricing-exceptions" -Method Post -Headers $rm -ContentType 'application/json' -Body $body
if ($created.id -ne $replay.id) { throw 'Retry created a different request.' }
Write-Output "Created revision 1; retry returned the same request: $($created.id)"
$amendment = @{ discountBps = 40; reason = 'Corrected retention offer'; expectedVersion = 1 } | ConvertTo-Json
$amended = Invoke-RestMethod "$BaseUrl/pricing-exceptions/$($created.id)" -Method Patch -Headers $rm -ContentType 'application/json' -Body $amendment
$stale = @{ outcome = 'Approved'; expectedVersion = 1; comment = 'Stale review' } | ConvertTo-Json
try {
    Invoke-RestMethod "$BaseUrl/pricing-exceptions/$($created.id)/decisions" -Method Post -Headers $reviewer -ContentType 'application/json' -Body $stale | Out-Null
    throw 'Stale approval unexpectedly succeeded.'
} catch {
    if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 409) { throw }
}
Write-Output 'Amended to 40 bps; stale approval rejected with 409.'
$decision = @{ outcome = 'Approved'; expectedVersion = $amended.version; comment = 'Reviewed corrected terms' } | ConvertTo-Json
$approved = Invoke-RestMethod "$BaseUrl/pricing-exceptions/$($created.id)/decisions" -Method Post -Headers $reviewer -ContentType 'application/json' -Body $decision
$discount = Invoke-RestMethod "$BaseUrl/applications/$ApplicationId/approved-discount" -Headers @{ 'X-User-Id' = 'mortgage-process' }
if ($discount.discountBps -ne 40 -or $discount.revisionNumber -ne 2 -or $discount.decisionId -ne $approved.decisions[0].id) { throw 'Approved discount/provenance mismatch.' }
Write-Output 'Approved revision 2; downstream received 40 bps with matching decision.'
$discount | ConvertTo-Json
