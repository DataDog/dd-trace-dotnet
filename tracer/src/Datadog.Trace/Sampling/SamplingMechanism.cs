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
    /// No sampling decision was made; or it was made with an unknown mechanism.
    /// This is NOT a valid value and should not be sent to the Trace Agent.
    /// It is only used internally by the tracer.
    /// </summary>
    public const int Unknown = -1;

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
    /// </summary>
    public const int RemoteRateAuto = 2;

    /// <summary>
    /// A sampling decision was made using a sampling rule or
    /// the global sampling rate configured by the user on the tracer.
    /// The available sampling priorities are <see cref="SamplingPriorityValues.UserReject"/> (-1)
    /// and <see cref="SamplingPriorityValues.UserKeep"/> (2).
    /// </summary>
    public const int Rule = 3;

    /// <summary>
    /// A sampling decision was made manually by the user.
    /// The available sampling priorities are <see cref="SamplingPriorityValues.UserReject"/> (-1)
    /// and <see cref="SamplingPriorityValues.UserKeep"/> (2).
    /// </summary>
    public const int Manual = 4;

    /// <summary>
    /// A sampling decision was made by AppSec; probably due to a security event.
    /// The sampling priority is always <see cref="SamplingPriorityValues.UserKeep"/> (2).
    /// </summary>
    public const int AppSec = 5;

    /// <summary>
    /// A sampling decision was made using a sampling rule configured remotely by the user.
    /// The available sampling priorities are <see cref="SamplingPriorityValues.UserReject"/> (-1)
    /// and <see cref="SamplingPriorityValues.UserKeep"/> (2).
    /// </summary>
    public const int RemoteRateUser = 6;

    /// <summary>
    /// A sampling decision was made using a sampling rule configured remotely by Datadog.
    /// The available sampling priorities are [TBD].
    /// </summary>
    public const int RemoteRateDatadog = 7;

    /// <summary>
    /// CIApp does not have a defined mechanism value. Default to <see cref="Unknown"/> for now.
    /// </summary>
    public const int CiApp = Unknown;
}
