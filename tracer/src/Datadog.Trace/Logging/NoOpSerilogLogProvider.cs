// <copyright file="NoOpSerilogLogProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging.LogProviders;

namespace Datadog.Trace.Logging
{
    /// <summary>
    /// Log provider that performs a no-op on all logs injection for Serilog versions before 2.0
    /// because the default behavior may throw unhandled exceptions when cross-AppDomain calls
    /// are made. Prefer disabling the feature over taking a chance that the application's avoids
    /// cross-AppDomain calls.
    /// </summary>
    internal class NoOpSerilogLogProvider : SerilogLogProvider
    {
        private static readonly IDisposable NoopDisposableInstance = new DisposableAction();
        private static readonly Version SupportedSerilogAssemblyVersion = new Version("2.0.0.0");

        internal static new bool IsLoggerAvailable() =>
            SerilogLogProvider.IsLoggerAvailable() && !SerilogAssemblySupported();

#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
        protected override OpenMdc GetOpenMdcMethod()
        {
            return (_, __, ___) => NoopDisposableInstance;
        }
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter

        private static bool SerilogAssemblySupported()
        {
            var ndcContextType = FindType("Serilog.Context.LogContext", new[] { "Serilog", "Serilog.FullNetFx" });
            var serilogVersion = ndcContextType.Assembly.GetName().Version;
            return serilogVersion >= SupportedSerilogAssemblyVersion;
        }
    }
}
