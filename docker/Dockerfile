FROM microsoft/nanoserver

RUN powershell.exe -Command \
    $ErrorActionPreference = 'Stop'; \
    New-Item -Path c:\datadog-trace-agent -ItemType directory; \
    Invoke-WebRequest https://github.com/DataDog/datadog-trace-agent/releases/download/5.19.0/trace-agent-windows-5.19.0.exe -OutFile c:\datadog-trace-agent\trace-agent-windows.exe;

ADD config.ini c:/datadog-trace-agent/config.ini

EXPOSE 8126

CMD ["c:\datadog-trace-agent\trace-agent-windows.exe", "--ddconfig c:\datadog-trace-agent\config.ini"]
