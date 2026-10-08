// <copyright file="CallTargetAotCategories.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers;

/// <summary>
/// The instrumentation categories of an application instrumented at build time (NativeAOT). The native tracer enables a
/// CallTarget definition while one of its categories is enabled, and instruments or restores its methods with ReJIT.
/// Here the instrumentation of the categories given to <c>dd-trace aot instrument</c> is compiled in: the registrations
/// of the instrumented methods register their integrations, which <see cref="IntegrationOptions{TIntegration, TTarget}"/>
/// enables while one of their categories is enabled.
/// </summary>
internal static class CallTargetAotCategories
{
    private static readonly object Sync = new();
    private static readonly Dictionary<Type, Integration> Integrations = new();
    private static uint _enabled;

    internal static InstrumentationCategory Enabled => (InstrumentationCategory)Volatile.Read(ref _enabled);

    /// <summary>
    /// Called by the registration of an instrumented method, before its first call reaches the integration.
    /// </summary>
    /// <param name="categories">The categories of the CallTarget definitions of the integration.</param>
    internal static void Register<TIntegration, TTarget>(uint categories)
    {
        lock (Sync)
        {
            var key = typeof(IntegrationOptions<TIntegration, TTarget>);
            if (!Integrations.TryGetValue(key, out var integration))
            {
                integration = new Integration(IntegrationOptions<TIntegration, TTarget>.SetCategoryEnabled);
                Integrations[key] = integration;
            }

            integration.Categories |= categories;
            integration.Apply(_enabled);
        }
    }

    /// <returns>The number of integrations the categories enable.</returns>
    internal static int Enable(InstrumentationCategory categories)
    {
        lock (Sync)
        {
            return Update(_enabled | (uint)categories);
        }
    }

    /// <returns>The number of integrations the categories disable.</returns>
    internal static int Disable(InstrumentationCategory categories)
    {
        lock (Sync)
        {
            return Update(_enabled & ~(uint)categories);
        }
    }

    private static int Update(uint enabled)
    {
        Volatile.Write(ref _enabled, enabled);
        var changed = 0;
        foreach (var integration in Integrations.Values)
        {
            if (integration.Apply(enabled))
            {
                changed++;
            }
        }

        return changed;
    }

    private sealed class Integration
    {
        private readonly Action<bool> _setEnabled;
        private bool? _enabled;

        public Integration(Action<bool> setEnabled)
        {
            _setEnabled = setEnabled;
        }

        public uint Categories { get; set; }

        /// <returns>Whether the integration changed.</returns>
        public bool Apply(uint enabledCategories)
        {
            var enabled = (Categories & enabledCategories) != 0;
            if (_enabled == enabled)
            {
                return false;
            }

            _enabled = enabled;
            _setEnabled(enabled);
            return true;
        }
    }
}
