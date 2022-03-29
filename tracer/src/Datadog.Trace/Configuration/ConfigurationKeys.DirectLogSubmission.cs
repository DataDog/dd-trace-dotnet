// <copyright file="ConfigurationKeys.DirectLogSubmission.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Logging.DirectSubmission;

namespace Datadog.Trace.Configuration
{
    internal static partial class ConfigurationKeys
    {
        internal static class DirectLogSubmission
        {
            /// <summary>
            /// Configuration key for a list of direct log submission integrations to enable.
            /// Only selected integrations are enabled for direct log submission
            /// Default is empty (direct log submission disabled).
            /// Supports multiple values separated with semi-colons.
            /// </summary>
            /// <seealso cref="DirectLogSubmissionSettings.DirectLogSubmissionEnabledIntegrations"/>
            public const string EnabledIntegrations = "DD_LOGS_DIRECT_SUBMISSION_INTEGRATIONS";

            /// <summary>
            /// Set the name of the originating host for direct logs submission.
            /// Required for direct logs submission (default is machine name).
            /// </summary>
            /// <seealso cref="DirectLogSubmissionSettings.DirectLogSubmissionHost"/>
            public const string Host = "DD_LOGS_DIRECT_SUBMISSION_HOST";

            /// <summary>
            /// Set the originating source for direct logs submission.
            /// Default is 'csharp'
            /// </summary>
            /// <seealso cref="DirectLogSubmissionSettings.DirectLogSubmissionSource"/>
            public const string Source = "DD_LOGS_DIRECT_SUBMISSION_SOURCE";

            /// <summary>
            /// Configuration key for a list of tags to be applied globally to all logs directly submitted.
            /// Supports multiple key key-value pairs which are comma-separated, and for which the key and
            /// value are colon-separated. For example Key1:Value1, Key2:Value2
            /// </summary>
            /// <seealso cref="DirectLogSubmissionSettings.DirectLogSubmissionGlobalTags"/>
            public const string GlobalTags = "DD_LOGS_DIRECT_SUBMISSION_TAGS";

            /// <summary>
            /// Configuration key for the url to send logs to.
            /// Default value uses the domain set in <see cref="ConfigurationKeys.Site"/>, so defaults to
            /// <c>https://http-intake.logs.datadoghq.com:443</c>.
            /// </summary>
            /// <seealso cref="DirectLogSubmissionSettings.DirectLogSubmissionUrl"/>
            public const string Url = "DD_LOGS_DIRECT_SUBMISSION_URL";

            /// <summary>
            /// Configuration key for the minimum level logs should have to be sent to the intake.
            /// Default value is <c>Information</c>.
            /// Should be one of <c>Verbose</c>,<c>Debug</c>,<c>Information</c>,<c>Warning</c>,<c>Error</c>,<c>Fatal</c>
            /// </summary>
            /// <seealso cref="DirectLogSubmissionSettings.DirectLogSubmissionMinimumLevel"/>
            public const string MinimumLevel = "DD_LOGS_DIRECT_SUBMISSION_MINIMUM_LEVEL";

            /// <summary>
            /// Configuration key for the maximum number of logs to send at one time
            /// Default value is <c>1,000</c>, the maximum accepted by the Datadog log API
            /// </summary>
            /// <seealso cref="DirectLogSubmissionSettings.DirectLogSubmissionBatchSizeLimit"/>
            public const string BatchSizeLimit = "DD_LOGS_DIRECT_SUBMISSION_MAX_BATCH_SIZE";

            /// <summary>
            /// Configuration key for the maximum number of logs to hold in internal queue at any one time
            /// Default value is <c>100,000</c>.
            /// </summary>
            /// <seealso cref="DirectLogSubmissionSettings.DirectLogSubmissionQueueSizeLimit"/>
            public const string QueueSizeLimit = "DD_LOGS_DIRECT_SUBMISSION_MAX_QUEUE_SIZE";

            /// <summary>
            /// Configuration key for the time to wait between checking for batches
            /// Default value is <c>2</c>s.
            /// </summary>
            /// <seealso cref="DirectLogSubmissionSettings.DirectLogSubmissionBatchPeriod"/>
            public const string BatchPeriodSeconds = "DD_LOGS_DIRECT_SUBMISSION_BATCH_PERIOD_SECONDS";
        }
    }
}
