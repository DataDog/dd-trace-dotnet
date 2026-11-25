$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

try {
    if ([string]::IsNullOrWhiteSpace($env:DD_LOGGER_DD_API_KEY)) {
        if ([string]::IsNullOrWhiteSpace($env:DD_STS_OIDC_TOKEN)) {
            throw 'CI Visibility credentials are required: DD_STS_OIDC_TOKEN is unavailable'
        }

        $oidcToken = $env:DD_STS_OIDC_TOKEN
        Remove-Item Env:DD_STS_OIDC_TOKEN -ErrorAction SilentlyContinue
        try {
            $response = Invoke-RestMethod -Method Get -TimeoutSec 60 `
                -Uri 'https://dd-sts.us1.ddbuild.io/sts/datadog/exchange?policy=apm-sdks-api-key' `
                -Headers @{ Authorization = "Bearer $oidcToken" }
        }
        catch {
            throw 'CI Visibility credential exchange failed'
        }

        if ($response.api_key -isnot [string] -or [string]::IsNullOrWhiteSpace($response.api_key)) {
            throw 'CI Visibility credential exchange returned no valid API key'
        }

        $env:DD_LOGGER_DD_API_KEY = $response.api_key
        Write-Output 'CI Visibility API key configured using dd-sts'
    }
}
finally {
    Remove-Item Env:DD_STS_OIDC_TOKEN -ErrorAction SilentlyContinue
    $oidcToken = $null
    $response = $null
}

# Keep failure, skip, retry and hang diagnostics without routine per-test progress.
$env:DD_TEST_LOG_ROUTINE_EVENTS = 'false'
& C:\entrypoint.bat RunManagedUnitTests --framework $env:FRAMEWORK `
    --RunManagedUnitTestsFromArtifacts true --NugetPackageDirectory C:\mnt\packages
exit $LASTEXITCODE
