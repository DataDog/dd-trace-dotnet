# escape=`

# Use the latest Windows Server Core image with .NET Framework 4.7.2.
FROM microsoft/dotnet-framework:4.7.2-sdk

# Restore the default Windows shell for correct batch processing below.
SHELL ["cmd", "/S", "/C"]

# Download the Build Tools bootstrapper.
ADD https://aka.ms/vs/15/release/vs_buildtools.exe C:\TEMP\vs_buildtools.exe

# Install Build Tools excluding workloads and components with known issues.
RUN C:\TEMP\vs_buildtools.exe --quiet --wait --norestart --nocache `
    --installPath C:\BuildTools `
    --all `
    --remove Microsoft.VisualStudio.Component.Windows10SDK.10240 `
    --remove Microsoft.VisualStudio.Component.Windows10SDK.10586 `
    --remove Microsoft.VisualStudio.Component.Windows10SDK.14393 `
    --remove Microsoft.VisualStudio.Component.Windows81SDK `
 || IF "%ERRORLEVEL%"=="3010" EXIT 0

FROM base as build

SHELL ["C:\\BuildTools\\Common7\\Tools\\VsDevCmd.bat"]

WORKDIR /app
COPY *.sln .
COPY src/Datadog.Trace/*.csproj ./src/Datadog.Trace/
COPY src/Datadog.Trace.ClrProfiler.Managed/*.csproj ./src/Datadog.Trace.ClrProfiler.Managed/
COPY test/Datadog.Trace.ClrProfiler.IntegrationTests/*.csproj ./test/Datadog.Trace.ClrProfiler.IntegrationTests/
COPY test/Datadog.Trace.TestHelpers/*.csproj ./test/Datadog.Trace.TestHelpers/

# restore trace
WORKDIR /app/src/Datadog.Trace
RUN dotnet restore

# restore clr profiler
WORKDIR /app/src/Datadog.Trace.ClrProfiler.Managed
RUN dotnet restore

# restore test helpers
WORKDIR /app/test/Datadog.Trace.TestHelpers
RUN dotnet restore

# restore integration tests
WORKDIR /app/test/Datadog.Trace.ClrProfiler.IntegrationTests
RUN dotnet restore

WORKDIR /app
COPY . .

# build native DLL
WORKDIR /app/src/Datadog.Trace.ClrProfiler.Native
RUN dotnet msbuild Datadog.Trace.ClrProfiler.Native.vcxproj

# build integration tests
WORKDIR /app/test/Datadog.Trace.ClrProfiler.IntegrationTests
RUN dotnet build

FROM build AS test
WORKDIR /app/test/Datadog.Trace.ClrProfiler.IntegrationTests
RUN dotnet test
