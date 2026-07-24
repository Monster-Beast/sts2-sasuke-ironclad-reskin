param(
    [Parameter(Mandatory = $true)]
    [string]$WorkbookPath,

    [Parameter(Mandatory = $true)]
    [string]$RevisionCsvPath
)

$ErrorActionPreference = 'Stop'

$xlUp = -4162
$xlCalculationAutomatic = -4105
$xlOpenXMLWorkbook = 51

function Get-WorksheetRowMap {
    param(
        [Parameter(Mandatory = $true)]$Worksheet,
        [Parameter(Mandatory = $true)][int]$KeyColumn,
        [int]$StartRow = 1
    )

    $lastRow = $Worksheet.Cells.Item($Worksheet.Rows.Count, $KeyColumn).End($xlUp).Row
    $map = @{}
    for ($row = $StartRow; $row -le $lastRow; $row++) {
        $key = [string]$Worksheet.Cells.Item($row, $KeyColumn).Value2
        if (-not [string]::IsNullOrWhiteSpace($key)) {
            $map[$key] = $row
        }
    }
    return $map
}

function Set-CellText {
    param($Worksheet, [int]$Row, [int]$Column, [string]$Value)
    $Worksheet.Cells.Item($Row, $Column).Value2 = $Value
}

function Get-AuthenticityNote {
    param($Revision)

    if ($Revision.NamingSource -like '*原样采用*') {
        return ('面板名“{0}”采用已经核验的正式招式、状态、能力或忍具名。' -f $Revision.NewName)
    }
    return ('面板名“{0}”是基于“{1}”的动作或能力描述，不宣称为漫画正式忍术名；不得脱离所列动作制作新的拳法、铠甲、护盾或查克拉传递表现。' -f $Revision.NewName, $Revision.Basis)
}

$resolvedWorkbook = (Resolve-Path -LiteralPath $WorkbookPath).Path
$resolvedCsv = (Resolve-Path -LiteralPath $RevisionCsvPath).Path
$revisions = @(Get-Content -LiteralPath $resolvedCsv -Encoding UTF8 | ConvertFrom-Csv -Delimiter '|')
if ($revisions.Count -eq 0) {
    throw 'Revision CSV is empty.'
}

$lockProbe = $null
try {
    $lockProbe = [System.IO.File]::Open(
        $resolvedWorkbook,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None
    )
} catch {
    throw 'The target workbook is open or locked. Close only the target workbook before applying the update.'
} finally {
    if ($null -ne $lockProbe) {
        $lockProbe.Dispose()
    }
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('sts2-m15-' + [guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $tempRoot)
$tempWorkbook = Join-Path $tempRoot 'm15-working.xlsx'
Copy-Item -LiteralPath $resolvedWorkbook -Destination $tempWorkbook
Unblock-File -LiteralPath $tempWorkbook

$excel = $null
$workbook = $null
try {
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $excel.DisplayAlerts = $false
    # The imported workbook contains an overlapping legacy merge range. Excel's
    # repair mode opens a temporary copy and normalizes that package before save.
    $workbook = $excel.Workbooks.Open(
        $tempWorkbook,
        0,
        $false,
        5,
        '',
        '',
        $true,
        1,
        '',
        $false,
        $false,
        0,
        $false,
        $true,
        1
    )
    $excel.Calculation = $xlCalculationAutomatic

    $main = $workbook.Worksheets.Item(1)
    $uniqueCheck = $workbook.Worksheets.Item(5)
    $eraCheck = $workbook.Worksheets.Item(6)
    $sharinganCheck = $workbook.Worksheets.Item(8)
    $namingRules = $workbook.Worksheets.Item(9)
    $webReview = $workbook.Worksheets.Item(11)

    $mainRows = Get-WorksheetRowMap -Worksheet $main -KeyColumn 5 -StartRow 5
    $uniqueRows = Get-WorksheetRowMap -Worksheet $uniqueCheck -KeyColumn 1 -StartRow 4
    $eraRows = Get-WorksheetRowMap -Worksheet $eraCheck -KeyColumn 1 -StartRow 4
    $sharinganRows = Get-WorksheetRowMap -Worksheet $sharinganCheck -KeyColumn 1 -StartRow 4
    $ruleRows = Get-WorksheetRowMap -Worksheet $namingRules -KeyColumn 1 -StartRow 1

    foreach ($revision in $revisions) {
        if (-not $mainRows.ContainsKey($revision.English)) {
            throw ('Missing card in main sheet: {0}' -f $revision.English)
        }
        $row = [int]$mainRows[$revision.English]
        $currentName = [string]$main.Cells.Item($row, 15).Value2
        if ($currentName -ne $revision.OldName) {
            throw ('Unexpected current name for {0}: expected "{1}", found "{2}".' -f $revision.English, $revision.OldName, $currentName)
        }

        $effect = [string]$main.Cells.Item($row, 9).Value2
        Set-CellText $main $row 15 $revision.NewName
        Set-CellText $main $row 16 $revision.Basis
        Set-CellText $main $row 17 $revision.NamingSource
        Set-CellText $main $row 18 $revision.CatalogStatus
        Set-CellText $main $row 20 $revision.Usage
        Set-CellText $main $row 23 ('动作真实性修正：{0} 机制对应：{1}' -f $revision.Reason, $effect)

        $strength = [string]$main.Cells.Item($row, 24).Value2
        if (-not [string]::IsNullOrEmpty($strength)) {
            Set-CellText $main $row 24 ($strength.Replace($revision.OldName, $revision.NewName))
        }
        Set-CellText $main $row 25 $revision.Formal
        Set-CellText $main $row 26 $revision.SourceType
        Set-CellText $main $row 27 (Get-AuthenticityNote $revision)
        Set-CellText $main $row 28 $revision.Url

        if ($uniqueRows.ContainsKey($revision.English)) {
            $target = [int]$uniqueRows[$revision.English]
            Set-CellText $uniqueCheck $target 2 $revision.NewName
            Set-CellText $uniqueCheck $target 3 $revision.Basis
        }
        if ($eraRows.ContainsKey($revision.English)) {
            $target = [int]$eraRows[$revision.English]
            Set-CellText $eraCheck $target 4 $revision.NewName
            Set-CellText $eraCheck $target 5 $revision.Basis
            Set-CellText $eraCheck $target 7 $revision.Usage
            Set-CellText $eraCheck $target 8 $revision.CatalogStatus
        }
        if ($sharinganRows.ContainsKey($revision.English)) {
            $target = [int]$sharinganRows[$revision.English]
            Set-CellText $sharinganCheck $target 2 $revision.NewName
        }
        if ($ruleRows.ContainsKey($revision.English)) {
            $target = [int]$ruleRows[$revision.English]
            Set-CellText $namingRules $target 2 $revision.NewName
            Set-CellText $namingRules $target 4 $revision.Basis
        }
    }

    $namingRules.Rows.Item(10).Resize(3).Insert()
    $namingRules.Range('A9:F9').Copy()
    $namingRules.Range('A10:F12').PasteSpecial(-4122)
    $excel.CutCopyMode = $false
    Set-CellText $namingRules 10 1 '动作真实性'
    Set-CellText $namingRules 10 2 '先验证佐助是否做过该动作；不能因原卡名含拳、铠、盾、熔岩等词就直接造术。'
    $namingRules.Range('B10:F10').Merge()
    Set-CellText $namingRules 11 1 '衍生边界'
    Set-CellText $namingRules 11 2 '允许描述实际动作的方向、次数与用途，但必须标为描述性动作，不冒充漫画正式忍术名。'
    $namingRules.Range('B11:F11').Merge()
    Set-CellText $namingRules 12 1 '原牌语义'
    Set-CellText $namingRules 12 2 '只继承伤害、格挡、抽牌等机制关系；若战士动作与佐助不符，动作名称必须让位于佐助真实表现。'
    $namingRules.Range('B12:F12').Merge()

    $webRows = Get-WorksheetRowMap -Worksheet $webReview -KeyColumn 1 -StartRow 4
    $shadowCloneKey = '手里剑影分身之术'
    if ($webRows.ContainsKey($shadowCloneKey)) {
        $row = [int]$webRows[$shadowCloneKey]
        Set-CellText $webReview $row 2 '手游版本确认纳入'
        Set-CellText $webReview $row 3 '官方手游“兄弟之战”技能'
        Set-CellText $webReview $row 4 '漫画用户列表未确认佐助；但官方手游兄弟之战版本明确展示佐助以机关手里剑发动该术。'
        Set-CellText $webReview $row 5 '仅按官方手游版本纳入，不扩张为漫画时期通用能力'
        Set-CellText $webReview $row 6 'Anger'
        Set-CellText $webReview $row 7 'https://www.taptap.cn/moment/822559668973863702?group_id=1066'
        Set-CellText $webReview $row 8 '版本边界必须保留：这是手游兄弟之战动作依据，不宣称漫画正史中佐助正式使用。'
    }

    $auditName = [string]::Concat([char[]](0x52A8,0x4F5C,0x771F,0x5B9E,0x6027,0x590D,0x6838))
    for ($index = $workbook.Worksheets.Count; $index -ge 1; $index--) {
        if ([string]$workbook.Worksheets.Item($index).Name -eq $auditName) {
            $workbook.Worksheets.Item($index).Delete()
        }
    }
    $audit = $workbook.Worksheets.Add()
    $audit.Name = $auditName
    $audit.Move($null, $workbook.Worksheets.Item($workbook.Worksheets.Count))
    $headers = @('卡牌英文名', '旧面板名', '新面板名', '真实性依据', '命名类型', '修改理由', '证据URL')
    for ($column = 1; $column -le $headers.Count; $column++) {
        Set-CellText $audit 1 $column $headers[$column - 1]
    }
    $audit.Range('A1:G1').Font.Bold = $true
    $audit.Range('A1:G1').Interior.Color = 0xD9EAF7
    $audit.Range('A1:G1').AutoFilter() | Out-Null
    $audit.Application.ActiveWindow.SplitRow = 1
    $audit.Application.ActiveWindow.FreezePanes = $true

    $auditRow = 2
    foreach ($revision in $revisions) {
        Set-CellText $audit $auditRow 1 $revision.English
        Set-CellText $audit $auditRow 2 $revision.OldName
        Set-CellText $audit $auditRow 3 $revision.NewName
        Set-CellText $audit $auditRow 4 $revision.Basis
        Set-CellText $audit $auditRow 5 $revision.NamingSource
        Set-CellText $audit $auditRow 6 $revision.Reason
        Set-CellText $audit $auditRow 7 $revision.Url
        $auditRow++
    }
    $audit.Columns.Item(1).ColumnWidth = 22
    $audit.Columns.Item(2).ColumnWidth = 24
    $audit.Columns.Item(3).ColumnWidth = 24
    $audit.Columns.Item(4).ColumnWidth = 32
    $audit.Columns.Item(5).ColumnWidth = 24
    $audit.Columns.Item(6).ColumnWidth = 70
    $audit.Columns.Item(7).ColumnWidth = 55
    $audit.Range(('A1:G{0}' -f ($auditRow - 1))).WrapText = $true
    $audit.Range(('A1:G{0}' -f ($auditRow - 1))).VerticalAlignment = -4160

    foreach ($propertyName in @('Author', 'Last Author', 'Company', 'Manager')) {
        try {
            $workbook.BuiltinDocumentProperties.Item($propertyName).Value = ''
        } catch {
        }
    }

    $workbook.ForceFullCalculation = $true
    $workbook.Calculate()
    $workbook.SaveAs($tempWorkbook, $xlOpenXMLWorkbook)
    $workbook.Close($true)
    [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($workbook)
    $workbook = $null
    $excel.Quit()
    [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($excel)
    $excel = $null

    Copy-Item -LiteralPath $tempWorkbook -Destination $resolvedWorkbook -Force
    Unblock-File -LiteralPath $resolvedWorkbook
} finally {
    if ($null -ne $workbook) {
        try { $workbook.Close($false) } catch {}
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($workbook)
    }
    if ($null -ne $excel) {
        try { $excel.Quit() } catch {}
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($excel)
    }
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    if (Test-Path -LiteralPath $tempWorkbook) {
        Remove-Item -LiteralPath $tempWorkbook
    }
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot
    }
}

Write-Output ('Updated {0} card names in {1}.' -f $revisions.Count, $resolvedWorkbook)
