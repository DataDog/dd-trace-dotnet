#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmokeTests;

public abstract record SmokeTestScenario
{
    protected const string DefaultSnapshotIgnoredAttrs =
        "span_id" + ",trace_id" + ",parent_id" + ",duration" + ",start" + ",metrics.system.pid" + ",meta.runtime-id" + ","
        + "meta.network.client.ip" + ",meta.http.client_ip" + ",metrics.process_id" + ",meta._dd.p.dm" + ","
        + "meta._dd.p.tid" + ",meta._dd.parent_id" + ",meta._dd.appsec.s.req.params" + ","
        + "meta._dd.appsec.s.res.body" + ",meta._dd.appsec.s.req.headers" + ","
        + "meta._dd.appsec.s.res.headers" + ",meta._dd.appsec.fp.http.endpoint" + ","
        + "meta._dd.appsec.fp.http.header" + ",meta._dd.appsec.fp.http.network";

    public required string ShortName { get; init; }
    public required string PublishFramework { get; init; }
    public required string RuntimeTag { get; init; }
    public required string DockerImageRepo { get; init; }
    public required string Os { get; init; }
    public required string OsVersion { get; init; }

    public bool RunCrashTest { get; init; } = true;
    public bool ExcludeWhenPrerelease { get; init; }
    public bool IsNoop { get; init; }

    public virtual string SnapshotFile
        => PublishFramework == "netcoreapp2.1" ? "smoke_test_snapshots_2_1" : "smoke_test_snapshots";

    public virtual string SnapshotIgnoredAttrs => DefaultSnapshotIgnoredAttrs;

    public string JobName => $"{ShortName}_{RuntimeTag.Replace('.', '_')}";
    public string DockerTag => $"dd-trace-dotnet/{JobName}-tester";
    public string RuntimeImage => $"{DockerImageRepo}:{RuntimeTag}";
    public bool IsWindows => Os == "windows";
}

public record InstallerScenario : SmokeTestScenario
{
    public required InstallType InstallType { get; init; }
    public string InstallCommand => InstallType.GetInstallCommand();
}

public record ChiseledScenario : SmokeTestScenario
{
    public bool IsArm64 { get; init; }
}

public record NuGetScenario : SmokeTestScenario
{
    public required string RuntimeId { get; init; }
    public required string NuGetPackageName { get; init; }
    public string RelativeProfilerPath => $"datadog/{RuntimeId}/Datadog.Trace.ClrProfiler.Native.so";
    public string RelativeApiWrapperPath => $"datadog/{RuntimeId}/Datadog.Linux.ApiWrapper.x64.so";

    public override string SnapshotFile => NuGetPackageName switch
    {
        Projects.DatadogAzureFunctions => "smoke_test_azurefunctions_snapshots",
        _ => base.SnapshotFile,
    };
}

public record DotnetToolScenario : SmokeTestScenario
{
    public required string RuntimeId { get; init; }
}

public record DotnetToolNugetScenario : SmokeTestScenario
{
    public required string RuntimeId { get; init; }
}

public record SelfInstrumentScenario : SmokeTestScenario
{
    public required InstallType InstallType { get; init; }
    public string InstallCommand => InstallType.GetInstallCommand();
}

public record TrimmingScenario : SmokeTestScenario
{
    public required InstallType InstallType { get; init; }
    public required string RuntimeId { get; init; }
    public required string PackageName { get; init; }
    public string? PackageVersionSuffix { get; init; }
    public string InstallCommand => InstallType.GetInstallCommand();
}

public record WindowsMsiScenario : SmokeTestScenario
{
    public required string Channel32Bit { get; init; }
}

public record WindowsNuGetScenario : SmokeTestScenario
{
    public required string Channel32Bit { get; init; }
    public required string RelativeProfilerPath { get; init; }
    public required string NuGetPackageName { get; init; }
}

public record WindowsDotnetToolScenario : SmokeTestScenario
{
    public required string Channel32Bit { get; init; }
}

public record WindowsTracerHomeScenario : SmokeTestScenario
{
    public required string Channel32Bit { get; init; }
    public required string RelativeProfilerPath { get; init; }
}

public record WindowsFleetInstallerIisScenario : SmokeTestScenario
{
    public required string TargetPlatform { get; init; }
    public required string FleetInstallerCommand { get; init; }

    public override string SnapshotFile => "smoke_test_iis_snapshots";

    public override string SnapshotIgnoredAttrs
        => DefaultSnapshotIgnoredAttrs
         + ",meta._dd.appsec.waf.version" + ",metrics._dd.appsec.event_rules.loaded"
         + ",metrics._dd.appsec.event_rules.error_count" + ",metrics._dd.tracer_kr"
         + ",metrics._sampling_priority_v1";
}
