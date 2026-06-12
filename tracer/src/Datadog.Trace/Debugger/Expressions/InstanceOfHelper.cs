// <copyright file="InstanceOfHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Debugger.Expressions;

internal static class InstanceOfHelper
{
    private const int MaxCachedTypes = 512;
    private const int MaxTrackedMisses = 512;
    private const int MaxResolutionRetries = 3;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(InstanceOfHelper));
    private static readonly string[] EmptyAssemblyNames = [];
    private static readonly ConcurrentDictionary<string, ResolutionState> ResolutionStates = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Type> BclTypeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Array", typeof(Array) },
        { typeof(Array).FullName, typeof(Array) },
        { "bool", typeof(bool) },
        { typeof(bool).FullName, typeof(bool) },
        { "byte", typeof(byte) },
        { typeof(byte).FullName, typeof(byte) },
        { "sbyte", typeof(sbyte) },
        { typeof(sbyte).FullName, typeof(sbyte) },
        { "char", typeof(char) },
        { typeof(char).FullName, typeof(char) },
        { "DateTime", typeof(DateTime) },
        { typeof(DateTime).FullName, typeof(DateTime) },
        { "DateTimeOffset", typeof(DateTimeOffset) },
        { typeof(DateTimeOffset).FullName, typeof(DateTimeOffset) },
        { "decimal", typeof(decimal) },
        { typeof(decimal).FullName, typeof(decimal) },
        { "Delegate", typeof(Delegate) },
        { typeof(Delegate).FullName, typeof(Delegate) },
        { "double", typeof(double) },
        { typeof(double).FullName, typeof(double) },
        { "Enum", typeof(Enum) },
        { typeof(Enum).FullName, typeof(Enum) },
        { "Exception", typeof(Exception) },
        { typeof(Exception).FullName, typeof(Exception) },
        { "float", typeof(float) },
        { typeof(float).FullName, typeof(float) },
        { "Guid", typeof(Guid) },
        { typeof(Guid).FullName, typeof(Guid) },
        { "int", typeof(int) },
        { typeof(int).FullName, typeof(int) },
        { "IntPtr", typeof(IntPtr) },
        { typeof(IntPtr).FullName, typeof(IntPtr) },
        { "uint", typeof(uint) },
        { typeof(uint).FullName, typeof(uint) },
        { "long", typeof(long) },
        { typeof(long).FullName, typeof(long) },
        { "nint", typeof(IntPtr) },
        { "nuint", typeof(UIntPtr) },
        { "ulong", typeof(ulong) },
        { typeof(ulong).FullName, typeof(ulong) },
        { "object", typeof(object) },
        { typeof(object).FullName, typeof(object) },
        { "short", typeof(short) },
        { typeof(short).FullName, typeof(short) },
        { "ushort", typeof(ushort) },
        { typeof(ushort).FullName, typeof(ushort) },
        { "string", typeof(string) },
        { typeof(string).FullName, typeof(string) },
        { "TimeSpan", typeof(TimeSpan) },
        { typeof(TimeSpan).FullName, typeof(TimeSpan) },
        { "Type", typeof(Type) },
        { typeof(Type).FullName, typeof(Type) },
        { "UIntPtr", typeof(UIntPtr) },
        { typeof(UIntPtr).FullName, typeof(UIntPtr) },
        { "ValueType", typeof(ValueType) },
        { typeof(ValueType).FullName, typeof(ValueType) },
    };

    private static Func<Assembly[]> _getAssemblies = AppDomain.CurrentDomain.GetAssemblies;
    private static int _assemblyLoadGeneration;

    static InstanceOfHelper()
    {
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
    }

    internal static bool IsInstanceOf(object value, string typeName)
    {
        var type = ResolveType(typeName);
        var result = type.IsInstanceOfType(value);
        LogSuspiciousMismatch(type, value?.GetType(), typeName, result);
        return result;
    }

    internal static bool IsInstanceOf<TValue>(TValue value, string typeName)
    {
        var type = ResolveType(typeName);
        var valueType = typeof(TValue);
        bool result;
        if (valueType.IsValueType)
        {
            if (Nullable.GetUnderlyingType(valueType) is not null)
            {
                result = type.IsInstanceOfType(value);
                LogSuspiciousMismatch(type, value?.GetType(), typeName, result);
                return result;
            }

            result = type.IsAssignableFrom(valueType);
            LogSuspiciousMismatch(type, valueType, typeName, result);
            return result;
        }

        result = type.IsInstanceOfType(value);
        LogSuspiciousMismatch(type, value?.GetType(), typeName, result);
        return result;
    }

    internal static Type ResolveType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            ThrowInvalidTypeName();
        }

        if (BclTypeAliases.TryGetValue(typeName, out var aliasType))
        {
            return aliasType;
        }

        var observedGeneration = Volatile.Read(ref _assemblyLoadGeneration);
        if (ResolutionStates.TryGetValue(typeName, out var cachedResolution))
        {
            if (cachedResolution.Type is not null || cachedResolution.IsAmbiguous)
            {
                return GetResolvedTypeOrThrow(typeName, cachedResolution, noNewAssemblies: false);
            }

            if (cachedResolution.ScannedGeneration == observedGeneration)
            {
                return GetResolvedTypeOrThrow(typeName, cachedResolution, noNewAssemblies: true);
            }
        }

        var typeNameInfo = ParseTypeName(typeName);
        var resolvedType = typeNameInfo.IsAssemblyQualified
                               ? null
                               : Type.GetType(typeName, throwOnError: false, ignoreCase: true);
        if (resolvedType is not null)
        {
            AddResolvedType(typeName, resolvedType, Volatile.Read(ref _assemblyLoadGeneration));
            return resolvedType;
        }

        if (!IsFullyQualifiedTypeName(typeNameInfo.SearchTypeName))
        {
            ThrowFullyQualifiedNameRequired(typeName);
        }

        return ResolveLoadedType(typeName, typeNameInfo);
    }

    private static Type ResolveLoadedType(string typeName, TypeNameInfo typeNameInfo)
    {
        ResolutionState lastResolution = null;
        for (var i = 0; i < MaxResolutionRetries; i++)
        {
            var observedGeneration = Volatile.Read(ref _assemblyLoadGeneration);
            ResolutionStates.TryGetValue(typeName, out var currentResolution);
            if (currentResolution is not null)
            {
                if (currentResolution.Type is not null || currentResolution.IsAmbiguous)
                {
                    return GetResolvedTypeOrThrow(typeName, currentResolution, noNewAssemblies: false);
                }

                if (currentResolution.ScannedGeneration == observedGeneration)
                {
                    return GetResolvedTypeOrThrow(typeName, currentResolution, noNewAssemblies: true);
                }
            }

            var assemblies = Volatile.Read(ref _getAssemblies)();
            var scanResult = ScanAssemblies(typeName, typeNameInfo, assemblies, currentResolution?.ScannedAssemblies, currentResolution?.Type);
            lastResolution = AddScannedResolution(typeName, currentResolution, scanResult, observedGeneration);
            if (lastResolution.IsAmbiguous)
            {
                ThrowAmbiguousType(typeName);
            }

            if (lastResolution.Type is not null)
            {
                return lastResolution.Type;
            }

            if (observedGeneration != Volatile.Read(ref _assemblyLoadGeneration))
            {
                continue;
            }

            ThrowUnknownType(typeName);
        }

        if (lastResolution is not null)
        {
            return GetResolvedTypeOrThrow(typeName, lastResolution, noNewAssemblies: false);
        }

        ThrowUnknownType(typeName);
        return null;
    }

    private static ScanResult ScanAssemblies(string typeName, TypeNameInfo typeNameInfo, Assembly[] assemblies, string[] alreadyScannedAssemblies, Type currentResolvedType)
    {
        Type resolvedType = null;
        var isAmbiguous = false;
        var scannedAssemblies = new List<string>();
        var alreadyScannedAssemblySet = alreadyScannedAssemblies is null || alreadyScannedAssemblies.Length == 0
                                            ? null
                                            : new HashSet<string>(alreadyScannedAssemblies, StringComparer.Ordinal);

        for (var i = 0; i < assemblies.Length; i++)
        {
            var assembly = assemblies[i];
            var assemblyIdentity = GetAssemblyIdentity(assembly);
            if (alreadyScannedAssemblySet?.Contains(assemblyIdentity) == true)
            {
                continue;
            }

            scannedAssemblies.Add(assemblyIdentity);
            var matchedType = TryResolveFromAssembly(typeName, typeNameInfo, assembly);
            if (matchedType is null)
            {
                continue;
            }

            var previousMatch = currentResolvedType ?? resolvedType;
            if (previousMatch is not null && previousMatch != matchedType)
            {
                isAmbiguous = true;
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Ambiguous case-insensitive type lookup for '{TypeName}'. Matched '{FirstType}' and '{SecondType}'. Use exact type casing or an assembly-qualified name.", typeName, previousMatch.AssemblyQualifiedName, matchedType.AssemblyQualifiedName);
                }

                continue;
            }

            resolvedType = matchedType;
        }

        return new ScanResult(resolvedType, isAmbiguous, scannedAssemblies.Count == 0 ? EmptyAssemblyNames : scannedAssemblies.ToArray());
    }

    private static Type TryResolveFromAssembly(string typeName, TypeNameInfo typeNameInfo, Assembly assembly)
    {
        if (typeNameInfo.IsAssemblyQualified && !IsMatchingAssembly(assembly, typeNameInfo.AssemblyName))
        {
            return null;
        }

        Type type;
        try
        {
            type = assembly.GetType(typeNameInfo.SearchTypeName, throwOnError: false, ignoreCase: true);
        }
        catch (AmbiguousMatchException ex)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(ex, "Ambiguous case-insensitive type lookup for '{TypeName}' in assembly '{AssemblyName}'. Use exact type casing or an assembly-qualified name.", typeName, assembly.FullName);
            }

            throw new InstanceOfEvaluationException($"Multiple types matching '{typeName}' were found using case-insensitive lookup. Use exact type casing or an assembly-qualified name.", ex);
        }
        catch (Exception ex)
        {
            throw new InstanceOfEvaluationException($"Failed to inspect assembly '{assembly.FullName}' while resolving type '{typeName}': {ex.Message}", ex);
        }

        return type;
    }

    private static bool IsFullyQualifiedTypeName(string typeName)
    {
        return typeName.IndexOf('.') >= 0;
    }

    private static void LogSuspiciousMismatch(Type resolvedType, Type valueType, string requestedTypeName, bool result)
    {
        if (result ||
            valueType is null ||
            !Log.IsEnabled(LogEventLevel.Debug))
        {
            return;
        }

        var typeNameInfo = ParseTypeName(requestedTypeName);
        if (typeNameInfo.IsAssemblyQualified ||
            !string.Equals(valueType.FullName, typeNameInfo.SearchTypeName, StringComparison.OrdinalIgnoreCase) ||
            resolvedType == valueType)
        {
            return;
        }

        Log.Debug("Instanceof lookup for '{TypeName}' resolved to '{ResolvedType}', but the runtime value type is '{ValueType}'. The namespace-qualified name may exist in multiple assemblies; use an assembly-qualified name to disambiguate.", requestedTypeName, resolvedType.AssemblyQualifiedName, valueType.AssemblyQualifiedName);
    }

    private static TypeNameInfo ParseTypeName(string typeName)
    {
        var assemblySeparatorIndex = GetAssemblySeparatorIndex(typeName);
        if (assemblySeparatorIndex < 0)
        {
            return new TypeNameInfo(typeName, null);
        }

        return new TypeNameInfo(
            typeName.Substring(0, assemblySeparatorIndex).Trim(),
            typeName.Substring(assemblySeparatorIndex + 1).Trim());
    }

    private static int GetAssemblySeparatorIndex(string typeName)
    {
        var bracketDepth = 0;
        for (var i = 0; i < typeName.Length; i++)
        {
            switch (typeName[i])
            {
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth--;
                    break;
                case ',' when bracketDepth == 0:
                    return i;
            }
        }

        return -1;
    }

    private static bool IsMatchingAssembly(Assembly assembly, string assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
        {
            return false;
        }

        return string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal) ||
               string.Equals(assembly.FullName, assemblyName, StringComparison.Ordinal);
    }

    private static string GetAssemblyIdentity(Assembly assembly)
    {
        return string.Concat(assembly.FullName, "|", RuntimeHelpers.GetHashCode(assembly).ToString());
    }

    private static Type GetResolvedTypeOrThrow(string typeName, ResolutionState resolution, bool noNewAssemblies)
    {
        if (resolution.IsAmbiguous)
        {
            ThrowAmbiguousType(typeName);
        }

        if (resolution.Type is not null)
        {
            return resolution.Type;
        }

        if (noNewAssemblies)
        {
            ThrowUnknownTypeNoNewAssemblies(typeName);
        }

        ThrowUnknownType(typeName);
        return null;
    }

    private static void AddResolvedType(string typeName, Type type, int scannedGeneration)
    {
        ClearCacheIfNeeded();
        var newResolution = new ResolutionState(type, isAmbiguous: false, scannedGeneration, EmptyAssemblyNames);
        ResolutionStates.AddOrUpdate(
            typeName,
            newResolution,
            (_, currentResolution) => MergeResolvedType(typeName, currentResolution, type, scannedGeneration));
    }

    private static ResolutionState AddScannedResolution(string typeName, ResolutionState currentResolution, ScanResult scanResult, int scannedGeneration)
    {
        ClearCacheIfNeeded();
        var newResolution = currentResolution is null
                                ? new ResolutionState(scanResult.Type, scanResult.IsAmbiguous, scannedGeneration, scanResult.ScannedAssemblies)
                                : MergeScannedResolution(typeName, currentResolution, scanResult, scannedGeneration);

        return ResolutionStates.AddOrUpdate(
            typeName,
            newResolution,
            (_, currentResolution) => MergeScannedResolution(typeName, currentResolution, scanResult, scannedGeneration));
    }

    private static ResolutionState MergeResolvedType(string typeName, ResolutionState currentResolution, Type resolvedType, int scannedGeneration)
    {
        var isAmbiguous = currentResolution.IsAmbiguous;
        var type = currentResolution.Type ?? resolvedType;
        if (currentResolution.Type is not null && currentResolution.Type != resolvedType)
        {
            isAmbiguous = true;
        }

        return new ResolutionState(type, isAmbiguous, Math.Max(currentResolution.ScannedGeneration, scannedGeneration), currentResolution.ScannedAssemblies);
    }

    private static ResolutionState MergeScannedResolution(string typeName, ResolutionState currentResolution, ScanResult scanResult, int scannedGeneration)
    {
        var isAmbiguous = currentResolution.IsAmbiguous || scanResult.IsAmbiguous;
        var type = currentResolution.Type ?? scanResult.Type;
        if (currentResolution.Type is not null &&
            scanResult.Type is not null &&
            currentResolution.Type != scanResult.Type)
        {
            isAmbiguous = true;
        }

        return new ResolutionState(type, isAmbiguous, Math.Max(currentResolution.ScannedGeneration, scannedGeneration), MergeScannedAssemblies(currentResolution.ScannedAssemblies, scanResult.ScannedAssemblies));
    }

    private static string[] MergeScannedAssemblies(string[] currentAssemblies, string[] scannedAssemblies)
    {
        if (currentAssemblies is null || currentAssemblies.Length == 0)
        {
            return scannedAssemblies is null || scannedAssemblies.Length == 0 ? EmptyAssemblyNames : scannedAssemblies;
        }

        if (scannedAssemblies is null || scannedAssemblies.Length == 0)
        {
            return currentAssemblies;
        }

        var merged = new HashSet<string>(currentAssemblies, StringComparer.Ordinal);
        for (var i = 0; i < scannedAssemblies.Length; i++)
        {
            merged.Add(scannedAssemblies[i]);
        }

        var result = new string[merged.Count];
        merged.CopyTo(result);
        return result;
    }

    private static void ClearCacheIfNeeded()
    {
        if (ResolutionStates.Count >= MaxCachedTypes ||
            ResolutionStates.Count >= MaxTrackedMisses)
        {
            ResolutionStates.Clear();
        }
    }

    private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
    {
        Interlocked.Increment(ref _assemblyLoadGeneration);
    }

    internal static void ResetForTests()
    {
        ResolutionStates.Clear();
        Volatile.Write(ref _getAssemblies, AppDomain.CurrentDomain.GetAssemblies);
        Interlocked.Exchange(ref _assemblyLoadGeneration, 0);
    }

    internal static void SetAssemblyProviderForTests(Func<Assembly[]> getAssemblies)
    {
        ResolutionStates.Clear();
        Volatile.Write(ref _getAssemblies, getAssemblies ?? AppDomain.CurrentDomain.GetAssemblies);
        Interlocked.Exchange(ref _assemblyLoadGeneration, 0);
    }

    internal static void IncrementAssemblyLoadGenerationForTests()
    {
        Interlocked.Increment(ref _assemblyLoadGeneration);
    }

    private static void ThrowInvalidTypeName()
    {
        throw new InstanceOfEvaluationException("failed to parse type name");
    }

    private static void ThrowFullyQualifiedNameRequired(string typeName)
    {
        throw new InstanceOfEvaluationException($"'{typeName}' must be a fully qualified type name");
    }

    private static void ThrowUnknownType(string typeName)
    {
        throw new InstanceOfEvaluationException($"'{typeName}' is unknown type");
    }

    private static void ThrowUnknownTypeNoNewAssemblies(string typeName)
    {
        throw new InstanceOfEvaluationException($"'{typeName}' is unknown type. No new assemblies were loaded since the last lookup.");
    }

    private static void ThrowAmbiguousType(string typeName)
    {
        throw new InstanceOfEvaluationException($"Multiple types matching '{typeName}' were found using case-insensitive lookup. Use exact type casing or an assembly-qualified name.");
    }

    private readonly struct ScanResult
    {
        internal ScanResult(Type type, bool isAmbiguous, string[] scannedAssemblies)
        {
            Type = type;
            IsAmbiguous = isAmbiguous;
            ScannedAssemblies = scannedAssemblies;
        }

        internal Type Type { get; }

        internal bool IsAmbiguous { get; }

        internal string[] ScannedAssemblies { get; }
    }

    private readonly struct TypeNameInfo
    {
        internal TypeNameInfo(string searchTypeName, string assemblyName)
        {
            SearchTypeName = searchTypeName;
            AssemblyName = assemblyName;
        }

        internal string SearchTypeName { get; }

        internal string AssemblyName { get; }

        internal bool IsAssemblyQualified => AssemblyName is not null;
    }

    private sealed class ResolutionState
    {
        internal ResolutionState(Type type, bool isAmbiguous, int scannedGeneration, string[] scannedAssemblies)
        {
            Type = type;
            IsAmbiguous = isAmbiguous;
            ScannedGeneration = scannedGeneration;
            ScannedAssemblies = scannedAssemblies ?? EmptyAssemblyNames;
        }

        internal Type Type { get; }

        internal bool IsAmbiguous { get; }

        internal int ScannedGeneration { get; }

        internal string[] ScannedAssemblies { get; }
    }

    internal sealed class InstanceOfEvaluationException : Exception
    {
        internal InstanceOfEvaluationException(string message)
            : base(message)
        {
        }

        internal InstanceOfEvaluationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
