@echo off
SET fmk=net45
SET mode=Release
echo framework is %fmk%
gacutil -u Datadog.Trace
gacutil -u Datadog.Trace.ClrProfiler.Managed
gacutil -u Datadog.Trace.AspNet
gacutil -i %~dp0\src\Datadog.Trace\bin\%mode%\%fmk%\Datadog.Trace.dll
gacutil -i %~dp0\src\Datadog.Trace.ClrProfiler.Managed\bin\%mode%\%fmk%\Datadog.Trace.ClrProfiler.Managed.dll
gacutil -i %~dp0\src\Datadog.Trace.AspNet\bin\%mode%\net45\Datadog.Trace.AspNet.dll