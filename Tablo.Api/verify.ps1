$ErrorActionPreference = 'Stop'

$baseUrl = "http://localhost:5166/api"

# Login as admin of restaurant A
$loginBody = @{
    email = "admin@tablodemo.com"
    password = "password123"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "$baseUrl/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
$token = $response.token

$headers = @{
    Authorization = "Bearer $token"
    "Content-Type" = "application/json"
}

# 1. Get employees (Should only return restaurant A employees)
Write-Host "Getting employees..."
$employees = Invoke-RestMethod -Uri "$baseUrl/employees" -Method Get -Headers $headers
$employees | Select-Object id, firstName, role | Format-Table

# 2. Create Kitchen employee
Write-Host "Creating Kitchen employee..."
$newEmpBody = @{
    firstName = "Test"
    lastName = "Kitchen"
    email = "test.kitchen@tablodemo.com"
    password = "password"
    roleName = "Kitchen"
} | ConvertTo-Json

$newEmp = Invoke-RestMethod -Uri "$baseUrl/employees" -Method Post -Body $newEmpBody -Headers $headers
Write-Host "Created: $($newEmp.email) - Role: $($newEmp.role)"

# 3. Create Waiter employee
Write-Host "Creating Waiter employee..."
$newEmpBody2 = @{
    firstName = "Test"
    lastName = "Waiter"
    email = "test.waiter@tablodemo.com"
    password = "password"
    roleName = "Waiter"
} | ConvertTo-Json

$newEmp2 = Invoke-RestMethod -Uri "$baseUrl/employees" -Method Post -Body $newEmpBody2 -Headers $headers
Write-Host "Created: $($newEmp2.email) - Role: $($newEmp2.role)"

# 4. Attempt to deactivate self
Write-Host "Attempting to deactivate self..."
$adminId = $response.user.id
try {
    $deactivateBody = @{ isActive = $false } | ConvertTo-Json
    Invoke-RestMethod -Uri "$baseUrl/employees/$adminId/status" -Method Patch -Body $deactivateBody -Headers $headers
    Write-Host "FAILED: Allowed self-deactivation!"
} catch {
    Write-Host "SUCCESS: Prevented self-deactivation ($($_.Exception.Response.StatusCode))"
}

# 5. Attempt to demote self
Write-Host "Attempting to demote self..."
try {
    $demoteBody = @{
        firstName = "Admin"
        lastName = "User"
        email = "admin@tablodemo.com"
        roleName = "Kitchen"
    } | ConvertTo-Json
    Invoke-RestMethod -Uri "$baseUrl/employees/$adminId" -Method Put -Body $demoteBody -Headers $headers
    Write-Host "FAILED: Allowed self-demotion!"
} catch {
    Write-Host "SUCCESS: Prevented self-demotion ($($_.Exception.Response.StatusCode))"
}

Write-Host "Verification complete."
