ARG DOTNETSDK_VERSION
ARG RUNTIME_IMAGE

# Build the ASP.NET Core app using the latest SDK
FROM mcr.microsoft.com/dotnet/sdk:$DOTNETSDK_VERSION-windowsservercore-ltsc2022 as builder

# Build the smoke test app
WORKDIR /src
COPY ./test/test-applications/regression/AspNetCoreSmokeTest/ .

ARG PUBLISH_FRAMEWORK
RUN dotnet publish "AspNetCoreSmokeTest.csproj" -c Release --framework %PUBLISH_FRAMEWORK% -o /src/publish

FROM $RUNTIME_IMAGE AS publish
SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

WORKDIR /app

ARG CHANNEL
ARG TARGET_PLATFORM

# Install the hosting bundle
RUN  $url='https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/' + $env:CHANNEL + '.0/dotnet-hosting-' + $env:CHANNEL + '.0-win.exe'; \
    echo "Fetching " + $url; \
    Invoke-WebRequest $url -OutFile c:/hosting.exe; \
    Start-Process -Wait -PassThru -FilePath "c:/hosting.exe" -ArgumentList @('/install', '/q', '/norestart'); \
    rm c:/hosting.exe;

# Copy the tracer home file from tracer/test/test-applications/regression/AspNetCoreSmokeTest/artifacts
COPY --from=builder /src/artifacts /install

ARG INSTALL_COMMAND
RUN mkdir /logs; \
    mkdir /monitoring-home; \
    cd /install; \
    Expand-Archive 'c:\install\windows-tracer-home.zip' -DestinationPath 'c:\monitoring-home\';  \
    c:\install\Datadog.FleetInstaller.exe install-version --home-path c:\monitoring-home; \
    cd /app;

# Set the additional env vars
ENV DD_PROFILING_ENABLED=1 \
    DD_TRACE_DEBUG=1 \
    DD_APPSEC_ENABLED=1 \
    DD_REMOTE_CONFIGURATION_ENABLED=0 \
    DD_TRACE_LOG_DIRECTORY="C:\logs"

# Set a random env var we should ignore
ENV SUPER_SECRET_CANARY=MySuperSecretCanary

# see https://github.com/DataDog/dd-trace-dotnet/pull/3579
ENV DD_INTERNAL_WORKAROUND_77973_ENABLED=1

# Copy the app across
COPY --from=builder /src/publish /app/.

# Create new website we control, but keep auto-start disabled, and stop IIS
RUN Remove-WebSite -Name 'Default Web Site'; \
    $ENABLE_32_BIT='false'; \
    if($env:TARGET_PLATFORM -eq 'x86') { $ENABLE_32_BIT='true' }; \
    Write-Host "Creating website with 32 bit enabled: $env:ENABLE_32_BIT"; \
    c:\Windows\System32\inetsrv\appcmd add apppool /startMode:"AlwaysRunning" /autoStart:"false" /name:AspNetCorePool /managedRuntimeVersion:"" /enable32bitapponwin64:$ENABLE_32_BIT; \
    New-Website -Name 'SmokeTest' -Port 5000 -PhysicalPath 'c:\app' -ApplicationPool 'AspNetCorePool'; \
    Set-ItemProperty "IIS:\Sites\SmokeTest" -Name applicationDefaults.preloadEnabled -Value True; \
    net stop /y was

# We override the normal service monitor entrypoint, because we want the container to shut down after the request is sent
# This is all way more convoluted than it feels like it should be, but it's the only way I could find to get things to work as required
# Service monitor copies ambient environment variables to the app pool. However, it doesn't have very much error tracking
# - if an app pool defines an environment variable that is also _globally_ defined, service monitor will crash
# - consequently we run the fleet installer command _after_ service monitor has run,
RUN ('$completedFile=\"C:\logs\completed.txt\"; if (Test-Path $completedFile) { Remove-Item $completedFile;};')  > C:\app\entrypoint.ps1; \
    ('Write-Host \"Running servicemonitor to copy variables\"; Start-Process -NoNewWindow -PassThru -FilePath \"c:/ServiceMonitor.exe\" -ArgumentList @(\"w3svc\", \"AspNetCorePool\");') >> C:\app\entrypoint.ps1; \
    ('Write-Host \"Waiting 20s\"; Start-Sleep -Seconds 20;') >> C:\app\entrypoint.ps1; \
    ('Write-Host \"Enabling instrumentation\"') >> C:\app\entrypoint.ps1; \
    ('c:\install\Datadog.FleetInstaller.exe ' + $env:INSTALL_COMMAND + ' --home-path c:\monitoring-home') >> C:\app\entrypoint.ps1; \
    ('Write-Host \"Starting AspNetCorePool app pool\"; Start-WebAppPool -Name \"AspNetCorePool\" -PassThru;') >> C:\app\entrypoint.ps1; \
    ('Write-Host \"Making 404 request\"; Invoke-WebRequest -Uri http://localhost:5000 -Headers @{ \"User-Agent\" = \"FleetInstallerTester/1.0\" }') >> C:\app\entrypoint.ps1; \
    ('while (-not (Test-Path "C:\logs\completed.txt")) { Write-Host \"Waiting for app shutdown\"; Start-Sleep -Seconds 1; }; ') >> C:\app\entrypoint.ps1; \
    ('Write-Host \"Stopping pool\";Stop-WebAppPool \"AspNetCorePool\" -PassThru;') >> C:\app\entrypoint.ps1;  \
    ('Write-Host \"Shutting down\"') >> C:\app\entrypoint.ps1;

# Set the script as the entrypoint
ENTRYPOINT ["powershell", "-File", "C:\\app\\entrypoint.ps1"]
