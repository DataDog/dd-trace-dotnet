// <copyright file="MockOtlpStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using OpenTelemetry.Proto.Trace.V1;

namespace Datadog.Trace.TestHelpers.MockOtlp;

/// <summary>
/// The status of a span, as reported over OTLP.
/// </summary>
public sealed class MockOtlpStatus
{
    private MockOtlpStatus(string message, Status.Types.StatusCode code)
    {
        Message = message;
        Code = code;
    }

    public string Message { get; }

    public Status.Types.StatusCode Code { get; }

    internal static MockOtlpStatus Create(Status status)
        => new(status.Message, status.Code);
}
