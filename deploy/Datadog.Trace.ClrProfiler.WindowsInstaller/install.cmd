@echo off
setlocal EnableDelayedExpansion

set DATADOG_APPCMD_CMDLINE=%systemroot%\system32\inetsrv\appcmd.exe set config /section:system.webServer/modules /+[name='DatadogTracingModule',type='Datadog.Trace.AspNet.TracingHttpModule,Datadog.Trace.AspNet,Version=1.0.0.0,Culture=neutral,PublicKeyToken=def86d061d0d2eeb']

echo Executing install.cmd at %date% %time%
IF EXIST %systemroot%\system32\inetsrv\appcmd.exe (
    echo Running: %DATADOG_APPCMD_CMDLINE%
    %DATADOG_APPCMD_CMDLINE% 2>&1
) ELSE (
    echo "%systemroot%\system32\inetsrv\appcmd.exe" doesn't exist
)

REM Always report success
exit /b 0