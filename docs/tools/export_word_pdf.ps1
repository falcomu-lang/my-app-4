param([string]$ExportsDirectory = (Join-Path $PSScriptRoot '..\exports'))
$ErrorActionPreference = 'Stop'
$resolvedExports = (Resolve-Path -LiteralPath $ExportsDirectory).Path
$wordForDocumentation = $null
try {
    $wordForDocumentation = New-Object -ComObject Word.Application
    $wordForDocumentation.Visible = $false
    $wordForDocumentation.DisplayAlerts = 0
    foreach ($documentStem in @('USER_MANUAL', 'SYSTEM_SPECIFICATION')) {
        $documentForExport = $null
        try {
            $sourceDocumentPath = Join-Path $resolvedExports ($documentStem + '.docx')
            $outputPdfPath = Join-Path $resolvedExports ($documentStem + '.pdf')
            $documentForExport = $wordForDocumentation.Documents.Open($sourceDocumentPath, $false, $true, $false)
            $documentForExport.ExportAsFixedFormat($outputPdfPath, 17)
            Write-Output $outputPdfPath
        }
        finally {
            if ($null -ne $documentForExport) {
                $documentForExport.Close(0)
                [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($documentForExport)
            }
        }
    }
}
finally {
    if ($null -ne $wordForDocumentation) {
        $wordForDocumentation.Quit(0)
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($wordForDocumentation)
    }
}
