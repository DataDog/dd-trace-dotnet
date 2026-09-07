$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data

$localDb = Get-Command 'SqlLocalDB.exe' -ErrorAction Stop
Write-Output 'Starting the MSSQLLocalDB instance'
& $localDb.Source start MSSQLLocalDB
if ($LASTEXITCODE -ne 0) {
    throw "SqlLocalDB.exe exited with code $LASTEXITCODE while starting MSSQLLocalDB."
}

$scriptPath = Join-Path $PSScriptRoot '..\.azure-pipelines\prepare_localdb.sql'
$script = Get-Content -LiteralPath $scriptPath -Raw
$batches = [regex]::Split($script, '(?im)^\s*GO\s*(?:--.*)?$')

$connection = New-Object System.Data.SqlClient.SqlConnection 'Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Connection Timeout=60;Initial Catalog=master'
try {
    $connection.Open()
    foreach ($batch in $batches) {
        if ([string]::IsNullOrWhiteSpace($batch)) {
            continue
        }

        $command = $connection.CreateCommand()
        try {
            $command.CommandText = $batch
            $command.CommandTimeout = 60
            [void]$command.ExecuteNonQuery()
        }
        finally {
            $command.Dispose()
        }
    }
}
finally {
    $connection.Dispose()
}

Write-Output 'MSSQLLocalDB initialized'
