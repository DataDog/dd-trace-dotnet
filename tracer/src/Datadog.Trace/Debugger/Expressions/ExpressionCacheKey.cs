// <copyright file="ExpressionCacheKey.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.Debugger.Expressions;

/// <summary>
/// Cache key for compiled expressions. Includes thisType, returnType, and all member RUNTIME types
/// to ensure we recompile when any type changes (polymorphic calls).
/// </summary>
internal readonly struct ExpressionCacheKey : IEquatable<ExpressionCacheKey>
{
    private readonly int _hashCode;
    private readonly Type?[]? _memberRuntimeTypes;

    public ExpressionCacheKey(Type thisType, Type? returnType, ScopeMember[] members)
    {
        ThisType = thisType;
        ReturnType = returnType;

        // Capture RUNTIME types of members (Value.GetType()), not declared types
        // This is critical for polymorphic scenarios where declared type is Object/base
        // but actual values are different concrete types
        var hash = new HashCode();
        hash.Add(thisType);
        hash.Add(returnType);

        if (members != null && members.Length > 0)
        {
            var types = new Type?[members.Length];
            var count = 0;
            foreach (var member in members)
            {
                if (member.Type == null)
                {
                    break; // End of valid members
                }

                // Use runtime type if value exists, else declared type
                var runtimeType = member.Value?.GetType() ?? member.Type;
                types[count++] = runtimeType;
                hash.Add(runtimeType);
            }

            // Trim array to actual count
            if (count > 0)
            {
                if (count < types.Length)
                {
                    Array.Resize(ref types, count);
                }

                _memberRuntimeTypes = types;
            }
            else
            {
                _memberRuntimeTypes = null;
            }
        }
        else
        {
            _memberRuntimeTypes = null;
        }

        _hashCode = hash.ToHashCode();
    }

    public Type ThisType { get; }

    public Type? ReturnType { get; }

    public override int GetHashCode() => _hashCode;

    public override bool Equals(object? obj) => obj is ExpressionCacheKey other && Equals(other);

    public bool Equals(ExpressionCacheKey other)
    {
        // Fast path: hash mismatch means definitely not equal
        if (_hashCode != other._hashCode)
        {
            return false;
        }

        // Must have same ThisType
        if (ThisType != other.ThisType)
        {
            return false;
        }

        // Must have same ReturnType
        if (ReturnType != other.ReturnType)
        {
            return false;
        }

        // Must have same member types (compare actual types, not just hash)
        if (_memberRuntimeTypes == null && other._memberRuntimeTypes == null)
        {
            return true;
        }

        if (_memberRuntimeTypes == null || other._memberRuntimeTypes == null)
        {
            return false;
        }

        if (_memberRuntimeTypes.Length != other._memberRuntimeTypes.Length)
        {
            return false;
        }

        for (int i = 0; i < _memberRuntimeTypes.Length; i++)
        {
            if (_memberRuntimeTypes[i] != other._memberRuntimeTypes[i])
            {
                return false;
            }
        }

        return true;
    }
}
