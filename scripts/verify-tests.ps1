# Hallagi acceptance tests 1..10 (ASCII-only to avoid PS 5.1 encoding issues)
$api = "http://localhost:5095"
$pass = "Hallagi@123"
$results = @()

function Login($email) {
    $body = @{ email = $email; password = $pass } | ConvertTo-Json
    return Invoke-RestMethod -Uri "$api/api/auth/login" -Method Post -Body $body -ContentType "application/json"
}

$owner = Login "owner@hallagi.sa"
$cashier = Login "cashier@hallagi.sa"
$barberUser = Login "barber@hallagi.sa"

$oh = @{ Authorization = "Bearer $($owner.token)" }
$ch = @{ Authorization = "Bearer $($cashier.token)" }
$bh = @{ Authorization = "Bearer $($barberUser.token)" }

# cashier has exactly one branch; owner has all -> use cashier's branch for the POS tests
$saleBranch = $cashier.branches[0]
$otherBranch = ($owner.branches | Where-Object { $_.id -ne $saleBranch.id } | Select-Object -First 1)

$posN = Invoke-RestMethod -Uri "$api/api/pos/data?branchId=$($saleBranch.id)" -Headers $oh
$svc25 = $posN.services | Where-Object { $_.price -eq 25 }
$svc35 = $posN.services | Where-Object { $_.price -eq 35 }
$barber = $posN.barbers | Select-Object -First 1

# ---------- TEST 1: cash sale 25 ----------
$before = Invoke-RestMethod -Uri "$api/api/treasury/state?branchId=$($saleBranch.id)" -Headers $oh
$idem1 = [guid]::NewGuid().ToString()
$body1 = @{ branchId = $saleBranch.id; barberId = $barber.id; customerId = $null; paymentMethod = "Cash"; idempotencyKey = $idem1; items = @(@{ serviceId = $svc25.id; quantity = 1 }) } | ConvertTo-Json
$sale1 = Invoke-RestMethod -Uri "$api/api/sales" -Method Post -Headers $ch -Body $body1 -ContentType "application/json"
$after1 = Invoke-RestMethod -Uri "$api/api/treasury/state?branchId=$($saleBranch.id)" -Headers $oh
$ok1a = $sale1.sale.totalAmount -eq 25
$ok1b = [math]::Round($after1.current.expectedCash - $before.current.expectedCash, 2) -eq 25
$results += "TEST1 cash sale 25: total=25 [$ok1a] ; drawer+25 [$ok1b]"

# ---------- TEST 2: card sale 35 ----------
$idem2 = [guid]::NewGuid().ToString()
$body2 = @{ branchId = $saleBranch.id; barberId = $barber.id; customerId = $null; paymentMethod = "Card"; idempotencyKey = $idem2; items = @(@{ serviceId = $svc35.id; quantity = 1 }) } | ConvertTo-Json
$sale2 = Invoke-RestMethod -Uri "$api/api/sales" -Method Post -Headers $ch -Body $body2 -ContentType "application/json"
$after2 = Invoke-RestMethod -Uri "$api/api/treasury/state?branchId=$($saleBranch.id)" -Headers $oh
$ok2a = $after2.current.cardSales -ge 35
$ok2b = [math]::Round($after2.current.expectedCash, 2) -eq [math]::Round($after1.current.expectedCash, 2)
$results += "TEST2 card sale 35: cardSales>=35 [$ok2a] ; drawer unchanged [$ok2b]"

# ---------- TEST 10: idempotency replay ----------
$replay = Invoke-RestMethod -Uri "$api/api/sales" -Method Post -Headers $ch -Body $body1 -ContentType "application/json"
$ok10 = ($replay.replayed -eq $true) -and ($replay.sale.id -eq $sale1.sale.id)
$results += "TEST10 replay same key -> no duplicate [$ok10]"

# ---------- TEST 3: customer visit recorded once ----------
$idem3 = [guid]::NewGuid().ToString()
$cb = @{ fullName = "Test Customer"; phone = "0555551234"; notes = "qa" } | ConvertTo-Json
$cust = Invoke-RestMethod -Uri "$api/api/customers" -Method Post -Headers $ch -Body $cb -ContentType "application/json"
$body3 = @{ branchId = $saleBranch.id; barberId = $barber.id; customerId = $cust.id; paymentMethod = "Cash"; idempotencyKey = $idem3; items = @(@{ serviceId = $svc25.id; quantity = 2 }) } | ConvertTo-Json
$sale3 = Invoke-RestMethod -Uri "$api/api/sales" -Method Post -Headers $ch -Body $body3 -ContentType "application/json"
$cf = Invoke-RestMethod -Uri "$api/api/customers/$($cust.id)" -Headers $oh
$ok3 = ($cf.visits | Where-Object { $_.id -eq $sale3.sale.id } | Measure-Object).Count -eq 1
$results += "TEST3 customer visit appears exactly once [$ok3]"

# ---------- TEST 4: cash expense 50 ----------
$before4 = Invoke-RestMethod -Uri "$api/api/treasury/state?branchId=$($saleBranch.id)" -Headers $oh
$exbody = @{ branchId = $saleBranch.id; category = "Supplies"; amount = 50; expenseDate = (Get-Date).ToString("yyyy-MM-dd"); description = "qa"; source = "CashDrawer" } | ConvertTo-Json
$exp = Invoke-RestMethod -Uri "$api/api/expenses" -Method Post -Headers $ch -Body $exbody -ContentType "application/json"
$after4 = Invoke-RestMethod -Uri "$api/api/treasury/state?branchId=$($saleBranch.id)" -Headers $oh
$ok4 = [math]::Round($after4.current.expectedCash - $before4.current.expectedCash, 2) -eq -50
$results += "TEST4 cash expense 50: drawer -50 [$ok4]"

# ---------- TEST 5: bank expense 50 ----------
$exbody2 = @{ branchId = $saleBranch.id; category = "Maintenance"; amount = 50; expenseDate = (Get-Date).ToString("yyyy-MM-dd"); description = "qa"; source = "Bank" } | ConvertTo-Json
$exp2 = Invoke-RestMethod -Uri "$api/api/expenses" -Method Post -Headers $oh -Body $exbody2 -ContentType "application/json"
$after4b = Invoke-RestMethod -Uri "$api/api/treasury/state?branchId=$($saleBranch.id)" -Headers $oh
$ok5 = [math]::Round($after4b.current.expectedCash, 2) -eq [math]::Round($after4.current.expectedCash, 2)
$results += "TEST5 bank expense 50: drawer unchanged [$ok5]"

# ---------- TEST 6: shift close shortage ----------
$expectedNow = $after4b.current.expectedCash
$closeBody = @{ branchId = $saleBranch.id; countedCash = $expectedNow - 10; note = "qa" } | ConvertTo-Json
$closed = Invoke-RestMethod -Uri "$api/api/treasury/close" -Method Post -Headers $oh -Body $closeBody -ContentType "application/json"
$ok6 = ($closed.expectedCash -eq $expectedNow) -and ($closed.countedCash -eq $expectedNow - 10) -and ($closed.difference -eq -10) -and ($closed.status -eq "Closed")
$results += "TEST6 shift close: expected=$($closed.expectedCash) counted=$($closed.countedCash) diff=$($closed.difference) result=$($closed.differenceText) [$ok6]"

# ---------- TEST 7: barber restrictions ----------
$barberMe = Invoke-RestMethod -Uri "$api/api/me/summary" -Headers $bh
$ok7a = $barberMe.role -eq "Barber" -and $barberMe.barber -ne $null
$forbidden = $false
try { Invoke-RestMethod -Uri "$api/api/expenses" -Headers $bh | Out-Null } catch { $forbidden = $true }
$ok7b = $forbidden
$forbidden2 = $false
try { Invoke-RestMethod -Uri "$api/api/treasury/state?branchId=$($otherBranch.id)" -Headers $bh | Out-Null } catch { $forbidden2 = $true }
$ok7c = $forbidden2
$results += "TEST7 barber: own data [$ok7a] ; expenses denied [$ok7b] ; other branch denied [$ok7c]"

# ---------- TEST 8: branch isolation ----------
$forbidden3 = $false
try {
    $bodyY = @{ branchId = $otherBranch.id; barberId = $barber.id; customerId = $null; paymentMethod = "Cash"; idempotencyKey = [guid]::NewGuid().ToString(); items = @(@{ serviceId = $svc25.id; quantity = 1 }) } | ConvertTo-Json
    Invoke-RestMethod -Uri "$api/api/sales" -Method Post -Headers $ch -Body $bodyY -ContentType "application/json" | Out-Null
} catch { $forbidden3 = $true }
$results += "TEST8 branch isolation: cashier cannot sell in other branch [$forbidden3]"

# ---------- TEST 9: historical price integrity ----------
$updated = Invoke-RestMethod -Uri "$api/api/sales/$($sale1.sale.id)" -Headers $oh
$ok9a = $updated.items[0].unitPrice -eq 25
$newSvc = @{ name = "Price Changed Test"; description = $null; basePrice = 30; active = $true } | ConvertTo-Json
Invoke-RestMethod -Uri "$api/api/services/$($svc25.id)" -Method Put -Headers $oh -Body $newSvc -ContentType "application/json" | Out-Null
$updated2 = Invoke-RestMethod -Uri "$api/api/sales/$($sale1.sale.id)" -Headers $oh
$ok9b = $updated2.items[0].unitPrice -eq 25
$results += "TEST9 historical price: 25 at sale [$ok9a] ; unchanged after edit [$ok9b]"

$results | ForEach-Object { $_ }
