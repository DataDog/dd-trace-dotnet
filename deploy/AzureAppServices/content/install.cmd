
REM Create logging directory for tracer version
mkdir D:\home\LogFiles\Datadog\Tracer\v1_10_33

REM Create home directory for tracer version
mkdir D:\home\site\wwwroot\datadog\tracer\v1_10_33

REM Copy tracer home directory to version specific directory
xcopy /e D:\home\SiteExtensions\Datadog.Trace.AzureAppServices\Tracer D:\home\site\wwwroot\datadog\tracer\v1_10_33

REM Create directory for agent to live
mkdir  D:\home\site\wwwroot\datadog\tracer\v1_10_33\agent

REM Copy all agent files
xcopy /e D:\home\SiteExtensions\Datadog.Trace.AzureAppServices\Agent D:\home\site\wwwroot\datadog\tracer\v1_10_33\agent
