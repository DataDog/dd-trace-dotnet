// <copyright file="IActivityListener.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Activity
{
    internal interface IActivityListener : IDuckType
    {
        object ActivityStarted { get; set; }

        object ActivityStopped { get; set; }

        object ShouldListenTo { get; set; }

        object Sample { get; set; }

        object SampleUsingParentId { get; set; }
    }
}
