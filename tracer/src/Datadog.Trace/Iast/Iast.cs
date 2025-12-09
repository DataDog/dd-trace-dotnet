// <copyright file="Iast.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Analyzers;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.Iast;

/// <summary>
/// The class responsible for coordinating IAST
/// </summary>
internal sealed class Iast
{
    private static Iast? _instance;
    private static bool _globalInstanceInitialized;
    private static object _globalInstanceLock = new();
    private readonly IastSettings _settings;
    private readonly OverheadController _overheadController;
    private IDiscoveryService? _discoveryService;
    private bool _spanMetaStructs;

    static Iast()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Iast"/> class with default settings.
    /// </summary>
    public Iast()
        : this(IastSettings.FromDefaultSources())
    {
    }

    internal Iast(IastSettings settings)
    {
        _settings = settings;
        _overheadController = new OverheadController(_settings.MaxConcurrentRequests, _settings.RequestSampling);
    }

    internal Iast(IastSettings settings, IDiscoveryService discoveryService)
        : this(settings)
    {
        _discoveryService = discoveryService;
        SubscribeToDiscoveryService(_discoveryService);
    }

    internal IastSettings Settings => _settings;

    internal OverheadController OverheadController => _overheadController;

    /// <summary>
    /// Gets or sets the global <see cref="Iast"/> instance.
    /// </summary>
    public static Iast Instance
    {
        get => LazyInitializer.EnsureInitialized(ref _instance, ref _globalInstanceInitialized, ref _globalInstanceLock)!;

        set
        {
            lock (_globalInstanceLock)
            {
                _instance = value;
                _globalInstanceInitialized = true;
            }
        }
    }

    internal void InitAnalyzers()
    {
        if (_settings.Enabled)
        {
            HardcodedSecretsAnalyzer.Initialize(TimeSpan.FromMilliseconds(_settings.RegexTimeout));
        }
    }

    internal bool IsMetaStructSupported()
    {
        if (_discoveryService is null)
        {
            _discoveryService = Tracer.Instance.TracerManager.DiscoveryService;
            SubscribeToDiscoveryService(_discoveryService);
        }

        return _spanMetaStructs;
    }

    private void SubscribeToDiscoveryService(IDiscoveryService discoveryService)
    {
        discoveryService.SubscribeToChanges(config => _spanMetaStructs = config.SpanMetaStructs);
    }
}
