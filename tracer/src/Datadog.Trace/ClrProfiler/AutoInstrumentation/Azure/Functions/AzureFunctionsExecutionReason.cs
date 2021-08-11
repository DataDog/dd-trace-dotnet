// <copyright file="AzureFunctionsExecutionReason.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.Functions
{
    /// <summary>
    /// Enum for ducktyping
    /// </summary>
    public enum AzureFunctionsExecutionReason
    {
        /// <summary>Indicates a function executed because of an automatic trigger.</summary>
        AutomaticTrigger,

        /// <summary>Indicates a function executed because of a programmatic host call.</summary>
        HostCall,

        /// <summary>Indicates a function executed because of a request from a dashboard user.</summary>
        Dashboard
    }
}
#endif
