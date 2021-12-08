// <copyright file="DirectSubmissionLogLevel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;

namespace Datadog.Trace.Logging.DirectSubmission
{
    /// <summary>
    /// The unified log levels to use with direct submission
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public enum DirectSubmissionLogLevel
    {
        /// <summary>
        /// The most verbose level. Also known as Trace
        /// </summary>
        Verbose = 0,

        /// <summary>
        /// Debug
        /// </summary>
        Debug = 1,

        /// <summary>
        /// The default log level
        /// </summary>
        Information = 2,

        /// <summary>
        /// Warning
        /// </summary>
        Warning = 3,

        /// <summary>
        /// Error
        /// </summary>
        Error = 4,

        /// <summary>
        /// The least verbose/most severe level. Also known as critical
        /// </summary>
        Fatal = 5,
    }
}
