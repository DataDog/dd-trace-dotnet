$ErrorActionPreference = 'Stop'

$service = Get-Service -Name MSMQ -ErrorAction Stop
if ($service.Status -ne 'Running') {
    Write-Output 'Starting the MSMQ service'
    Start-Service -Name MSMQ
    $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
}

Write-Output 'MSMQ initialized'
