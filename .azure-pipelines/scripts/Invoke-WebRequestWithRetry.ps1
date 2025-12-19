<#
.SYNOPSIS
    Invokes a web request with automatic retry logic for PowerShell 5.1+
.DESCRIPTION
    This function provides retry capability for Invoke-WebRequest that works
    in PowerShell 5.1 (Windows PowerShell), which doesn't support the
    -MaximumRetryCount parameter introduced in PowerShell Core 6.0+.
.PARAMETER Uri
    The URI to download from
.PARAMETER OutFile
    The output file path
.PARAMETER MaxRetries
    Maximum number of retry attempts (default: 5)
.PARAMETER RetryIntervalSec
    Seconds to wait between retries (default: 2)
.PARAMETER TimeoutSec
    Timeout in seconds for each attempt (default: 120)
.EXAMPLE
    . .\Invoke-WebRequestWithRetry.ps1
    Invoke-WebRequestWithRetry -Uri "https://example.com/file.zip" -OutFile "file.zip"
#>

function Invoke-WebRequestWithRetry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Uri,

        [Parameter(Mandatory=$true)]
        [string]$OutFile,

        [Parameter(Mandatory=$false)]
        [int]$MaxRetries = 5,

        [Parameter(Mandatory=$false)]
        [int]$RetryIntervalSec = 2,

        [Parameter(Mandatory=$false)]
        [int]$TimeoutSec = 120
    )

    for ($i = 0; $i -le $MaxRetries; $i++) {
        try {
            Write-Host "Attempt $($i + 1) of $($MaxRetries + 1): Downloading from $Uri"
            Invoke-WebRequest -Uri $Uri -OutFile $OutFile -TimeoutSec $TimeoutSec
            Write-Host "Download successful"
            return
        } catch {
            if ($i -eq $MaxRetries) {
                Write-Host "All retry attempts failed"
                throw
            }
            Write-Host "Download failed: $($_.Exception.Message). Retrying in $RetryIntervalSec seconds..."
            Start-Sleep -Seconds $RetryIntervalSec
        }
    }
}
