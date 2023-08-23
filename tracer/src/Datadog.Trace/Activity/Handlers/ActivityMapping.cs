// <copyright file="ActivityMapping.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Activity.Handlers
{
    internal readonly struct ActivityMapping
    {
        public readonly object Activity;
        public readonly Scope Scope;

        internal ActivityMapping(object activity, Scope scope)
        {
            Activity = activity;
            Scope = scope;
        }
    }
}
