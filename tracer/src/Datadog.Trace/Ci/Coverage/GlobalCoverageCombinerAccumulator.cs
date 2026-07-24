// <copyright file="GlobalCoverageCombinerAccumulator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageCombinerAccumulator
{
    private readonly GlobalCoverageArtifactLimits _limits;
    private readonly Dictionary<NullableStringKey, ComponentAccumulator> _components = new();
    private readonly List<ComponentAccumulator> _componentOrder = new();
    private int _entryCount;
    private long _identityCharacters;
    private long _bitmapBytes;
    private bool _materialized;

    public GlobalCoverageCombinerAccumulator(GlobalCoverageArtifactLimits? limits = null)
    {
        _limits = limits ?? GlobalCoverageArtifactLimits.Default;
    }

    public void Add(GlobalCoverageInfo model)
    {
        if (_materialized)
        {
            ThrowHelper.ThrowInvalidOperationException("The global coverage accumulator has already been materialized.");
        }

        foreach (var component in model.Components)
        {
            var componentKey = new NullableStringKey(component.Name);
            if (!_components.TryGetValue(componentKey, out var destinationComponent))
            {
                if (_components.Count >= _limits.MaximumComponents)
                {
                    throw new InvalidDataException("The combined global coverage component limit was exceeded.");
                }

                AddIdentity(component.Name);
                destinationComponent = new ComponentAccumulator(component.Name);
                _components.Add(componentKey, destinationComponent);
                _componentOrder.Add(destinationComponent);
            }

            foreach (var file in component.Files)
            {
                var fileKey = new NullableStringKey(file.Path);
                if (!destinationComponent.Files.TryGetValue(fileKey, out var destinationFile))
                {
                    _entryCount = checked(_entryCount + 1);
                    if (_entryCount > _limits.MaximumEntries)
                    {
                        throw new InvalidDataException("The combined global coverage entry limit was exceeded.");
                    }

                    AddIdentity(file.Path);
                    ValidateBitmap(file.ExecutableBitmap);
                    ValidateBitmap(file.ExecutedBitmap);
                    AddBitmapBytes(file.ExecutableBitmap?.Length ?? 0);
                    AddBitmapBytes(file.ExecutedBitmap?.Length ?? 0);
                    destinationFile = new FileAccumulator(file.Path, file.ExecutableBitmap, file.ExecutedBitmap);
                    destinationComponent.Files.Add(fileKey, destinationFile);
                    destinationComponent.FileOrder.Add(destinationFile);
                }
                else
                {
                    destinationFile.ExecutableBitmap = MergeBitmap(destinationFile.ExecutableBitmap, file.ExecutableBitmap);
                    destinationFile.ExecutedBitmap = MergeBitmap(destinationFile.ExecutedBitmap, file.ExecutedBitmap);
                }
            }
        }
    }

    public GlobalCoverageInfo Materialize()
    {
        if (_materialized)
        {
            ThrowHelper.ThrowInvalidOperationException("The global coverage accumulator has already been materialized.");
        }

        _materialized = true;
        var model = new GlobalCoverageInfo();
        foreach (var componentAccumulator in _componentOrder)
        {
            var component = new ComponentCoverageInfo(componentAccumulator.Name);
            foreach (var fileAccumulator in componentAccumulator.FileOrder)
            {
                component.Files.Add(
                    new FileCoverageInfo(fileAccumulator.Path)
                    {
                        ExecutableBitmap = fileAccumulator.ExecutableBitmap,
                        ExecutedBitmap = fileAccumulator.ExecutedBitmap,
                    });
            }

            model.Components.Add(component);
        }

        _ = model.GetTotalPercentage();
        return model;
    }

    private byte[]? MergeBitmap(byte[]? current, byte[]? incoming)
    {
        if (incoming is null)
        {
            return current;
        }

        ValidateBitmap(incoming);
        if (current is null)
        {
            AddBitmapBytes(incoming.Length);
            return incoming;
        }

        if (incoming.Length > current.Length)
        {
            AddBitmapBytes(incoming.Length - current.Length);
            for (var i = 0; i < current.Length; i++)
            {
                incoming[i] |= current[i];
            }

            return incoming;
        }

        for (var i = 0; i < incoming.Length; i++)
        {
            current[i] |= incoming[i];
        }

        return current;
    }

    private void AddIdentity(string? identity)
    {
        _identityCharacters = checked(_identityCharacters + (identity?.Length ?? 0));
        if (_identityCharacters > _limits.MaximumIdentityCharacters)
        {
            throw new InvalidDataException("The combined global coverage path/name character limit was exceeded.");
        }
    }

    private void ValidateBitmap(byte[]? bitmap)
    {
        if (bitmap is not null && bitmap.Length > _limits.MaximumBitmapBytes)
        {
            throw new InvalidDataException("A combined global coverage bitmap exceeds the per-bitmap limit.");
        }
    }

    private void AddBitmapBytes(int count)
    {
        _bitmapBytes = checked(_bitmapBytes + count);
        if (_bitmapBytes > _limits.MaximumModelBitmapBytes)
        {
            throw new InvalidDataException("The combined global coverage bitmap limit was exceeded.");
        }
    }

    private readonly struct NullableStringKey : IEquatable<NullableStringKey>
    {
        public NullableStringKey(string? value)
        {
            Value = value;
        }

        private string? Value { get; }

        public bool Equals(NullableStringKey other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is NullableStringKey other && Equals(other);

        public override int GetHashCode() => Value is null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    }

    private sealed class ComponentAccumulator
    {
        public ComponentAccumulator(string? name)
        {
            Name = name;
        }

        public string? Name { get; }

        public Dictionary<NullableStringKey, FileAccumulator> Files { get; } = new();

        public List<FileAccumulator> FileOrder { get; } = new();
    }

    private sealed class FileAccumulator
    {
        public FileAccumulator(string? path, byte[]? executableBitmap, byte[]? executedBitmap)
        {
            Path = path;
            ExecutableBitmap = executableBitmap;
            ExecutedBitmap = executedBitmap;
        }

        public string? Path { get; }

        public byte[]? ExecutableBitmap { get; set; }

        public byte[]? ExecutedBitmap { get; set; }
    }
}
