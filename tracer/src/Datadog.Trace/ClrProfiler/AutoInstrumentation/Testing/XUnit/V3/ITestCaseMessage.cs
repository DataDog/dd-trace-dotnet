// <copyright file="ITestCaseMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3;

/// <summary>
/// Base interface for all messages related to test cases.
/// </summary>
internal interface ITestCaseMessage
{
    /// <summary>
    /// Gets the test case's unique ID. Can be used to correlate test messages with the appropriate
    /// test case that they're related to.
    /// </summary>
    string TestCaseUniqueID { get; }
}
