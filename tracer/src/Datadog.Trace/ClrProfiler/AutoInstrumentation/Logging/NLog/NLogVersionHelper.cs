// <copyright file="NLogVersionHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog
{
    /// <summary>
    /// Helps to determine what NuGet version of NLog is based on available types.
    /// </summary>
    internal class NLogVersionHelper<TTarget>
    {
        static NLogVersionHelper()
        {
            var assembly = typeof(TTarget).Assembly;

            var targetWithContext = assembly.GetType("NLog.Targets.TargetWithContext");

            if (targetWithContext?.GetProperty("IncludeScopeProperties") is not null)
            {
                Version = NLogVersion.NLog50;
            }
            else if (targetWithContext is not null)
            {
                Version = NLogVersion.NLog45;
            }
            else if (targetWithContext is null)
            {
                // Type was added in NLog 4.3, so we can use it to safely determine the version
                var testType = assembly.GetType("NLog.Config.ExceptionRenderingFormat");
                Version = testType is null ? NLogVersion.NLogPre43 : NLogVersion.NLog43To45;
            }
        }

        public static NLogVersion Version { get; }
    }
}
