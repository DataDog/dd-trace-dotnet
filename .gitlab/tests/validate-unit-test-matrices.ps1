param (
    [string]$BuildAssembly = 'tracer/build/_build/bin/Debug/_build.dll'
)

$ErrorActionPreference = 'Stop'
$BuildAssembly = (Resolve-Path $BuildAssembly).Path
$validationDirectory = Join-Path ([IO.Path]::GetTempPath()) ("gitlab-unit-matrices-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $validationDirectory | Out-Null
$environmentNames = @('CI_JOB_ID', 'CI_COMMIT_BRANCH', 'CI_MERGE_REQUEST_SOURCE_BRANCH_NAME',
    'SYSTEM_PULLREQUEST_SOURCEBRANCH', 'BUILD_SOURCEBRANCH', 'BUILD_SOURCEBRANCHNAME', 'NUKE_TELEMETRY_OPTOUT')
$savedEnvironment = @{}
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
    [Environment]::SetEnvironmentVariable($name, $null)
}

try {
    $env:CI_JOB_ID = 'matrix-validation'
    $env:NUKE_TELEMETRY_OPTOUT = '1'
    $cases = @(
        @{ Name = 'ordinary'; Branch = 'feature/unit-shadow'; Files = @(); All = $false; Full = $false },
        @{ Name = 'mainline'; Branch = 'master'; Files = @(); All = $false; Full = $true },
        @{ Name = 'forced'; Branch = 'feature/unit-shadow'; Files = @(); All = $true; Full = $true },
        @{ Name = 'integration'; Branch = 'feature/unit-shadow'; Files = @('tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Test.cs'); All = $false; Full = $true },
        @{ Name = 'aspects'; Branch = 'feature/unit-shadow'; Files = @('tracer/src/Datadog.Trace/Iast/Aspects/Test.cs'); All = $false; Full = $true },
        @{ Name = '100-snapshots'; Branch = 'feature/unit-shadow'; Files = @(1..100 | ForEach-Object { "snapshots/test$_.verified.txt" }); All = $false; Full = $false },
        @{ Name = '101-snapshots'; Branch = 'feature/unit-shadow'; Files = @(1..101 | ForEach-Object { "snapshots/test$_.verified.txt" }); All = $false; Full = $true }
    )
    foreach ($case in $cases) {
        $env:CI_COMMIT_BRANCH = $case.Branch
        $inputPath = Join-Path $validationDirectory "$($case.Name).json"
        $outputDirectory = Join-Path $validationDirectory $case.Name
        [IO.File]::WriteAllText($inputPath, (ConvertTo-Json -InputObject @($case.Files)))
        $output = & dotnet $BuildAssembly GenerateGitlabUnitTestsPipelines `
            --GitlabChangedFiles $inputPath --GitlabUnitTestPipelineDirectory $outputDirectory `
            --TargetBranch master --IncludeAllTestFrameworks $case.All 2>&1
        if ($LASTEXITCODE -ne 0) { throw ($output -join "`n") }

        $expected = if ($case.Full) { @{ windows = 10; linux = 18; 'linux-arm64' = 14 } } else { @{ windows = 5; linux = 8; 'linux-arm64' = 10 } }
        foreach ($platform in $expected.Keys) {
            $yaml = Get-Content (Join-Path $outputDirectory "$platform-unit-tests.yml") -Raw
            $jobs = [regex]::Matches($yaml, '(?m)^"unit-tests-[^"]+":\r?$').Count
            if ($jobs -ne $expected[$platform]) {
                throw "$($case.Name)/${platform}: expected $($expected[$platform]) jobs, got $jobs"
            }
            if ($yaml -match 'integration|macos|allow_failure') { throw 'Unexpected scope or optional child job' }
            if ($platform -eq 'linux-arm64' -and $yaml -match 'netcoreapp') { throw 'Unsupported ARM64 runtime' }
        }
        Write-Output "PASS: $($case.Name)"
    }
    Write-Output "Generated validation artifacts: $validationDirectory"
}
finally {
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name])
    }
}
