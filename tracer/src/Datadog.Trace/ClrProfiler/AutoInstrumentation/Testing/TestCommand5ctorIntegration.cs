// <copyright file="TestCommand5ctorIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing;

/// <summary>
/// System.Void Microsoft.DotNet.Tools.Test.TestCommand::.ctor(System.Collections.Generic.IEnumerable`1[System.String],System.Collections.Generic.IEnumerable`1[System.String],System.Collections.Generic.IEnumerable`1[System.String],System.Boolean,System.String) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "dotnet",
    TypeName = "Microsoft.DotNet.Tools.Test.TestCommand",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["System.Collections.Generic.IEnumerable`1[System.String]", "System.Collections.Generic.IEnumerable`1[System.String]", "System.Collections.Generic.IEnumerable`1[System.String]", ClrNames.Bool, ClrNames.String],
    MinimumVersion = "2.0.0",
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
        if (msbuildArgs is null || !CIVisibility.IsRunning || CIVisibility.Settings.CodeCoverageEnabled != true)
        {
            return CallTargetState.GetDefault();
        }

        Common.InjectCodeCoverageCollector(ref msbuildArgs);

        if (Common.Log.IsEnabled(LogEventLevel.Debug))
        {
            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
            sb.AppendLine("Microsoft.DotNet.Tools.Test.TestCommand..ctor arguments:");
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
            Common.Log.Debug("{MessageValue}", StringBuilderCache.GetStringAndRelease(sb));
        }

        return CallTargetState.GetDefault();
    }
}
