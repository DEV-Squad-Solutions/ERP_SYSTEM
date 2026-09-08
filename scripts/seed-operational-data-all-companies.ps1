[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Password,

    [string]$BaseUrl = "https://localhost:5001",
    [string]$UserName = "admin",
    [string]$Prefix = "AUTO-DEMO",
    [ValidateRange(1, 20)]
    [int]$CustomerCount = 3,
    [ValidateRange(1, 20)]
    [int]$SupplierCount = 3,
    [ValidateRange(1, 50)]
    [int]$ItemCount = 5,
    [ValidateRange(0, 20)]
    [int]$DriverCount = 2,
    [ValidateRange(0, 50)]
    [int]$EmployeeCount = 3,
    [switch]$Preview,
    [switch]$ContinueOnError,
    [switch]$SkipCertificateCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ApiRoot = $BaseUrl.TrimEnd('/') + "/api/v1"

function Convert-ToJsonBody {
    param([Parameter(Mandatory = $true)]$Value)

    return $Value | ConvertTo-Json -Depth 20 -Compress
}

function Get-ProblemMessage {
    param([Parameter(Mandatory = $true)]$ErrorRecord)

    if (-not [string]::IsNullOrWhiteSpace($ErrorRecord.ErrorDetails.Message)) {
        try {
            $problem = $ErrorRecord.ErrorDetails.Message | ConvertFrom-Json
            if ($problem.errors) {
                return (($problem.errors | ForEach-Object {
                    if ($_.description) { $_.description } else { $_ | Out-String }
                }) -join " | ")
            }
            if ($problem.detail) { return [string]$problem.detail }
            if ($problem.title) { return [string]$problem.title }
        }
        catch {
            return $ErrorRecord.ErrorDetails.Message
        }
    }

    $response = $ErrorRecord.Exception.Response
    if ($null -eq $response) {
        return $ErrorRecord.Exception.Message
    }

    try {
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if ([string]::IsNullOrWhiteSpace($content)) {
            return $ErrorRecord.Exception.Message
        }

        $problem = $content | ConvertFrom-Json
        if ($problem.errors) {
            return (($problem.errors | ForEach-Object {
                if ($_.description) { $_.description } else { $_ | Out-String }
            }) -join " | ")
        }
        if ($problem.detail) { return [string]$problem.detail }
        if ($problem.title) { return [string]$problem.title }
        return $content
    }
    catch {
        return $ErrorRecord.Exception.Message
    }
}

function Invoke-ErpApi {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("GET", "POST", "PUT")]
        [string]$Method,
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [hashtable]$Headers,
        $Body
    )

    $parameters = @{
        Method = $Method
        Uri = $ApiRoot + $Path
        ContentType = "application/json; charset=utf-8"
    }
    if ($Headers) { $parameters.Headers = $Headers }
    if ($null -ne $Body) { $parameters.Body = Convert-ToJsonBody $Body }
    if ($SkipCertificateCheck) {
        $parameters.SkipCertificateCheck = $true
    }

    try {
        $response = Invoke-RestMethod @parameters
        if ($response -is [System.Array]) {
            foreach ($item in $response) {
                Write-Output $item
            }
            return
        }
        return $response
    }
    catch {
        $message = Get-ProblemMessage $_
        throw "API $Method $Path failed: $message"
    }
}

function Invoke-Login {
    return Invoke-ErpApi -Method POST -Path "/Auth/login" -Body @{
        userName = $UserName
        password = $Password
    }
}

function Get-CompanyHeaders {
    param([Parameter(Mandatory = $true)][int]$CompanyId)

    $login = Invoke-Login
    if ($login.requiresCompanySelection) {
        $token = Invoke-ErpApi -Method POST -Path "/Auth/select-company" -Body @{
            selectionToken = $login.selectionToken
            companyId = $CompanyId
        }
        return @{ Authorization = "Bearer $($token.accessToken)" }
    }

    $onlyCompany = @($login.companies) | Select-Object -First 1
    if ($null -eq $onlyCompany -or [int]$onlyCompany.id -ne $CompanyId) {
        throw "The user cannot select company $CompanyId. Assign the Admin user to every company first."
    }
    return @{ Authorization = "Bearer $($login.accessToken)" }
}

function Get-AllCompanies {
    param([hashtable]$Headers)

    $companies = @()
    $pageNumber = 1
    do {
        $page = Invoke-ErpApi -Method GET `
            -Path "/Companies?pageNumber=$pageNumber&pageSize=100" `
            -Headers $Headers
        $companies += @($page.items)
        $pageNumber++
    } while ($pageNumber -le [int]$page.totalPages)

    return $companies
}

function Find-ByExactName {
    param($Rows, [string]$Name)

    return @($Rows) | Where-Object { [string]$_.name -eq $Name } |
        Select-Object -First 1
}

function Get-OrCreateNamedEntity {
    param(
        [hashtable]$Headers,
        [string]$SelectPath,
        [string]$CreatePath,
        [string]$Name,
        [hashtable]$Body
    )

    $rows = @(Invoke-ErpApi -Method GET -Path $SelectPath -Headers $Headers)
    $existing = Find-ByExactName $rows $Name
    if ($existing) {
        return $existing
    }

    if ($Preview) {
        Write-Host "  [preview] create $CreatePath : $Name"
        return $null
    }

    return Invoke-ErpApi -Method POST -Path $CreatePath `
        -Headers $Headers -Body $Body
}

function Get-InvoiceByNumber {
    param([hashtable]$Headers, [string]$InvoiceNumber)

    $encoded = [Uri]::EscapeDataString($InvoiceNumber)
    $page = Invoke-ErpApi -Method GET `
        -Path "/Invoices?pageNumber=1&pageSize=5&invoiceNumber=$encoded" `
        -Headers $Headers
    $row = @($page.items) | Where-Object {
        [string]$_.invoiceNumber -eq $InvoiceNumber
    } | Select-Object -First 1
    if ($null -eq $row) { return $null }
    return Invoke-ErpApi -Method GET -Path "/Invoices/$($row.id)" `
        -Headers $Headers
}

function New-InvoiceBody {
    param(
        [string]$InvoiceNumber,
        [string]$InvoiceType,
        [string]$InvoiceDate,
        [int]$StoreId,
        [int]$PartnerId,
        [int]$CategoryId,
        [int]$ItemId,
        [decimal]$Quantity,
        [decimal]$Price,
        [Nullable[int]]$SourceInvoiceLineId = $null,
        [Nullable[decimal]]$ReturnUnitCost = $null
    )

    return @{
        invoiceNumber = $InvoiceNumber
        invoiceType = $InvoiceType
        itemsCategoryId = $CategoryId
        contentType = "Items"
        paymentTerm = "Credit"
        invoiceDate = $InvoiceDate
        dueDate = $InvoiceDate
        storeId = $StoreId
        businessPartnerId = $PartnerId
        partnerInvoiceNo = "$InvoiceNumber-PARTNER"
        cashboxId = $null
        exchangeRate = $null
        cashboxExchangeRate = $null
        wbWeight = 0
        wbScaleDifference = 0
        wbDiscount = 0
        containerStoreId = $null
        countryId = $null
        driverId = $null
        actualDriverName = $null
        usesExternalDriver = $false
        externalDriverName = $null
        vehicleNumber = $null
        exportInvoiceCode = $null
        discountAmount = 0
        paidAmount = 0
        notes = "$Prefix generated operational data"
        lines = @(@{
            itemId = $ItemId
            count = $null
            weight = $null
            quantity = $Quantity
            price = $Price
            notes = "$Prefix generated line"
            sourceInvoiceLineId = $SourceInvoiceLineId
            returnUnitCost = $ReturnUnitCost
            itemName = $null
        })
        containerLines = @()
        wbTotal = $null
    }
}

function Ensure-Invoice {
    param([hashtable]$Headers, [hashtable]$Body)

    $existing = Get-InvoiceByNumber $Headers $Body.invoiceNumber
    if ($existing) { return $existing }
    if ($Preview) {
        Write-Host "  [preview] create invoice $($Body.invoiceNumber)"
        return $null
    }
    return Invoke-ErpApi -Method POST -Path "/Invoices" `
        -Headers $Headers -Body $Body
}

function Ensure-PostedVoucher {
    param(
        [hashtable]$Headers,
        [string]$Description,
        [string]$Date,
        [string]$Direction,
        [int]$CashboxId,
        [decimal]$Amount,
        [Nullable[int]]$BusinessPartnerId,
        [Nullable[int]]$AccountId
    )

    $encoded = [Uri]::EscapeDataString($Description)
    $page = Invoke-ErpApi -Method GET `
        -Path "/CashVouchers?pageNumber=1&pageSize=5&search=$encoded" `
        -Headers $Headers
    $draft = @($page.items) | Where-Object {
        [string]$_.description -eq $Description
    } | Select-Object -First 1
    if ($draft -and -not [bool]$draft.isDraft) { return }
    if ($Preview) {
        $action = if ($draft) { "post existing draft voucher" } else { "create posted voucher" }
        Write-Host "  [preview] $action`: $Description"
        return
    }

    if ($null -eq $draft) {
        $draft = Invoke-ErpApi -Method POST -Path "/CashVouchers" `
            -Headers $Headers -Body @{
                voucherDate = $Date
                direction = $Direction
                cashboxId = $CashboxId
                amount = $Amount
                description = $Description
            }
    }
    Invoke-ErpApi -Method PUT -Path "/CashVouchers/$($draft.id)" `
        -Headers $Headers -Body @{
            voucherDate = $Date
            direction = $Direction
            cashboxId = $CashboxId
            cashMovementTypeId = $null
            employeeId = $null
            businessPartnerId = $BusinessPartnerId
            driverId = $null
            driverTripId = $null
            externalPartyName = $null
            accountId = $AccountId
            amount = $Amount
            referenceNumber = "$Prefix-$($draft.id)"
            description = $Description
            notes = "$Prefix generated posted voucher"
            rowVersion = $draft.rowVersion
            exchangeRate = $null
        } | Out-Null
}

function Ensure-JournalEntry {
    param(
        [hashtable]$Headers,
        [int]$FiscalYearId,
        [string]$Date,
        [string]$Description,
        [string]$EntryType,
        [int]$DebitAccountId,
        [int]$CreditAccountId,
        [decimal]$Amount
    )

    $encoded = [Uri]::EscapeDataString($Description)
    $page = Invoke-ErpApi -Method GET `
        -Path "/JournalEntries?pageNumber=1&pageSize=5&search=$encoded&fiscalYearId=$FiscalYearId" `
        -Headers $Headers
    $existing = @($page.items) | Where-Object {
        [string]$_.description -eq $Description
    } | Select-Object -First 1
    if ($existing) { return }
    if ($Preview) {
        Write-Host "  [preview] create $EntryType journal: $Description"
        return
    }

    Invoke-ErpApi -Method POST -Path "/JournalEntries" `
        -Headers $Headers -Body @{
            fiscalYearId = $FiscalYearId
            entryDate = $Date
            description = $Description
            entryType = $EntryType
            lines = @(
                @{
                    accountId = $DebitAccountId
                    description = "$Description - debit"
                    debit = $Amount
                    credit = 0
                },
                @{
                    accountId = $CreditAccountId
                    description = "$Description - credit"
                    debit = 0
                    credit = $Amount
                }
            )
        } | Out-Null
}

function Get-SeedDate {
    param($FiscalYear)

    $start = [DateTime]::Parse([string]$FiscalYear.startDate)
    $end = [DateTime]::Parse([string]$FiscalYear.endDate)
    $today = [DateTime]::Today
    if ($today -lt $start) { return $start.AddDays(30) }
    if ($today -gt $end) { return $end.AddDays(-1) }
    return $today
}

function Seed-Company {
    param($Company)

    $companyId = [int]$Company.id
    Write-Host "Company $companyId - $($Company.name)"
    $headers = Get-CompanyHeaders $companyId
    $companyDetails = Invoke-ErpApi -Method GET `
        -Path "/Companies/$companyId" -Headers $headers
    $baseCurrency = [string]$companyDetails.baseCurrency

    $years = @(
        Invoke-ErpApi -Method GET -Path "/FiscalYears/select" `
            -Headers $headers |
            Where-Object { $null -ne $_ }
    )
    $fiscalYear = $years | Where-Object {
        $null -ne $_ -and
        $_.PSObject.Properties.Name -contains "status" -and
        [string]$_.status -eq "Open"
    } | Sort-Object @{ Expression = { [bool]$_.isCurrent }; Descending = $true }, `
        @{ Expression = { [DateTime]$_.startDate }; Descending = $true } |
        Select-Object -First 1
    if ($null -eq $fiscalYear) {
        $lastYear = $years | Sort-Object {
            [DateTime]$_.endDate
        } -Descending | Select-Object -First 1
        if ($lastYear) {
            $newStart = ([DateTime]$lastYear.endDate).AddDays(1)
            $newEnd = $newStart.AddYears(1).AddDays(-1)
        }
        else {
            $newStart = [DateTime]::new([DateTime]::Today.Year, 1, 1)
            $newEnd = [DateTime]::new([DateTime]::Today.Year, 12, 31)
        }

        if ($Preview) {
            Write-Host "  [preview] create fiscal year $($newStart.Year) ($($newStart.ToString('yyyy-MM-dd')) to $($newEnd.ToString('yyyy-MM-dd')))"
            Write-Host "  [preview] its default chart, mappings and financial statements will be initialized automatically"
            Write-Host "  [preview] operational records will be created after fiscal-year initialization"
            return
        }

        $fiscalYear = Invoke-ErpApi -Method POST -Path "/FiscalYears" `
            -Headers $headers -Body @{
                name = [string]$newStart.Year
                startDate = $newStart.ToString("yyyy-MM-dd")
                endDate = $newEnd.ToString("yyyy-MM-dd")
                isCurrent = $true
            }
    }
    $seedDateValue = Get-SeedDate $fiscalYear
    $seedDate = $seedDateValue.ToString("yyyy-MM-dd")

    if ($Preview) {
        Write-Host "  [preview] fiscal year $($fiscalYear.name), date $seedDate"
        Write-Host "  [preview] $CustomerCount customers, $SupplierCount suppliers, $ItemCount items"
        Write-Host "  [preview] $ItemCount purchases, $ItemCount sales, 2 returns"
        Write-Host "  [preview] 3 posted vouchers, 1 manual journal, 1 adjustment journal"
        Write-Host "  [preview] $DriverCount drivers, $EmployeeCount employees; payroll excluded"
        return
    }

    $unitName = "$Prefix وحدة"
    $unit = Get-OrCreateNamedEntity -Headers $headers `
        -SelectPath "/ItemUnits/select" -CreatePath "/ItemUnits" `
        -Name $unitName -Body @{ name = $unitName; isActive = $true }

    $categoryName = "$Prefix تصنيف"
    $category = Get-OrCreateNamedEntity -Headers $headers `
        -SelectPath "/ItemsCategories/select" -CreatePath "/ItemsCategories" `
        -Name $categoryName -Body @{
            name = $categoryName
            isActive = $true
            notes = "$Prefix generated category"
        }

    $storeName = "$Prefix مخزن رئيسي"
    $store = Get-OrCreateNamedEntity -Headers $headers `
        -SelectPath "/Stores/select" -CreatePath "/Stores" `
        -Name $storeName -Body @{
            name = $storeName
            address = "Generated test warehouse"
            isContainerStore = $false
            businessPartnerId = $null
            isActive = $true
        }

    $cashboxName = "$Prefix خزينة رئيسية"
    $cashbox = Get-OrCreateNamedEntity -Headers $headers `
        -SelectPath "/Cashboxes/select" -CreatePath "/Cashboxes" `
        -Name $cashboxName -Body @{
            name = $cashboxName
            currency = $baseCurrency
            openingBalance = 50000
            isActive = $true
            notes = "$Prefix generated cashbox"
            openingBalanceDate = $seedDate
            openingExchangeRate = $null
        }

    $partners = @(Invoke-ErpApi -Method GET -Path "/BusinessPartners/select" `
        -Headers $headers)
    $customers = @()
    foreach ($index in 1..$CustomerCount) {
        $name = "$Prefix عميل $index"
        $partner = Find-ByExactName $partners $name
        if ($null -eq $partner) {
            $partner = Invoke-ErpApi -Method POST -Path "/BusinessPartners" `
                -Headers $headers -Body @{
                    name = $name
                    phoneNumber = "010$companyId$($index.ToString('000000'))"
                    email = "customer-$companyId-$index@example.local"
                    address = "Generated customer address"
                    taxNumber = $null
                    currency = $baseCurrency
                    creditLimit = 100000
                    isActive = $true
                    special = $false
                }
            $partners += $partner
        }
        $customers += $partner
    }

    $suppliers = @()
    foreach ($index in 1..$SupplierCount) {
        $name = "$Prefix مورد $index"
        $partner = Find-ByExactName $partners $name
        if ($null -eq $partner) {
            $partner = Invoke-ErpApi -Method POST -Path "/BusinessPartners" `
                -Headers $headers -Body @{
                    name = $name
                    phoneNumber = "011$companyId$($index.ToString('000000'))"
                    email = "supplier-$companyId-$index@example.local"
                    address = "Generated supplier address"
                    taxNumber = $null
                    currency = $baseCurrency
                    creditLimit = 100000
                    isActive = $true
                    special = $false
                }
            $partners += $partner
        }
        $suppliers += $partner
    }

    $items = @(Invoke-ErpApi -Method GET -Path "/Items/select" `
        -Headers $headers)
    $seedItems = @()
    foreach ($index in 1..$ItemCount) {
        $name = "$Prefix صنف $index"
        $item = Find-ByExactName $items $name
        if ($null -eq $item) {
            $item = Invoke-ErpApi -Method POST -Path "/Items" `
                -Headers $headers -Body @{
                    itemUnitId = [int]$unit.id
                    name = $name
                    description = "$Prefix generated item"
                    isActive = $true
                }
            $items += $item
        }
        $seedItems += $item
    }

    for ($index = 0; $index -lt $seedItems.Count; $index++) {
        $sequence = $index + 1
        $item = $seedItems[$index]
        $supplier = $suppliers[$index % $suppliers.Count]
        $customer = $customers[$index % $customers.Count]
        $purchaseNumber = "$Prefix-C$companyId-PUR-$($sequence.ToString('000'))"
        $salesNumber = "$Prefix-C$companyId-SAL-$($sequence.ToString('000'))"
        $purchase = Ensure-Invoice $headers (New-InvoiceBody `
            -InvoiceNumber $purchaseNumber -InvoiceType "Purchase" `
            -InvoiceDate $seedDate -StoreId ([int]$store.id) `
            -PartnerId ([int]$supplier.id) -CategoryId ([int]$category.id) `
            -ItemId ([int]$item.id) -Quantity 20 -Price (80 + $sequence))
        Ensure-Invoice $headers (New-InvoiceBody `
            -InvoiceNumber $salesNumber -InvoiceType "Sales" `
            -InvoiceDate $seedDate -StoreId ([int]$store.id) `
            -PartnerId ([int]$customer.id) -CategoryId ([int]$category.id) `
            -ItemId ([int]$item.id) -Quantity 5 -Price (120 + $sequence)) |
            Out-Null
    }

    $firstItem = $seedItems[0]
    $firstPurchase = Get-InvoiceByNumber $headers `
        "$Prefix-C$companyId-PUR-001"
    $firstSale = Get-InvoiceByNumber $headers `
        "$Prefix-C$companyId-SAL-001"
    if ($firstSale -and @($firstSale.lines).Count -gt 0) {
        Ensure-Invoice $headers (New-InvoiceBody `
            -InvoiceNumber "$Prefix-C$companyId-SRET-001" `
            -InvoiceType "SalesReturn" -InvoiceDate $seedDate `
            -StoreId ([int]$store.id) -PartnerId ([int]$customers[0].id) `
            -CategoryId ([int]$category.id) -ItemId ([int]$firstItem.id) `
            -Quantity 1 -Price 121 `
            -SourceInvoiceLineId ([int]$firstSale.lines[0].id)) | Out-Null
    }
    if ($firstPurchase -and @($firstPurchase.lines).Count -gt 0) {
        Ensure-Invoice $headers (New-InvoiceBody `
            -InvoiceNumber "$Prefix-C$companyId-PRET-001" `
            -InvoiceType "PurchaseReturn" -InvoiceDate $seedDate `
            -StoreId ([int]$store.id) -PartnerId ([int]$suppliers[0].id) `
            -CategoryId ([int]$category.id) -ItemId ([int]$firstItem.id) `
            -Quantity 2 -Price 81 `
            -SourceInvoiceLineId ([int]$firstPurchase.lines[0].id)) | Out-Null
    }

    $accounts = @(Invoke-ErpApi -Method GET `
        -Path "/Accounts/journal-select?fiscalYearId=$($fiscalYear.id)" `
        -Headers $headers)
    $expenseAccount = $accounts | Where-Object { $_.code -eq "5300" } |
        Select-Object -First 1
    $capitalAccount = $accounts | Where-Object { $_.code -eq "3100" } |
        Select-Object -First 1
    if ($null -eq $expenseAccount -or $null -eq $capitalAccount) {
        throw "Company $companyId is missing default accounts 5300 or 3100."
    }

    Ensure-PostedVoucher -Headers $headers `
        -Description "$Prefix قبض من عميل" -Date $seedDate `
        -Direction "Receipt" -CashboxId ([int]$cashbox.id) -Amount 1000 `
        -BusinessPartnerId ([int]$customers[0].id) -AccountId $null
    Ensure-PostedVoucher -Headers $headers `
        -Description "$Prefix سداد لمورد" -Date $seedDate `
        -Direction "Payment" -CashboxId ([int]$cashbox.id) -Amount 700 `
        -BusinessPartnerId ([int]$suppliers[0].id) -AccountId $null
    Ensure-PostedVoucher -Headers $headers `
        -Description "$Prefix مصروف إداري" -Date $seedDate `
        -Direction "Payment" -CashboxId ([int]$cashbox.id) -Amount 250 `
        -BusinessPartnerId $null -AccountId ([int]$expenseAccount.id)

    Ensure-JournalEntry -Headers $headers `
        -FiscalYearId ([int]$fiscalYear.id) -Date $seedDate `
        -Description "$Prefix قيد يدوي" -EntryType "Manual" `
        -DebitAccountId ([int]$expenseAccount.id) `
        -CreditAccountId ([int]$capitalAccount.id) -Amount 500
    Ensure-JournalEntry -Headers $headers `
        -FiscalYearId ([int]$fiscalYear.id) -Date $seedDate `
        -Description "$Prefix قيد تسوية" -EntryType "Adjustment" `
        -DebitAccountId ([int]$expenseAccount.id) `
        -CreditAccountId ([int]$capitalAccount.id) -Amount 100

    if ($DriverCount -gt 0) {
        $drivers = @(Invoke-ErpApi -Method GET -Path "/Drivers/select" `
            -Headers $headers)
        foreach ($index in 1..$DriverCount) {
            $name = "$Prefix سائق $index"
            if ($null -eq (Find-ByExactName $drivers $name)) {
                $driver = Invoke-ErpApi -Method POST -Path "/Drivers" `
                    -Headers $headers -Body @{
                        name = $name
                        phoneNumber = "012$companyId$($index.ToString('000000'))"
                        nationalId = $null
                        licenseNumber = "$Prefix-$companyId-$index"
                        licenseExpiryDate = $seedDateValue.AddYears(2).ToString("yyyy-MM-dd")
                        isActive = $true
                    }
                $drivers += $driver
            }
        }
    }

    if ($EmployeeCount -gt 0) {
        $employeePage = Invoke-ErpApi -Method GET `
            -Path "/Employees/GetAll?pageNumber=1&pageSize=100&search=$([Uri]::EscapeDataString($Prefix))" `
            -Headers $headers
        foreach ($index in 1..$EmployeeCount) {
            $name = "$Prefix موظف $index"
            $existing = @($employeePage.employees) | Where-Object {
                [string]$_.name -eq $name
            } | Select-Object -First 1
            if ($null -eq $existing) {
                Invoke-ErpApi -Method POST -Path "/Employees" `
                    -Headers $headers -Body @{
                        name = $name
                        jobTitle = "موظف تجريبي"
                        phoneNumber = "015$companyId$($index.ToString('000000'))"
                        email = "employee-$companyId-$index@example.local"
                        address = "Generated employee address"
                        type = "Monthly"
                        salary = 5000 + ($index * 250)
                        requiredWorkingDaysPerMonth = 26
                        isActive = $true
                    } | Out-Null
            }
        }
    }

    Write-Host "  completed"
}

$initialLogin = Invoke-Login
$assignedCompanies = @($initialLogin.companies)
if ($assignedCompanies.Count -eq 0) {
    throw "The login user is not assigned to any company."
}

$firstCompanyHeaders = Get-CompanyHeaders ([int]$assignedCompanies[0].id)
$allCompanies = @(Get-AllCompanies $firstCompanyHeaders)
$assignedIds = @($assignedCompanies | ForEach-Object { [int]$_.id })
$unassignedCompanies = @($allCompanies | Where-Object {
    $assignedIds -notcontains [int]$_.id
})
if ($unassignedCompanies.Count -gt 0) {
    $missing = $unassignedCompanies | ForEach-Object { "$($_.id) - $($_.name)" }
    throw "The Admin user is not assigned to every company. Missing: $($missing -join ', ')"
}

if ($Preview) {
    Write-Host "Preview only. No data will be written."
}

$failures = @()
foreach ($company in $assignedCompanies) {
    try {
        Seed-Company $company
    }
    catch {
        $failures += "Company $($company.id): $($_.Exception.Message)"
        Write-Error $failures[-1] -ErrorAction Continue
        if (-not $ContinueOnError) { throw }
    }
}

if ($failures.Count -gt 0) {
    throw "Seeding finished with $($failures.Count) failed company/companies.`n$($failures -join "`n")"
}

Write-Host "Operational seed completed for $($assignedCompanies.Count) company/companies."
Write-Host "Run the same command again safely; records with the same Prefix are skipped."
