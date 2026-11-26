$ErrorActionPreference = "Stop"

if (-not $env:BENCHMARKING_PLATFORM_DIR) {
    $env:BENCHMARKING_PLATFORM_DIR = "C:\benchmarking-platform"
}
$ddTraceDotNetDir = "$env:BENCHMARKING_PLATFORM_DIR\dd-trace-dotnet"
$buildArtifactsDir = "$ddTraceDotNetDir\build-artifacts"
$tracerHomeWinDir = "$ddTraceDotNetDir\tracer\tracer-home-win"

function Clone-DotNetRepo {
    if ($env:CI_PROJECT_NAME -match "dd-trace-dotnet") {
      $branch = $env:CI_COMMIT_REF_NAME
    } else {
      $branch = "master"
      Write-Output "Not running from dd-trace-dotnet's CI. Setting cloned dd-trace-dotnet branch to '$branch'..."
    }

    git clone https://github.com/DataDog/dd-trace-dotnet.git $ddTraceDotNetDir
    Set-Location $ddTraceDotNetDir
    git checkout $branch

    Write-Output "Preparing Samples.AspNetCoreSimpleController to use .NET 9..."
    (Get-Content './tracer/test/test-applications/throughput/Samples.AspNetCoreSimpleController/Samples.AspNetCoreSimpleController.csproj' -Raw) -replace '<TargetFramework>net5.0</TargetFramework>', '<TargetFramework>net9.0</TargetFramework>' | Set-Content './tracer/test/test-applications/throughput/Samples.AspNetCoreSimpleController/Samples.AspNetCoreSimpleController.csproj'
}

function Clone-DotNetBuildArtifacts {
    if (-not $env:buildId) {
        Write-Error "The 'buildId' environment variable is not set. dd-trace-dotnet build artifacts cannot be downloaded."
    }

    $ProgressPreference = "SilentlyContinue" # Makes Invoke-WebRequest faster

    $personalAccessToken = "1"
    $artifactNames = @(
        "windows-tracer-home",
        "Datadog.Trace.Manual.dll"
    )

    New-Item -ItemType Directory -Path $buildArtifactsDir -Force | Out-Null

    foreach ($artifactName in $artifactNames) {
        $artifactsUri = "https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds/$env:buildId/artifacts?artifactName=$artifactName&api-version=4.1"
        $artifactData = Invoke-RestMethod -Uri $artifactsUri -Headers @{Authorization = "Basic "+[Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$personalAccessToken"))}
        $downloadUrl = $artifactData.resource.downloadUrl
        Invoke-WebRequest -Uri $downloadUrl -OutFile "$buildArtifactsDir/$artifactName.zip"
        Expand-Archive -Path "$buildArtifactsDir/$artifactName.zip" -DestinationPath "$buildArtifactsDir/$artifactName" -Force
    }

    Move-Item -Path "$buildArtifactsDir\windows-tracer-home\*" -Destination "$tracerHomeWinDir" -Force

    New-Item -ItemType Directory -Path "$ddTraceDotNetDir\tracer\bin\netcoreapp3.1" -Force | Out-Null

    # Copy and move DLLs
    Copy-Item `
        -Recurse `
        -Path "$buildArtifactsDir\Datadog.Trace.Manual.dll\Datadog.Trace.Manual.dll\*" `
        -Destination "$ddTraceDotNetDir\tracer\test\test-applications\throughput\Samples.AspNetCoreSimpleController"

    Copy-Item `
        -Recurse `
        -Path "$buildArtifactsDir\Datadog.Trace.Manual.dll\Datadog.Trace.Manual.dll\*" `
        -Destination "$ddTraceDotNetDir\tracer\bin\netcoreapp3.1"
}

function Run-Benchmarks {
    # Set environment variables
    $env:COR_PROFILER = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
    $env:CORECLR_PROFILER = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
    $env:NATIVE_PROFILER_PATH = "$tracerHomeWinDir\win-x64\Datadog.Trace.ClrProfiler.Native.dll"
    $env:TRACER_HOME_PATH = "$tracerHomeWinDir"
    $env:COR_PROFILER_PATH = "$env:NATIVE_PROFILER_PATH"
    $env:CORECLR_PROFILER_PATH = "$env:NATIVE_PROFILER_PATH"
    $env:DD_DOTNET_TRACER_HOME = "$env:TRACER_HOME_PATH"

    # Navigate to application directory and build
    [Environment]::SetEnvironmentVariable("PATH", "C:\dotnet;" + [Environment]::GetEnvironmentVariable("PATH", [EnvironmentVariableTarget]::Process), [EnvironmentVariableTarget]::Process)
    Set-Location "$ddTraceDotNetDir\tracer\test\test-applications\throughput\Samples.AspNetCoreSimpleController"
    Write-Output "Building the SUT..."
    [Environment]::SetEnvironmentVariable("PATH", "C:\dotnet;" + [Environment]::GetEnvironmentVariable("PATH", [EnvironmentVariableTarget]::Process), [EnvironmentVariableTarget]::Process)
    C:\dotnet\dotnet.exe build $DOTNET_BUILD_ARGS

    # Set benchmarking environment variables and run benchmarks
    Set-Location "$env:BENCHMARKING_PLATFORM_DIR"
    if (-not $(Test-Path ".env")) {
        if (-not $env:DD_API_KEY) {
            Write-Output "DD_API_KEY is not set, fetching from SSM Parameter Store..."
            if (-not $env:DD_API_KEY_SSM_PARAMETER) {
                Write-Error "DD_API_KEY_SSM_PARAMETER is not set. Please set it, or set DD_API_KEY directly."
                exit 1
            }
            $env:DD_API_KEY = (Get-SSMParameter -Name $env:DD_API_KEY_SSM_PARAMETER -WithDecryption $true).Value
        }
        if (-not $(env | Select-String -Pattern "K6_OPTIONS_")) {
            Write-Output "K6 options are not set. Please copy .env.example to .env and set the variables." 
            exit 1
        }
    } else {
        Write-Output ".env file found."
        Get-Content .env | foreach {
            $name, $value = $_.split('=')
            if ([string]::IsNullOrWhiteSpace($name) -or $name.Contains('#')) {
                return
            }
            Set-Content env:\$name $value
        }
    }
    
    bp-runner --debug ./bp-runner.win.yml
}

# ARTIFACTS_DIR is used by Run-Benchmarks
if (-not $env:ARTIFACTS_DIR) {
    $env:ARTIFACTS_DIR = "$env:BENCHMARKING_PLATFORM_DIR\artifacts"
}
New-Item -ItemType Directory -Path $env:ARTIFACTS_DIR -Force | Out-Null

# Skip actual benchmarking and use mock results from S3.
# Useful for testing CI pipeline infrastructure (artifact fetching, result
# processing, uploads) without waiting for real benchmarks to complete.
if ($env:BP_INFRA_TEST -eq $true) {
    Write-Output "BP_INFRA_TEST set to '$env:BP_INFRA_TEST'. Will mock benchmark results."

    Read-S3Object `
        -BucketName $env:ARTIFACTS_BUCKET_NAME `
        -Key "dd-trace-dotnet/_test/candidate-mock.converted.json" `
        -File "$env:ARTIFACTS_DIR\candidate-mock.converted.json"

    exit 0
}

Clone-DotNetRepo

Clone-DotNetBuildArtifacts

Run-Benchmarks
