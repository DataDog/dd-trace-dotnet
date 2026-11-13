// <copyright file="OracleActionNameSetter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.DatabaseMonitoring;

/// <summary>
/// Sets the write-only <c>OracleConnection.ActionName</c> property without allocating on each call.
/// The setter is bound once per connection type (the managed and unmanaged ODP.NET drivers have different types)
/// and then called through a cached delegate, so neither commands nor new connection objects allocate.
/// A duck-typed interface proxy would allocate an object on every call.
/// </summary>
internal static class OracleActionNameSetter
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(OracleActionNameSetter));
    private static readonly ConcurrentDictionary<Type, Action<object, string>?> Setters = new();
    private static readonly Func<Type, Action<object, string>?> CreateSetter = Create;

    // Processes normally use a single connection type, so check the last one before the dictionary (a Type lookup costs ~20 ns).
    private static LastSetter? _lastSetter;

    /// <summary>
    /// Gets a delegate that sets ActionName on a connection of the given type.
    /// </summary>
    /// <returns>The delegate, or null if the type has no public writable string ActionName property</returns>
    public static Action<object, string>? Get(Type connectionType)
    {
        if (Volatile.Read(ref _lastSetter) is { } last && last.ConnectionType == connectionType)
        {
            return last.SetActionName;
        }

        var setActionName = Setters.GetOrAdd(connectionType, CreateSetter);
        Volatile.Write(ref _lastSetter, new LastSetter(connectionType, setActionName));
        return setActionName;
    }

    private static Action<object, string>? Create(Type connectionType)
    {
        try
        {
            var setMethod = connectionType.GetProperty("ActionName", BindingFlags.Public | BindingFlags.Instance, binder: null, typeof(string), Type.EmptyTypes, modifiers: null)?.GetSetMethod();
            if (setMethod is null || connectionType.IsValueType)
            {
                return null;
            }

            var method = new DynamicMethod("SetOracleActionName", typeof(void), [typeof(object), typeof(string)], typeof(OracleActionNameSetter).Module, skipVisibility: true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, connectionType);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt, setMethod);
            il.Emit(OpCodes.Ret);
            return (Action<object, string>)method.CreateDelegate(typeof(Action<object, string>));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error binding ActionName on {ConnectionType}", connectionType.FullName);
            return null;
        }
    }

    private sealed class LastSetter(Type connectionType, Action<object, string>? setActionName)
    {
        public Type ConnectionType { get; } = connectionType;

        public Action<object, string>? SetActionName { get; } = setActionName;
    }
}
