// <copyright file="DirectSubmissionLogLevelExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Logging.DirectSubmission
{
    internal static class DirectSubmissionLogLevelExtensions
    {
        public const string Verbose = "Verbose";
        public const string Debug = "Debug";
        public const string Information = "Information";
        public const string Warning = "Warning";
        public const string Error = "Error";
        public const string Fatal = "Fatal";

        public const string Unknown = "UNKNOWN";

        public static string GetName(this DirectSubmissionLogLevel logLevel)
            => logLevel switch
            {
                DirectSubmissionLogLevel.Verbose => Verbose,
                DirectSubmissionLogLevel.Debug => Debug,
                DirectSubmissionLogLevel.Information => Information,
                DirectSubmissionLogLevel.Warning => Warning,
                DirectSubmissionLogLevel.Error => Error,
                DirectSubmissionLogLevel.Fatal => Fatal,
                _ => Unknown,
            };

        public static DirectSubmissionLogLevel? Parse(string? value)
            => value?.ToUpperInvariant() switch
            {
                "TRACE" => DirectSubmissionLogLevel.Verbose,
                "VERBOSE" => DirectSubmissionLogLevel.Verbose,
                "DEBUG" => DirectSubmissionLogLevel.Debug,
                "INFO" => DirectSubmissionLogLevel.Information,
                "INFORMATION" => DirectSubmissionLogLevel.Information,
                "WARNING" => DirectSubmissionLogLevel.Warning,
                "ERROR" => DirectSubmissionLogLevel.Error,
                "CRITICAL" => DirectSubmissionLogLevel.Fatal,
                "FATAL" => DirectSubmissionLogLevel.Fatal,
                _ => null
            };
    }
}
