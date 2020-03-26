@echo off
setlocal

echo Executing install.cmd at %date% %time%

set DATADOG_APPCMD_CMDLINE=%systemroot%\system32\inetsrv\appcmd.exe set config /section:system.webServer/modules /+[name='DatadogTracingModule',type='Datadog.Trace.AspNet.TracingHttpModule,Datadog.Trace.AspNet,Version=1.0.0.0,Culture=neutral,PublicKeyToken=def86d061d0d2eeb']

IF EXIST %systemroot%\system32\inetsrv\appcmd.exe (
    echo Attempting to install the Datadog ASP.NET HttpModule with %systemroot%\system32\inetsrv\appcmd.exe
    %DATADOG_APPCMD_CMDLINE% 2>&1
) ELSE (
    echo "%systemroot%\system32\inetsrv\appcmd.exe" doesn't exist. The Datadog ASP.NET HttpModule will not be installed by this installer.
)

REM Always report success
exit /b 0