ARG DOTNETSDK_VERSION
ARG RUNTIME_IMAGE

# Build the ASP.NET Core app using the latest SDK
FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION-windowsservercore-ltsc2022 as builder

# Build the smoke test app
WORKDIR /src
COPY ./test/test-applications/regression/AspNetCoreSmokeTest/ .

ARG PUBLISH_FRAMEWORK
RUN dotnet publish "AspNetCoreSmokeTest.csproj" -c Release --framework %PUBLISH_FRAMEWORK% /p:PathMap=C:\src=/src -o /src/publish

FROM $RUNTIME_IMAGE AS publish-msi
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

WORKDIR /app

ARG CHANNEL_32_BIT
RUN if($env:CHANNEL_32_BIT){ \
    echo 'Installing x86 dotnet runtime ' + $env:CHANNEL_32_BIT; \
    curl 'https://raw.githubusercontent.com/dotnet/install-scripts/2bdc7f2c6e00d60be57f552b8a8aab71512dbcb2/src/dotnet-install.ps1' -o dotnet-install.ps1; \
    ./dotnet-install.ps1 -Architecture x86 -Runtime aspnetcore -Channel $env:CHANNEL_32_BIT -InstallDir c:\cli; \
    [Environment]::SetEnvironmentVariable('Path',  'c:\cli;' + $env:Path, [EnvironmentVariableTarget]::Machine); \
    rm ./dotnet-install.ps1; }

# Copy the installer files and this build's own monitoring home (used below as the
# "expected" post-upgrade state) from
# tracer/test/test-applications/regression/AspNetCoreSmokeTest/artifacts. The MSI is
# renamed to "datadog-apm.msi" by SmokeTestRunner.Builder.cs, same as every other
# Windows MSI scenario.
COPY --from=builder /src/artifacts /install

# Regression test for the MSI in-place-upgrade DLL-staleness bug (a stale
# libdatadog v20.0.0 datadog_profiling_ffi.dll survived an upgrade to a tracer
# shipping libdatadog v25.0.0 because Windows Installer's unversioned-file
# heuristic decided the file "looked user-modified" and skipped it, causing a
# fatal AV on next startup): install a pinned previous release first to create a
# genuinely aged on-disk state, then upgrade in place to the locally-built MSI
# under test.
ARG PREVIOUS_RELEASE_VERSION
RUN mkdir /logs; \
    cd /install; \
    echo "Installing the previous release (v$env:PREVIOUS_RELEASE_VERSION) to seed a genuinely aged on-disk state..."; \
    curl "https://github.com/DataDog/dd-trace-dotnet/releases/download/v$env:PREVIOUS_RELEASE_VERSION/datadog-dotnet-apm-$env:PREVIOUS_RELEASE_VERSION-x64.msi" -o datadog-apm-previous.msi; \
    $previous = Start-Process -Wait -PassThru msiexec -ArgumentList '/qn /i datadog-apm-previous.msi /l*v C:\logs\install-previous-release.log'; \
    if ($previous.ExitCode -ne 0) { throw "Installing the previous release (v$env:PREVIOUS_RELEASE_VERSION) failed with exit code $($previous.ExitCode); see C:\logs\install-previous-release.log" }; \
    Remove-Item datadog-apm-previous.msi; \
    echo 'Upgrading in place to the locally-built MSI under test...'; \
    $upgrade = Start-Process -Wait -PassThru msiexec -ArgumentList '/qn /i datadog-apm.msi /l*v C:\logs\install-upgrade.log'; \
    if ($upgrade.ExitCode -ne 0) { throw "Upgrading to the locally-built MSI failed with exit code $($upgrade.ExitCode); see C:\logs\install-upgrade.log" }; \
    echo "Expanding this build's own monitoring home to compare against..."; \
    Expand-Archive 'c:\install\windows-tracer-home.zip' -DestinationPath 'c:\expected-monitoring-home\'; \
    cd /app; \
    rm /install -r -fo

# Every native binary that carries a version independent of the tracer's own (i.e.
# would otherwise fall back to Windows' unreliable created-vs-modified timestamp
# comparison) must come out of the upgrade matching this build's own copy exactly.
# SHA256 comparison is cheaper and more robust than parsing the PE debug GUID, and
# catches a stale file regardless of whether it happens to crash on startup.
RUN $installed = 'C:\Program Files\Datadog\.NET Tracer\win-x64'; \
    $expected = 'C:\expected-monitoring-home\win-x64'; \
    $mismatches = @(); \
    foreach ($file in @('datadog_profiling_ffi.dll', 'ddwaf.dll')) { \
        $installedHash = (Get-FileHash (Join-Path $installed $file)).Hash; \
        $expectedHash = (Get-FileHash (Join-Path $expected $file)).Hash; \
        Write-Host "${file}: installed=$installedHash expected=$expectedHash"; \
        if ($installedHash -ne $expectedHash) { $mismatches += $file }; \
    }; \
    Remove-Item C:\expected-monitoring-home -Recurse -Force; \
    if ($mismatches.Count -gt 0) { throw "In-place upgrade left a stale copy of: $($mismatches -join ', ')" }

# Set the additional env vars
ENV DD_PROFILING_ENABLED=1 \
    DD_APPSEC_ENABLED=1 \
    DD_TRACE_DEBUG=1 \
    CORECLR_ENABLE_PROFILING=1 \
    CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8} \
    DD_TRACE_LOG_DIRECTORY="C:\logs" \
    DD_REMOTE_CONFIGURATION_ENABLED=0 \
    ASPNETCORE_URLS=http://localhost:5000

# Set a random env var we should ignore
ENV SUPER_SECRET_CANARY=MySuperSecretCanary

# see https://github.com/DataDog/dd-trace-dotnet/pull/3579
ENV DD_INTERNAL_WORKAROUND_77973_ENABLED=1

# Copy the app across
COPY --from=builder /src/publish /app/.

ENTRYPOINT ["dotnet", "AspNetCoreSmokeTest.dll"]
