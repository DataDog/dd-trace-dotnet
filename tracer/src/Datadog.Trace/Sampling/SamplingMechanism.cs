// <copyright file="SamplingMechanism.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Sampling;

/// <summary>
/// The mechanism used to make a trace sampling decision.
/// </summary>
internal static class SamplingMechanism
{
    /// <summary>
    /// Sampling decision was made using the default mechanism. Used before the tracer
    /// receives any rates from agent and there are no rules configured.
    /// The available sampling priorities are <see cref="SamplingPriorityValues.AutoReject"/> (0)
    /// and <see cref="SamplingPriorityValues.AutoKeep"/> (1).
    /// </summary>
    public const int Default = 0;

    /// <summary>
    /// A sampling decision was made using a sampling rate computed automatically by the Agent.
    /// The available sampling priorities are <see cref="SamplingPriorityValues.AutoReject"/> (0)
    /// and <see cref="SamplingPriorityValues.AutoKeep"/> (1).
    /// </summary>
    public const int AgentRate = 1;

    /// <summary>
    /// A sampling decision was made using a sampling rate computed automatically by the backend.
    /// The available sampling priorities are <see cref="SamplingPriorityValues.AutoReject"/> (0)
    /// and <see cref="SamplingPriorityValues.AutoKeep"/> (1).
    /// (Reserved for future use.)
    /// </summary>
    public const int RemoteRateAuto = 2;

    /// <summary>
    /// A sampling decision was made using a trace sampling rule or
    /// the global trace sampling rate configured by the user on the tracer.
    /// The available sampling priorities are <see cref="SamplingPriorityValues.UserReject"/> (-1)
    /// and <see cref="SamplingPriorityValues.UserKeep"/> (2).
    /// </summary>
    public const int TraceSamplingRule = 3;

    /// <summary>
    /// A sampling decision was made manually by the user.
    /// The available sampling priorities are <see cref="SamplingPriorityValues.UserReject"/> (-1)
    /// and <see cref="SamplingPriorityValues.UserKeep"/> (2).
    /// </summary>
    public const int Manual = 4;

    /// <summary>
    /// A sampling decision was made by ASM (formerly AppSec), probably due to a security event.
    /// The sampling priority is always <see cref="SamplingPriorityValues.UserKeep"/> (2).
    /// </summary>
    public const int Asm = 5;

    /// <summary>
    /// A sampling decision was made using a sampling rule configured remotely by the user.
    /// The available sampling priorities are <see cref="SamplingPriorityValues.UserReject"/> (-1)
    /// and <see cref="SamplingPriorityValues.UserKeep"/> (2).
    /// (Reserved for future use.)
    /// </summary>
    public const int RemoteRateUser = 6;

    /// <summary>
    /// A sampling decision was made using a sampling rule configured remotely by Datadog.
    /// (Reserved for future use.)
    /// </summary>
    public const int RemoteRateDatadog = 7;

    /// <summary>
    /// A sampling decision was made using a span sampling rule configured by the user on the tracer.
    /// The only available sampling priority is <see cref="SamplingPriorityValues.UserKeep"/> (2).
    /// </summary>
    public const int SpanSamplingRule = 8;
}
