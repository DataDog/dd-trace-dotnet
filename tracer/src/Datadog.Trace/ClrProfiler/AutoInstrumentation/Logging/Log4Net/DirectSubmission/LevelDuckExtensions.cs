// <copyright file="LevelDuckExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Logging.DirectSubmission;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission
{
    internal static class LevelDuckExtensions
    {
        private const int DebugThreshold = 30000;
        private const int InfoThreshold = 40000;
        private const int WarningThreshold = 60000;
        private const int ErrorThreshold = 70000;
        private const int FatalThreshold = 80000;

        public static DirectSubmissionLogLevel ToStandardLevel(this LevelDuck level)
            => level.Value switch
            {
                < DebugThreshold => DirectSubmissionLogLevel.Verbose, // ALL/Finest/Verbose/FINER/Trace/
                >= DebugThreshold and < InfoThreshold => DirectSubmissionLogLevel.Debug,
                >= InfoThreshold and < WarningThreshold => DirectSubmissionLogLevel.Information,
                >= WarningThreshold and < ErrorThreshold => DirectSubmissionLogLevel.Warning,
                >= ErrorThreshold and < FatalThreshold => DirectSubmissionLogLevel.Error,
                >= FatalThreshold => DirectSubmissionLogLevel.Fatal, // SEVERE, Critical, Alert, Fatal, Emergency
            };

        public static string ToStandardLevelString(this LevelDuck level)
            => level.Value switch
            {
                < DebugThreshold => DirectSubmissionLogLevelExtensions.Verbose, // ALL/Finest/Verbose/FINER/Trace/
                >= DebugThreshold and < InfoThreshold => DirectSubmissionLogLevelExtensions.Debug,
                >= InfoThreshold and < WarningThreshold => DirectSubmissionLogLevelExtensions.Information,
                >= WarningThreshold and < ErrorThreshold => DirectSubmissionLogLevelExtensions.Warning,
                >= ErrorThreshold and < FatalThreshold => DirectSubmissionLogLevelExtensions.Error,
                >= FatalThreshold => DirectSubmissionLogLevelExtensions.Fatal, // SEVERE, Critical, Alert, Fatal, Emergency
            };
    }
}
