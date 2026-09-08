[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Password,

    [string]$BaseUrl = "https://localhost:5001",
    [string]$UserName = "admin",
    [string]$Prefix = "AUTO-DEMO",
    [switch]$SkipCertificateCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ApiRoot = $BaseUrl.TrimEnd('/') + "/api/v1"

function Invoke-ErpApi {
    param(
        [Parameter(Mandatory = $true)][ValidateSet("GET", "POST")][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [hashtable]$Headers,
        $Body
    )

    $parameters = @{
        Method = $Method
        Uri = $ApiRoot + $Path
        ContentType = "application/json; charset=utf-8"
    }
    if ($Headers) { $parameters.Headers = $Headers }
    if ($null -ne $Body) {
        $parameters.Body = $Body | ConvertTo-Json -Depth 20 -Compress
    }
    if ($SkipCertificateCheck) { $parameters.SkipCertificateCheck = $true }
    return Invoke-RestMethod @parameters
}

function Select-CompanyHeaders {
    param($Login, [int]$CompanyId)

    $selected = Invoke-ErpApi -Method POST -Path "/Auth/select-company" -Body @{
        selectionToken = $Login.selectionToken
        companyId = $CompanyId
    }
    return @{ Authorization = "Bearer $($selected.accessToken)" }
}

function Expand-ResponseRows {
    param($Value)

    if ($null -eq $Value) { return }
    if ($Value -is [System.Array]) {
        foreach ($item in $Value) { Write-Output $item }
        return
    }
    Write-Output $Value
}

$login = Invoke-ErpApi -Method POST -Path "/Auth/login" -Body @{
    userName = $UserName
    password = $Password
}

$rows = @()
foreach ($company in @($login.companies)) {
    $headers = Select-CompanyHeaders $login ([int]$company.id)
    $partners = @(Expand-ResponseRows (Invoke-ErpApi -Method GET -Path "/BusinessPartners/select" -Headers $headers))
    $items = @(Expand-ResponseRows (Invoke-ErpApi -Method GET -Path "/Items/select" -Headers $headers))
    $drivers = @(Expand-ResponseRows (Invoke-ErpApi -Method GET -Path "/Drivers/select" -Headers $headers))
    $invoices = Invoke-ErpApi -Method GET `
        -Path "/Invoices?pageNumber=1&pageSize=100&invoiceNumber=$([Uri]::EscapeDataString($Prefix))" `
        -Headers $headers
    $vouchers = Invoke-ErpApi -Method GET `
        -Path "/CashVouchers?pageNumber=1&pageSize=100&search=$([Uri]::EscapeDataString($Prefix))" `
        -Headers $headers
    $journals = Invoke-ErpApi -Method GET `
        -Path "/JournalEntries?pageNumber=1&pageSize=100&search=$([Uri]::EscapeDataString($Prefix))" `
        -Headers $headers
    $employees = Invoke-ErpApi -Method GET `
        -Path "/Employees/GetAll?pageNumber=1&pageSize=100&search=$([Uri]::EscapeDataString($Prefix))" `
        -Headers $headers
    $years = @(Expand-ResponseRows (Invoke-ErpApi -Method GET -Path "/FiscalYears/select" -Headers $headers)) |
        Where-Object { $null -ne $_ -and [int]$_.id -gt 0 } |
        Sort-Object @{ Expression = { [bool]$_.isCurrent }; Descending = $true }, `
            @{ Expression = { [DateTime]$_.startDate }; Descending = $true }
    $year = $years | Select-Object -First 1
    $readiness = $null
    if ($year) {
        $readiness = Invoke-ErpApi -Method GET `
            -Path "/AccountingReadiness?fiscalYearId=$([int]$year.id)" `
            -Headers $headers
    }

    $rows += [pscustomobject]@{
        CompanyId = [int]$company.id
        Company = [string]$company.name
        Partners = @($partners | Where-Object { $_.name -like "$Prefix *" }).Count
        Items = @($items | Where-Object { $_.name -like "$Prefix *" }).Count
        Invoices = @($invoices.items | Where-Object { $_.invoiceNumber -like "$Prefix-*" }).Count
        Vouchers = @($vouchers.items | Where-Object { $_.description -like "$Prefix *" }).Count
        NamedJournals = @($journals.items | Where-Object { $_.description -like "$Prefix *" }).Count
        Drivers = @($drivers | Where-Object { $_.name -like "$Prefix *" }).Count
        Employees = @($employees.employees | Where-Object { $_.name -like "$Prefix *" }).Count
        FiscalYear = if ($year) { [string]$year.name } else { "NONE" }
        Ready = if ($readiness) { [bool]$readiness.isReady } else { $false }
        TotalSources = if ($readiness) { [int]$readiness.totalSources } else { 0 }
        MissingJournalSources = if ($readiness) { [int]$readiness.missingJournalSources } else { 0 }
        PendingInventoryCosts = if ($readiness) { [int]$readiness.pendingInventoryCosts } else { 0 }
    }
}

$rows | Sort-Object CompanyId | Format-Table -AutoSize
Write-Output "JSON_SUMMARY"
$rows | Sort-Object CompanyId | ConvertTo-Json -Depth 5
