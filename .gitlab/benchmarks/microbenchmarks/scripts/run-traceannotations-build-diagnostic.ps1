param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactsIndex,

    [Parameter(Mandatory = $true)]
    [string]$Label
)

$ErrorActionPreference = "Stop"

foreach ($name in @("CODE_SRC", "BASELINE_OR_CANDIDATE", "ARTIFACTS_DIR")) {
    if (-not [Environment]::GetEnvironmentVariable($name)) {
        throw "Missing required environment variable: $name"
    }
}

$project = "Benchmarks.Trace"
$filter = "*TraceAnnotationsBenchmark.RunOnMethodBegin*"
$tracerRoot = "$env:CODE_SRC\tracer"
$sourceBinDir = "$env:CODE_SRC\artifacts\bin\$project\release_net6.0"
$runDir = "$tracerRoot\benchmarks-run\$ArtifactsIndex"
$outputDir = "$env:ARTIFACTS_DIR\diagnostics-$ArtifactsIndex"
$benchmarkArtifactsDir = "$outputDir\benchmarkdotnet"
$dumpDir = "$outputDir\dumps"
$stopFile = "$outputDir\stop-sampling"

New-Item -ItemType Directory -Path $outputDir, $benchmarkArtifactsDir, $dumpDir -Force | Out-Null

if (-not (Test-Path "$sourceBinDir\$project.exe")) {
    throw "Benchmark executable not found at $sourceBinDir\$project.exe"
}

if (Test-Path $runDir) {
    Remove-Item -Recurse -Force $runDir
}
Copy-Item -Path $sourceBinDir -Destination $runDir -Recurse -Force

$environmentBackup = @{}
foreach ($name in @("DOTNET_ROOT", "PATH", "DD_SERVICE", "DD_ENV", "DD_DOTNET_TRACER_HOME", "DD_TRACER_HOME",
    "DOTNET_DbgEnableMiniDump", "DOTNET_DbgMiniDumpType", "DOTNET_DbgMiniDumpName",
    "COMPlus_DbgEnableMiniDump", "COMPlus_DbgMiniDumpType", "COMPlus_DbgMiniDumpName", "BDN_DIAGNOSTIC_GENERATION_MARKER")) {
    $environmentBackup[$name] = [Environment]::GetEnvironmentVariable($name, [EnvironmentVariableTarget]::Process)
}

[Environment]::SetEnvironmentVariable("DOTNET_ROOT", "C:\dotnet", [EnvironmentVariableTarget]::Process)
[Environment]::SetEnvironmentVariable("PATH", "C:\dotnet;" + [Environment]::GetEnvironmentVariable("PATH", [EnvironmentVariableTarget]::Process), [EnvironmentVariableTarget]::Process)

$env:DD_SERVICE = "dd-trace-dotnet"
$env:DD_ENV = "CI"
$env:DD_DOTNET_TRACER_HOME = "$tracerRoot\bin\monitoring-home"
$env:DD_TRACER_HOME = $env:DD_DOTNET_TRACER_HOME
$env:DOTNET_DbgEnableMiniDump = "1"
$env:DOTNET_DbgMiniDumpType = "4"
$env:DOTNET_DbgMiniDumpName = "$dumpDir\coreclr-%p.dmp"
$env:COMPlus_DbgEnableMiniDump = $env:DOTNET_DbgEnableMiniDump
$env:COMPlus_DbgMiniDumpType = $env:DOTNET_DbgMiniDumpType
$env:COMPlus_DbgMiniDumpName = $env:DOTNET_DbgMiniDumpName

$localDumpsKey = "HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps"
$localDumpsRootExisted = Test-Path $localDumpsKey
$localDumpsBackup = @{}
$sampler = $null
try {
    foreach ($processName in @("csc.exe", "dotnet.exe", "MSBuild.exe", "VBCSCompiler.exe")) {
        $processKey = "$localDumpsKey\$processName"
        $existingKey = Get-Item -Path $processKey -ErrorAction SilentlyContinue
        $backup = [ordered]@{
            key_existed = $null -ne $existingKey
            properties = @{}
        }

        if ($existingKey) {
            foreach ($propertyName in @("DumpFolder", "DumpType", "DumpCount")) {
                if ($existingKey.GetValueNames() -contains $propertyName) {
                    $backup.properties[$propertyName] = @{
                        value = $existingKey.GetValue($propertyName, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
                        kind = $existingKey.GetValueKind($propertyName)
                    }
                }
            }
        }

        $localDumpsBackup[$processName] = $backup
        New-Item -Path $processKey -Force | Out-Null
        New-ItemProperty -Path $processKey -Name DumpFolder -PropertyType ExpandString -Value $dumpDir -Force | Out-Null
        New-ItemProperty -Path $processKey -Name DumpType -PropertyType DWord -Value 2 -Force | Out-Null
        New-ItemProperty -Path $processKey -Name DumpCount -PropertyType DWord -Value 1 -Force | Out-Null
    }

    $startedAt = (Get-Date).ToUniversalTime()
    $context = [ordered]@{
        label = $Label
        run_mode = "isolated-single-method"
        started_at_utc = $startedAt.ToString("o")
        process_id = $PID
        processor_affinity = (Get-Process -Id $PID).ProcessorAffinity.ToInt64()
        requested_cpus = $env:PARALLEL_CPUS
        dotnet_info = (& dotnet --info 2>&1 | Out-String)
        operating_system = Get-CimInstance Win32_OperatingSystem |
            Select-Object Caption, Version, BuildNumber, FreePhysicalMemory, FreeVirtualMemory, TotalVirtualMemorySize
        computer_system = Get-CimInstance Win32_ComputerSystem |
            Select-Object TotalPhysicalMemory, NumberOfLogicalProcessors, NumberOfProcessors
        processors = @(Get-CimInstance Win32_Processor |
            Select-Object DeviceID, Name, NumberOfCores, NumberOfLogicalProcessors, ProcessorId)
        page_files = @(Get-CimInstance Win32_PageFileUsage |
            Select-Object Name, AllocatedBaseSize, CurrentUsage, PeakUsage)
    }
    $context | ConvertTo-Json -Depth 5 | Set-Content "$outputDir\context.json"

    $samplesPath = "$outputDir\samples.jsonl"
    $sampler = Start-Job -ArgumentList $samplesPath, $stopFile -ScriptBlock {
        param($OutputPath, $StopPath)

        $seenProcessIds = @{}
        while (-not (Test-Path $StopPath)) {
            $sample = [ordered]@{
                timestamp_utc = (Get-Date).ToUniversalTime().ToString("o")
                operating_system = $null
                counters = $null
                processes = @()
                error = $null
            }

            try {
                $cimProcesses = @{}
                Get-CimInstance Win32_Process -ErrorAction Stop |
                    ForEach-Object { $cimProcesses[[int]$_.ProcessId] = $_ }

                $sample.operating_system = Get-CimInstance Win32_OperatingSystem |
                    Select-Object FreePhysicalMemory, FreeVirtualMemory, TotalVirtualMemorySize

                $sample.processes = @(Get-Process -ErrorAction SilentlyContinue |
                    Where-Object { $_.ProcessName -match "^(csc|dotnet|msbuild|vbcscompiler|Benchmarks.Trace)$" } |
                    ForEach-Object {
                        $affinity = $null
                        $modules = $null
                        try {
                            $affinity = $_.ProcessorAffinity.ToInt64()
                        } catch {
                            $affinity = "unavailable"
                        }
                        if (-not $seenProcessIds.ContainsKey($_.Id)) {
                            try {
                                $modules = @($_.Modules |
                                    Select-Object ModuleName, FileName, @{ Name = "FileVersion"; Expression = { $_.FileVersionInfo.FileVersion } })
                            } catch {
                                $modules = @("unavailable")
                            }
                            $seenProcessIds[$_.Id] = $true
                        }

                        [ordered]@{
                            id = $_.Id
                            name = $_.ProcessName
                            parent_process_id = $cimProcesses[$_.Id].ParentProcessId
                            path = $cimProcesses[$_.Id].ExecutablePath
                            processor_affinity = $affinity
                            private_bytes = $_.PrivateMemorySize64
                            virtual_bytes = $_.VirtualMemorySize64
                            working_set_bytes = $_.WorkingSet64
                            modules = $modules
                        }
                    })

                try {
                    $counterValues = [ordered]@{}
                    $counterSet = Get-Counter -Counter @(
                        "\Memory\Committed Bytes",
                        "\Memory\Commit Limit",
                        "\Memory\Available Bytes",
                        "\Paging File(_Total)\% Usage"
                    ) -ErrorAction Stop
                    foreach ($counter in $counterSet.CounterSamples) {
                        $counterValues[$counter.Path] = $counter.CookedValue
                    }
                    $sample.counters = $counterValues
                } catch {
                    $sample.error = $_.Exception.Message
                }
            } catch {
                $sample.error = $_.Exception.Message
            }

            $sample | ConvertTo-Json -Compress -Depth 5 | Add-Content $OutputPath
            Start-Sleep -Milliseconds 500
        }
    }

    $arguments = @(
        "-r", "netcoreapp3.1",
        "-m",
        "-f", $filter,
        "--allCategories", "prs",
        "--iterationTime", "200",
        "--launchCount", "5",
        "--warmupCount", "10",
        "--iterationCount", "10",
        "--buildTimeout", "3600",
        "--keepFiles",
        "--artifacts", $benchmarkArtifactsDir
    )

    $exitCode = 1
    $attempts = @()
    try {
        $shimDir = "$tracerRoot\diagnostic-generation-cli"
        $shimPath = "$shimDir\dotnet.exe"
        $generationMarker = "$outputDir\generation-stopped.txt"
        if (-not (Test-Path $shimPath)) { throw "Prepared generation CLI was missing" }
        $env:BDN_DIAGNOSTIC_GENERATION_MARKER = $generationMarker
        $generationStarted = Get-Date
        $originalPath = $env:PATH
        $env:PATH = "$shimDir;$originalPath"
        Push-Location $runDir
        try {
            & "$runDir\$project.exe" @arguments 2>&1 |
                Tee-Object -FilePath "$outputDir\generation.log"
        } finally {
            $env:PATH = $originalPath
            Pop-Location
        }
        if (-not (Test-Path $generationMarker) -or
            (Get-Content $generationMarker -Raw).Trim() -ne "restore" -or
            (Select-String -Path "$outputDir\generation.log" -Pattern "// Execute:" -Quiet)) {
            throw "Generation did not stop before compilation and benchmark execution"
        }
        $generatedProjects = @(Get-ChildItem $env:CODE_SRC -Filter "BenchmarkDotNet.Autogenerated.csproj" -Recurse |
            Where-Object { $_.LastWriteTime -ge $generationStarted })
        if ($generatedProjects.Count -ne 1) {
            throw "Expected one newly generated netcoreapp3.1 project; found $($generatedProjects.Count)"
        }
        $generatedDir = $generatedProjects[0].Directory.FullName
        $buildScripts = @(Get-ChildItem $generatedDir -Filter "*.bat")
        if ($buildScripts.Count -ne 1 -or -not (Get-ChildItem $generatedDir -Filter "*.notcs")) {
            throw "Generated build script or source was missing or ambiguous"
        }
        $originalBuildScript = Get-Content $buildScripts[0].FullName -Raw
        if ($originalBuildScript.Contains($shimPath) -or $originalBuildScript -notmatch "call dotnet restore") {
            throw "Generated script did not use the default CLI"
        }
        $buildLines = @($originalBuildScript -split "`r?`n" | Where-Object { $_.Trim() })
        if ($buildLines.Count -ne 2 -or $buildLines[0] -notmatch " restore " -or
            $buildLines[1] -notmatch " build .*--no-restore" -or
            $originalBuildScript -notmatch "netcoreapp3.1") {
            throw "Unexpected generated restore/build command shape"
        }
        $replayScript = "$generatedDir\diagnostic-build.bat"
        Set-Content "$outputDir\generated-build-original.bat" $originalBuildScript
        Set-Content $replayScript ($buildLines[0] + "`r`nif errorlevel 1 exit /b %errorlevel%`r`n" +
            $buildLines[1] + " /v:normal`r`nexit /b %errorlevel%")
        $dumpCaptured = $false
        for ($attempt = 1; $attempt -le 10; $attempt++) {
            Get-ChildItem $generatedDir -Directory | Remove-Item -Recurse -Force
            $buildStarted = (Get-Date).ToUniversalTime()
            $buildLog = "$outputDir\build-$attempt.log"
            $startInfo = New-Object System.Diagnostics.ProcessStartInfo
            $startInfo.FileName = $env:ComSpec
            $startInfo.Arguments = '/d /c "' + $replayScript + '"'
            $startInfo.WorkingDirectory = $generatedDir
            $startInfo.UseShellExecute = $false
            $startInfo.RedirectStandardOutput = $true
            $startInfo.RedirectStandardError = $true
            $buildProcess = [System.Diagnostics.Process]::Start($startInfo)
            try {
                $stdout = $buildProcess.StandardOutput.ReadToEndAsync()
                $stderr = $buildProcess.StandardError.ReadToEndAsync()
                $buildProcess.WaitForExit()
                $buildExitCode = $buildProcess.ExitCode
                $buildOutput = $stdout.Result + $stderr.Result
                Set-Content $buildLog $buildOutput
                Write-Output $buildOutput
            } finally {
                $buildProcess.Dispose()
            }
            $compiledTracer = Select-String -Path $buildLog -Pattern "csc(?:\.exe|\.dll)?.*Datadog\.Trace" -Quiet
            $result = [ordered]@{
                attempt = $attempt
                exit_code = $buildExitCode
                started_at_utc = $buildStarted.ToString("o")
                finished_at_utc = (Get-Date).ToUniversalTime().ToString("o")
                processor_affinity = (Get-Process -Id $PID).ProcessorAffinity.ToInt64()
                tracer_compiler_invoked = [bool]$compiledTracer
            }
            $attempts += [pscustomobject]$result
            $result | ConvertTo-Json -Compress | Add-Content "$outputDir\build-attempts.jsonl"
            if (-not $compiledTracer) {
                throw "Attempt $attempt did not invoke the tracer compiler; it is not a valid cold build"
            }
            if ($buildExitCode -ne 0 -and -not $dumpCaptured) {
                $dumpCaptured = $true
                $env:DOTNET_DbgEnableMiniDump = "0"
                $env:COMPlus_DbgEnableMiniDump = "0"
            }
        }
        $exitCode = if (@($attempts | Where-Object { $_.exit_code -ne 0 }).Count) { 1 } else { 0 }
    } finally {
        New-Item -ItemType File -Path $stopFile -Force | Out-Null
        Wait-Job $sampler -Timeout 10 | Out-Null
        Stop-Job $sampler -ErrorAction SilentlyContinue
        Receive-Job $sampler -ErrorAction SilentlyContinue |
            Out-String |
            Set-Content "$outputDir\sampler.log"
        Remove-Job $sampler -Force -ErrorAction SilentlyContinue

        $finishedAt = (Get-Date).ToUniversalTime()
        [ordered]@{
            label = $Label
            exit_code = $exitCode
            cold_build_attempts = $attempts
            started_at_utc = $startedAt.ToString("o")
            finished_at_utc = $finishedAt.ToString("o")
            processor_affinity = (Get-Process -Id $PID).ProcessorAffinity.ToInt64()
        } | ConvertTo-Json -Depth 5 | Set-Content "$outputDir\result.json"

        foreach ($logName in @("Application", "System")) {
            $events = @(Get-WinEvent -FilterHashtable @{ LogName = $logName; StartTime = $startedAt.ToLocalTime() } -ErrorAction SilentlyContinue |
                Where-Object {
                    $_.ProviderName -match "Application Error|\.NET Runtime|Windows Error Reporting|WHEA" -or
                    $_.Message -match "csc|dotnet|MSBuild|VBCSCompiler"
                } |
                Select-Object TimeCreated, LogName, ProviderName, Id, LevelDisplayName, Message)
            ConvertTo-Json -InputObject $events -Depth 4 |
                Set-Content "$outputDir\events-$($logName.ToLowerInvariant()).json"
        }

        $cdb = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\Debuggers\x64\cdb.exe" -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($cdb) {
            foreach ($dump in Get-ChildItem $dumpDir -Filter "*.dmp" -ErrorAction SilentlyContinue) {
                & $cdb.FullName -z $dump.FullName -c "!analyze -v; k; lm; q" 2>&1 |
                    Set-Content "$outputDir\$($dump.BaseName)-analysis.txt"
            }
        } else {
            Set-Content "$outputDir\debugger.txt" "cdb.exe was not available; the raw dump is retained."
        }

        $samples = @(Get-Content $samplesPath -ErrorAction SilentlyContinue |
            ForEach-Object { $_ | ConvertFrom-Json })
        $processSummary = @($samples.processes |
            Where-Object { $null -ne $_ } |
            Select-Object id, name, parent_process_id, path, processor_affinity -Unique |
            Sort-Object name, id)
        ConvertTo-Json -InputObject $processSummary -Depth 4 |
            Set-Content "$outputDir\process-summary.json"

        $moduleSummary = @($samples.processes |
            Where-Object { $null -ne $_.modules } |
            ForEach-Object {
                $process = $_
                $_.modules | ForEach-Object {
                    [pscustomobject]@{
                        process_id = $process.id
                        process_name = $process.name
                        module_name = $_.ModuleName
                        file_name = $_.FileName
                        file_version = $_.FileVersion
                    }
                }
            } |
            Select-Object process_id, process_name, module_name, file_name, file_version -Unique)
        ConvertTo-Json -InputObject $moduleSummary -Depth 4 |
            Set-Content "$outputDir\module-summary.json"

        Write-Output "=== Diagnostic summary: $Label ==="
        Get-Content "$outputDir\result.json"
        Write-Output "=== Effective process affinity ==="
        Get-Content "$outputDir\process-summary.json"
        Write-Output "=== Compiler and runtime modules ==="
        $moduleSummary |
            Where-Object { $_.module_name -match "csc|coreclr|clr|hostfxr|hostpolicy|msbuild|roslyn" } |
            ConvertTo-Json -Depth 4 |
            Write-Output
        Get-ChildItem $dumpDir -Filter "*.dmp" -ErrorAction SilentlyContinue |
            Select-Object Name, Length, LastWriteTimeUtc |
            Format-Table -AutoSize |
            Out-String |
            Write-Output
        foreach ($eventFile in Get-ChildItem $outputDir -Filter "events-*.json" -ErrorAction SilentlyContinue) {
            Write-Output "=== $($eventFile.Name) ==="
            Get-Content $eventFile.FullName
        }
        foreach ($analysisFile in Get-ChildItem $outputDir -Filter "*-analysis.txt" -ErrorAction SilentlyContinue) {
            Write-Output "=== $($analysisFile.Name) ==="
            Get-Content $analysisFile.FullName
        }
    }

    Write-Output "Diagnostic $Label completed with benchmark exit code $exitCode"

} finally {
    if ($sampler) {
        Stop-Job $sampler -ErrorAction SilentlyContinue
        Remove-Job $sampler -Force -ErrorAction SilentlyContinue
    }
    foreach ($name in $environmentBackup.Keys) {
        [Environment]::SetEnvironmentVariable($name, $environmentBackup[$name], [EnvironmentVariableTarget]::Process)
    }
    $restoreErrors = @()
    foreach ($processName in $localDumpsBackup.Keys) {
        try {
            $processKey = "$localDumpsKey\$processName"
            $backup = $localDumpsBackup[$processName]
            if (-not $backup.key_existed) {
                if (Test-Path $processKey) { Remove-Item -Path $processKey -Recurse -Force -ErrorAction Stop }
                continue
            }
            foreach ($propertyName in @("DumpFolder", "DumpType", "DumpCount")) {
                if ($backup.properties.ContainsKey($propertyName)) {
                    $property = $backup.properties[$propertyName]
                    New-ItemProperty -Path $processKey -Name $propertyName -PropertyType $property.kind -Value $property.value -Force | Out-Null
                } else {
                    $key = Get-Item -Path $processKey
                    if ($key.GetValueNames() -contains $propertyName) {
                        Remove-ItemProperty -Path $processKey -Name $propertyName -ErrorAction Stop
                    }
                }
            }
        } catch {
            $restoreErrors += $_.Exception.Message
        }
    }
    if (-not $localDumpsRootExisted -and (Test-Path $localDumpsKey)) {
        try {
            $rootKey = Get-Item $localDumpsKey
            if ($rootKey.GetSubKeyNames().Count -eq 0 -and $rootKey.GetValueNames().Count -eq 0) {
                Remove-Item $localDumpsKey -Force -ErrorAction Stop
            }
        } catch {
            $restoreErrors += $_.Exception.Message
        }
    }
    if ($restoreErrors.Count -gt 0) {
        throw "WER restoration failed: $($restoreErrors -join '; ')"
    }
}
