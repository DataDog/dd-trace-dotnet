// <copyright file="ActivityMapping.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Activity.Handlers
{
    internal readonly struct ActivityMapping
    {
        // The Activity is held via a WeakReference so the mapping does not
        // keep the underlying System.Diagnostics.Activity alive on its own.
        // If the customer drops all references without calling Stop/Dispose,
        // GC can reclaim the Activity and the periodic reconciliation sweep
        // in ActivityHandlerCommon will close the associated Scope.
        public readonly WeakReference<object> Activity;
        public readonly Scope Scope;

        internal ActivityMapping(WeakReference<object> activity, Scope scope)
        {
            Activity = activity;
            Scope = scope;
        }
    }
}
