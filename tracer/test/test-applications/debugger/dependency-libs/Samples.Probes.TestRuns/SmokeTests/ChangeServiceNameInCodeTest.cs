using Datadog.Trace;
using Datadog.Trace.Configuration;

namespace Samples.Probes.TestRuns.SmokeTests;

public class ChangeServiceNameInCodeTest : IRun
{
    public const string UpdatedServiceName = "service-name-from-code";

    public void Run()
    {
        // Emit a snapshot before changing the service name
        BeforeServiceNameChange();

        // Simulate customer setting the service name in code at runtime
        var settings = TracerSettings.FromDefaultSources();
        settings.ServiceName = UpdatedServiceName;
        Tracer.Configure(settings);

        // Trigger a snapshot after the service name change
        AfterServiceNameChange();
    }

    public void BeforeServiceNameChange()
    {
    }

    public void AfterServiceNameChange()
    {
    }
}

