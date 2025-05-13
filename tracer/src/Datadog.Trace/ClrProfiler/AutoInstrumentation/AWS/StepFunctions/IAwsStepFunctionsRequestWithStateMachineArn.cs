// <copyright file="IAwsStepFunctionsRequestWithStateMachineArn.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.StepFunctions
{
    /// <summary>
    /// Interface for ducktyping IAmazonStepFunctionsRequest implementations
    /// </summary>
    internal interface IAwsStepFunctionsRequestWithStateMachineArn
    {
        /// <summary>
        /// Gets the name of the state machine
        /// </summary>
        string? StateMachineArn { get; }
    }
}
