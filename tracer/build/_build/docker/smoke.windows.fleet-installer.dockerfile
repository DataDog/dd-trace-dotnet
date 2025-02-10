ARG BUILD_IMAGE_TAG
ARG RUNTIME_IMAGE

# Build the ASP.NET Core app using the latest SDK
FROM mcr.microsoft.com/dotnet/sdk:$BUILD_IMAGE_TAG as builder

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

RUN mkdir /logs; \
    mkdir /monitoring-home; \
    cd /install; \
    Expand-Archive 'c:\install\windows-tracer-home.zip' -DestinationPath 'c:\monitoring-home\';  \
    c:\install\installer\Datadog.FleetInstaller.exe install --home-path c:\monitoring-home; \
    cd /app;

# Set the additional env vars
ENV DD_PROFILING_ENABLED=1 \
    DD_TRACE_DEBUG=1 \
    DD_APPSEC_ENABLED=1 \
    DD_TRACE_LOG_DIRECTORY="C:\logs"

# Set a random env var we should ignore
ENV SUPER_SECRET_CANARY=MySuperSecretCanary

# see https://github.com/DataDog/dd-trace-dotnet/pull/3579
ENV DD_INTERNAL_WORKAROUND_77973_ENABLED=1

# Copy the app across
COPY --from=builder /src/publish /app/.

# Create new website we control, but keep auto-start disabled
RUN Remove-WebSite -Name 'Default Web Site'; \
    $ENABLE_32_BIT='false'; \
    if($env:TARGET_PLATFORM -eq 'x86') { $ENABLE_32_BIT='true' }; \
    Write-Host "Creating website with 32 bit enabled: $env:ENABLE_32_BIT"; \
    c:\Windows\System32\inetsrv\appcmd add apppool /startMode:"AlwaysRunning" /autoStart:"false" /name:AspNetCorePool /managedRuntimeVersion:"" /enable32bitapponwin64:$ENABLE_32_BIT; \
    New-Website -Name 'SmokeTest' -Port 5000 -PhysicalPath 'c:\app' -ApplicationPool 'AspNetCorePool'; \
    Set-ItemProperty "IIS:\Sites\SmokeTest" -Name applicationDefaults.preloadEnabled -Value True;

# We override the normal service monitor entrypoint, because we want the container to shut down after the request is sent
# We would really like to get the pid of the worker processes, but we can't do that easily
# This is all way more convoluted than it feels like it should be, but it's the only way I could find to get things to work as required
RUN echo 'Write-Host \"Running servicemonitor to copy variables\"; Start-Process -NoNewWindow -PassThru -FilePath \"c:/ServiceMonitor.exe\" -ArgumentList @(\"w3svc\", \"AspNetCorePool\");' > C:\app\entrypoint.ps1; \
    echo 'Write-Host \"Starting AspNetCorePool app pool\"; Start-WebAppPool -Name \"AspNetCorePool\" -PassThru;' >> C:\app\entrypoint.ps1; \
    echo 'Write-Host \"Making 404 request\"; curl http://localhost:5000;' >> C:\app\entrypoint.ps1; \
    echo 'Write-Host \"Stopping pool\";Stop-WebAppPool \"AspNetCorePool\" -PassThru;' >> C:\app\entrypoint.ps1;  \
    echo 'Write-Host \"Shutting down\"' >> C:\app\entrypoint.ps1;

# Set the script as the entrypoint
ENTRYPOINT ["powershell", "-File", "C:\\app\\entrypoint.ps1"]
