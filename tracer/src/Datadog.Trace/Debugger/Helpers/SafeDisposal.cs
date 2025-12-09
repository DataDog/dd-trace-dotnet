// <copyright file="SafeDisposal.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;

#nullable enable
namespace Datadog.Trace.Debugger.Helpers
{
    internal static class SafeDisposal
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SafeDisposal));

        internal static void TryDispose<T>(T? disposable, string? component = null)
            where T : IDisposable
        {
            if (disposable is null)
            {
                return;
            }

            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                var name = component ?? typeof(T).Name;
                Log.Error(ex, "Error disposing {ComponentName}", name);
            }
        }

        internal static void TryExecute(Action? action, string actionDescription)
        {
            if (action is null)
            {
                return;
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error executing {ActionDescription}", actionDescription);
            }
        }

        internal static DisposalBuilder New() => new DisposalBuilder();

        internal sealed class DisposalBuilder
        {
            private readonly List<Action> _actions = [];

            internal DisposalBuilder Add<T>(T? disposable, string? name = null)
                where T : class, IDisposable
            {
                _actions.Add(() => TryDispose(disposable, name));
                return this;
            }

            internal DisposalBuilder Execute(Action action, string description)
            {
                _actions.Add(() => TryExecute(action, description));
                return this;
            }

            internal void DisposeAll()
            {
                foreach (var action in _actions)
                {
                    action();
                }
            }
        }
    }
}
