// <copyright file="IAmazonStepFunctionsRequestWithStateMachineArn.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.StepFunctions
{
    /// <summary>
    /// Interface for ducktyping AmazonStepFunctionsStartExecutionRequest implementations with the StateMachineArn property
    /// </summary>
    internal interface IAmazonStepFunctionsRequestWithStateMachineArn
    {
        /// <summary>
        /// Gets the Amazon Resource Name (ARN) of the state machine
        /// </summary>
        string? StateMachineArn { get; }
    }
}
