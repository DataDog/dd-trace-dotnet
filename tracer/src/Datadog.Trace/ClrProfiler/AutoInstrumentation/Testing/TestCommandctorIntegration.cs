// <copyright file="TestCommandctorIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing;

#pragma warning disable SA1402

/// <summary>
/// System.Void Microsoft.DotNet.Tools.Test.TestCommand::.ctor(System.Collections.Generic.IEnumerable`1[System.String],System.Boolean,System.String) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "dotnet",
    TypeName = "Microsoft.DotNet.Tools.Test.TestCommand",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["System.Collections.Generic.IEnumerable`1[System.String]", ClrNames.Bool, ClrNames.String],
    MinimumVersion = "6.0.0",
    MaximumVersion = "9.*.*",
    IntegrationName = IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class TestCommandctorIntegration
{
    internal const string IntegrationName = nameof(IntegrationId.DotnetTest);
    internal const IntegrationId IntegrationId = Configuration.IntegrationId.DotnetTest;

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref IEnumerable<string>? msbuildArgs, ref bool noRestore, ref string? msbuildPath)
    {
        /*
        2024-04-18 13:57:57.046 +02:00 [INF] TestCommand::.ctor
           msbuildArgs:
            -target:VSTest
            -nodereuse:false
            -nologo
            -property:VSTestTestAdapterPath="/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/test/test-applications/integrations/Samples.XUnitTests/~/repos;/Users/tony.redondo/repos/github/Datadog/dd-trace-dotnet/tracer/src/Datadog.Trace.Tools.Runner/bin/Release/Tool/net7.0/"
            -property:VSTestCollect="MyCollector;DatadogCoverage"
            -property:TargetFramework=net8.0
            -property:VSTestArtifactsProcessingMode=collect
            -property:VSTestSessionCorrelationId=65745_6006a69f-289a-48e9-8d6f-7cbaa04065bc
           noRestore: False
           msbuildPath:
         */

        var sb = new StringBuilder();
        sb.AppendLine("TestCommand::.ctor");
        sb.AppendLine("\tmsbuildArgs: ");
        if (msbuildArgs is not null)
        {
            foreach (var arg in msbuildArgs)
            {
                sb.AppendLine($"\t\t{arg}");
            }
        }

        sb.AppendLine("\tnoRestore: " + noRestore);
        sb.AppendLine("\tmsbuildPath: " + msbuildPath);

        Common.Log.Information("{MessageValue}", sb.ToString());
        return CallTargetState.GetDefault();
    }
}

/// <summary>
/// System.Void Microsoft.DotNet.Tools.Test.TestCommand::.ctor(System.Collections.Generic.IEnumerable`1[System.String],System.Collections.Generic.IEnumerable`1[System.String],System.Collections.Generic.IEnumerable`1[System.String],System.Boolean,System.String) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "dotnet",
    TypeName = "Microsoft.DotNet.Tools.Test.TestCommand",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["System.Collections.Generic.IEnumerable`1[System.String]", "System.Collections.Generic.IEnumerable`1[System.String]", "System.Collections.Generic.IEnumerable`1[System.String]", ClrNames.Bool, ClrNames.String],
    MinimumVersion = "2.1.0",
    MaximumVersion = "5.*.*",
    IntegrationName = IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class TestCommand5ctorIntegration
{
    internal const string IntegrationName = nameof(IntegrationId.DotnetTest);
    internal const IntegrationId IntegrationId = Configuration.IntegrationId.DotnetTest;

    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref IEnumerable<string>? msbuildArgs, ref IEnumerable<string>? userDefinedArguments, ref IEnumerable<string>? trailingArguments, ref bool noRestore, ref string? msbuildPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TestCommand::.ctor");
        sb.AppendLine("\tmsbuildArgs: ");
        if (msbuildArgs is not null)
        {
            foreach (var arg in msbuildArgs)
            {
                sb.AppendLine($"\t\t{arg}");
            }
        }

        sb.AppendLine("\tuserDefinedArguments: ");
        if (userDefinedArguments is not null)
        {
            foreach (var arg in userDefinedArguments)
            {
                sb.AppendLine($"\t\t{arg}");
            }
        }

        sb.AppendLine("\ttrailingArguments: ");
        if (trailingArguments is not null)
        {
            foreach (var arg in trailingArguments)
            {
                sb.AppendLine($"\t\t{arg}");
            }
        }

        sb.AppendLine("\tnoRestore: " + noRestore);
        sb.AppendLine("\tmsbuildPath: " + msbuildPath);

        Common.Log.Information("{MessageValue}", sb.ToString());
        return CallTargetState.GetDefault();
    }
}
