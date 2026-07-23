// <copyright file="CoverageContextContainer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Datadog.Trace.Ci.Coverage.Metadata;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class CoverageContextContainer : IDisposable
{
    private readonly object _gate = new();
    private readonly List<ModuleValue> _modules = new();
    private readonly ModuleValue.BufferKind _bufferKind;
    private ModuleValue? _currentModuleValue;
    private int _closed;
    private int _disposed;

    public CoverageContextContainer(object? state = null, ModuleValue.BufferKind bufferKind = ModuleValue.BufferKind.Context)
    {
        State = state;
        _bufferKind = bufferKind;
    }

    public object? State { get; set; }

    internal bool IsClosed => Volatile.Read(ref _closed) != 0;

    internal ModuleValue? GetModuleValue(Module module)
    {
        if (IsClosed)
        {
            return null;
        }

        if (Volatile.Read(ref _currentModuleValue) is { } current && current.Module == module)
        {
            return current;
        }

        lock (_gate)
        {
            if (_closed != 0)
            {
                return null;
            }

            return FindModuleValue(module);
        }
    }

    internal bool TryGetOrAddModuleValue(
        ModuleCoverageMetadata metadata,
        Module module,
        int rawByteLength,
        out ModuleValue? moduleValue)
    {
        moduleValue = GetModuleValue(module);
        if (moduleValue is not null)
        {
            return true;
        }

        lock (_gate)
        {
            if (_closed != 0)
            {
                moduleValue = null;
                return false;
            }

            moduleValue = FindModuleValue(module);
            if (moduleValue is not null)
            {
                return true;
            }

            var provisional = new ModuleValue(metadata, module, rawByteLength, _bufferKind);
            try
            {
                _modules.Add(provisional);
            }
            catch
            {
                // List growth can fail after the native buffer was allocated, so the unpublished value must release it here.
                provisional.Dispose();
                throw;
            }

            _currentModuleValue = provisional;
            moduleValue = provisional;
            return true;
        }
    }

    internal bool TryCloseAndGetModules(out IReadOnlyList<ModuleValue> modules)
    {
        lock (_gate)
        {
            if (_closed != 0)
            {
                modules = Array.Empty<ModuleValue>();
                return false;
            }

            _closed = 1;
            _currentModuleValue = null;
            modules = _modules;
            return true;
        }
    }

    internal ModuleValue[] SnapshotModules(int maximumModules = int.MaxValue)
    {
        lock (_gate)
        {
            if (_modules.Count > maximumModules)
            {
                throw new InvalidOperationException("The global coverage fallback contains too many modules.");
            }

            return _modules.Count == 0 ? Array.Empty<ModuleValue>() : _modules.ToArray();
        }
    }

    internal void Clear() => Dispose();

    public void Dispose()
    {
        ExceptionDispatchInfo? firstException = null;
        lock (_gate)
        {
            if (_disposed != 0)
            {
                return;
            }

            _disposed = 1;
            _closed = 1;
            _currentModuleValue = null;
            try
            {
                foreach (var moduleValue in _modules)
                {
                    try
                    {
                        moduleValue.Dispose();
                    }
                    catch (Exception ex)
                    {
                        firstException ??= ExceptionDispatchInfo.Capture(ex);
                    }
                }
            }
            finally
            {
                _modules.Clear();
            }
        }

        firstException?.Throw();
    }

    private ModuleValue? FindModuleValue(Module module)
    {
        for (var i = 0; i < _modules.Count; i++)
        {
            if (_modules[i] is { } item && item.Module == module)
            {
                _currentModuleValue = item;
                return item;
            }
        }

        return null;
    }
}
