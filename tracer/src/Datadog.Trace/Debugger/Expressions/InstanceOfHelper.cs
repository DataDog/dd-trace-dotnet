// <copyright file="InstanceOfHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Debugger.Expressions;

internal static class InstanceOfHelper
{
    private const int MaxCachedTypes = 512;
    private const int MaxResolutionRetries = 3;
    private const int LinearAssemblyIdentityScanThreshold = 8;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(InstanceOfHelper));
    private static readonly AssemblyIdentity[] EmptyAssemblyIdentities = [];
    private static readonly ConcurrentDictionary<string, ResolutionState> ResolutionStates = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Type> BclTypeAliases = CreateBclTypeAliases();

    private static Func<Assembly[]> _getAssemblies = AppDomain.CurrentDomain.GetAssemblies;
    private static int _assemblyLoadGeneration;

    static InstanceOfHelper()
    {
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
    }

    internal static bool IsInstanceOf(object? value, string typeName)
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
                if (!result && Log.IsEnabled(LogEventLevel.Debug))
                {
                    LogSuspiciousMismatch(type, value?.GetType(), typeName, result);
                }

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
        if (StringUtil.IsNullOrEmpty(typeName))
        {
            ThrowInvalidTypeName();
        }

        if (TryResolveBclTypeAlias(typeName, out var aliasType))
        {
            return aliasType;
        }

        var observedGeneration = Volatile.Read(ref _assemblyLoadGeneration);
        ResolutionStates.TryGetValue(typeName, out var cachedResolution);
        if (TryGetCachedResolution(typeName, observedGeneration, ref cachedResolution, out var cachedType))
        {
            return cachedType;
        }

        var typeNameInfo = ParseTypeName(typeName);
        if (typeNameInfo.IsAssemblyQualified)
        {
            return AssemblyQualifiedTypeResolver.Resolve(typeName, typeNameInfo);
        }

        if (MayResolveWithoutAssemblyScan(typeNameInfo.SearchTypeName) &&
            !typeNameInfo.MayContainAssemblyQualifiedGenericArguments)
        {
            var resolvedType = Type.GetType(typeName, throwOnError: false, ignoreCase: false);
            if (resolvedType is not null)
            {
                AddResolvedType(typeName, resolvedType, Volatile.Read(ref _assemblyLoadGeneration));
                return resolvedType;
            }
        }

        if (!IsFullyQualifiedTypeName(typeNameInfo.SearchTypeName))
        {
            ThrowFullyQualifiedNameRequired(typeName);
        }

        return ResolveLoadedType(typeName, typeNameInfo);
    }

    private static Type ResolveLoadedType(string typeName, TypeNameInfo typeNameInfo)
    {
        ResolutionState? lastResolution = null;
        for (var i = 0; i < MaxResolutionRetries; i++)
        {
            var observedGeneration = Volatile.Read(ref _assemblyLoadGeneration);
            ResolutionStates.TryGetValue(typeName, out var currentResolution);
            if (TryGetCachedResolution(typeName, observedGeneration, ref currentResolution, out var cachedType))
            {
                return cachedType;
            }

            var assemblies = Volatile.Read(ref _getAssemblies)();
            var alreadyScannedAssemblies = typeNameInfo.MayContainAssemblyQualifiedGenericArguments
                                               ? null
                                               : currentResolution?.ScannedAssemblies;
            var scanResult = ScanAssemblies(typeName, typeNameInfo, assemblies, alreadyScannedAssemblies, currentResolution?.GetTypeOrDefault());
            lastResolution = AddScannedResolution(typeName, currentResolution, scanResult, observedGeneration);
            if (lastResolution.IsAmbiguous)
            {
                ThrowAmbiguousType(typeName);
            }

            if (lastResolution.TryGetType(out var lastType))
            {
                return lastType;
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

        throw UnknownType(typeName);
    }

    private static ScanResult ScanAssemblies(string typeName, TypeNameInfo typeNameInfo, Assembly[] assemblies, AssemblyIdentity[]? alreadyScannedAssemblies, Type? currentResolvedType)
    {
        Type? resolvedType = null;
        var isAmbiguous = false;
        AssemblyIdentity[]? scannedAssemblies = null;
        var scannedAssemblyCount = 0;
        var alreadyScannedAssemblySet = alreadyScannedAssemblies is null || alreadyScannedAssemblies.Length == 0
                                            ? null
                                            : alreadyScannedAssemblies.Length > LinearAssemblyIdentityScanThreshold
                                                ? new HashSet<AssemblyIdentity>(alreadyScannedAssemblies)
                                                : null;

        for (var i = 0; i < assemblies.Length; i++)
        {
            var assembly = assemblies[i];
            var assemblyIdentity = new AssemblyIdentity(assembly);
            if (ContainsAssemblyIdentity(alreadyScannedAssemblies, alreadyScannedAssemblySet, assemblyIdentity))
            {
                continue;
            }

            AddAssemblyIdentity(ref scannedAssemblies, ref scannedAssemblyCount, assemblies.Length, assemblyIdentity);
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
                    Log.Debug("Ambiguous type lookup for '{TypeName}'. Matched '{FirstType}' and '{SecondType}'. Use an assembly-qualified name.", typeName, previousMatch.AssemblyQualifiedName, matchedType.AssemblyQualifiedName);
                }

                continue;
            }

            resolvedType = matchedType;
        }

        return new ScanResult(resolvedType, isAmbiguous, ToExactAssemblyIdentityArray(scannedAssemblies, scannedAssemblyCount));
    }

    private static Type? TryResolveFromAssembly(string typeName, TypeNameInfo typeNameInfo, Assembly assembly)
    {
        if (typeNameInfo.AssemblyName is { } assemblyName &&
            !AssemblyQualifiedTypeResolver.IsMatchingAssembly(assembly, assemblyName))
        {
            return null;
        }

        try
        {
            var searchTypeName = typeNameInfo.SearchTypeName;
            if (!typeNameInfo.MayContainAssemblyQualifiedGenericArguments)
            {
                return assembly.GetType(searchTypeName, throwOnError: false, ignoreCase: false);
            }

            return ResolveConstructedGenericFromLoadedAssemblies(searchTypeName, assembly);
        }
        catch (AmbiguousMatchException ex)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(ex, "Ambiguous type lookup for '{TypeName}' in assembly '{AssemblyName}'. Use an assembly-qualified name.", typeName, assembly.FullName);
            }

            throw new InstanceOfEvaluationException($"Multiple types matching '{typeName}' were found. Use an assembly-qualified name.", ex);
        }
        catch (Exception ex) when (IsAssemblyInspectionException(ex))
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(ex, "Failed to inspect assembly '{AssemblyName}' while resolving type '{TypeName}'. Continuing assembly scan.", assembly.FullName, typeName);
            }

            return null;
        }
    }

    private static bool IsAssemblyInspectionException(Exception exception)
    {
        return exception is TypeLoadException or FileNotFoundException or FileLoadException or BadImageFormatException or ReflectionTypeLoadException;
    }

    private static Type? ResolveConstructedGenericFromLoadedAssemblies(string searchTypeName, Assembly assembly)
    {
        return Type.GetType(
            searchTypeName,
            AssemblyQualifiedTypeResolver.ResolveGenericArgumentAssembly,
            (resolvedAssembly, nestedTypeName, nestedIgnoreCase) => ResolveTypePartFromLoadedAssembly(resolvedAssembly ?? assembly, nestedTypeName, nestedIgnoreCase),
            throwOnError: false,
            ignoreCase: false);
    }

    private static Type? ResolveTypePartFromLoadedAssembly(Assembly assembly, string typeName, bool ignoreCase)
    {
        return MayContainAssemblyQualifiedGenericArguments(typeName)
                   ? ResolveConstructedGenericFromLoadedAssemblies(typeName, assembly)
                   : assembly.GetType(typeName, throwOnError: false, ignoreCase: ignoreCase);
    }

    private static bool MayContainAssemblyQualifiedGenericArguments(string typeName)
    {
        return typeName.IndexOf("[[", StringComparison.Ordinal) >= 0;
    }

    private static bool IsFullyQualifiedTypeName(string typeName)
    {
        return typeName.IndexOf('.') >= 0;
    }

    private static void LogSuspiciousMismatch(Type resolvedType, Type? valueType, string requestedTypeName, bool result)
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
        if (typeName.IndexOf(',') < 0)
        {
            return new TypeNameInfo(typeName, null, mayContainAssemblyQualifiedGenericArguments: false);
        }

        var assemblySeparatorIndex = GetAssemblySeparatorIndex(typeName);
        if (assemblySeparatorIndex < 0)
        {
            return new TypeNameInfo(typeName, null, MayContainAssemblyQualifiedGenericArguments(typeName));
        }

        var searchTypeName = typeName.Substring(0, assemblySeparatorIndex).Trim();
        return new TypeNameInfo(
            searchTypeName,
            typeName.Substring(assemblySeparatorIndex + 1).Trim(),
            MayContainAssemblyQualifiedGenericArguments(searchTypeName));
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

    private static bool ContainsAssemblyIdentity(AssemblyIdentity[]? identities, HashSet<AssemblyIdentity>? identitySet, AssemblyIdentity identity)
    {
        if (identitySet is not null)
        {
            return identitySet.Contains(identity);
        }

        if (identities is null)
        {
            return false;
        }

        for (var i = 0; i < identities.Length; i++)
        {
            if (identities[i].Equals(identity))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAssemblyIdentity(AssemblyIdentity[] identities, int count, AssemblyIdentity identity)
    {
        for (var i = 0; i < count; i++)
        {
            if (identities[i].Equals(identity))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddAssemblyIdentity(ref AssemblyIdentity[]? identities, ref int count, int maxCapacity, AssemblyIdentity identity)
    {
        if (identities is null)
        {
            identities = new AssemblyIdentity[Math.Min(maxCapacity, LinearAssemblyIdentityScanThreshold)];
        }
        else if (count == identities.Length)
        {
            var newLength = Math.Min(maxCapacity, identities.Length * 2);
            var resizedIdentities = new AssemblyIdentity[newLength];
            Array.Copy(identities, resizedIdentities, identities.Length);
            identities = resizedIdentities;
        }

        identities[count] = identity;
        count++;
    }

    private static AssemblyIdentity[] ToExactAssemblyIdentityArray(AssemblyIdentity[]? identities, int count)
    {
        if (identities is null || count == 0)
        {
            return EmptyAssemblyIdentities;
        }

        if (count == identities.Length)
        {
            return identities;
        }

        var result = new AssemblyIdentity[count];
        Array.Copy(identities, result, count);
        return result;
    }

    private static Dictionary<string, Type> CreateBclTypeAliases()
    {
        var aliases = new Dictionary<string, Type>(StringComparer.Ordinal);
        AddBclType(aliases, typeof(Array), "Array");
        AddBclType(aliases, typeof(bool), "bool");
        AddBclType(aliases, typeof(byte), "byte");
        AddBclType(aliases, typeof(sbyte), "sbyte");
        AddBclType(aliases, typeof(char), "char");
        AddBclType(aliases, typeof(DateTime), "DateTime");
        AddBclType(aliases, typeof(DateTimeOffset), "DateTimeOffset");
        AddBclType(aliases, typeof(decimal), "decimal");
        AddBclType(aliases, typeof(Delegate), "Delegate");
        AddBclType(aliases, typeof(double), "double");
        AddBclType(aliases, typeof(Enum), "Enum");
        AddBclType(aliases, typeof(Exception), "Exception");
        AddBclType(aliases, typeof(float), "float");
        AddBclType(aliases, typeof(Guid), "Guid");
        AddBclType(aliases, typeof(int), "int");
        AddBclType(aliases, typeof(IntPtr), "IntPtr", "nint");
        AddBclType(aliases, typeof(uint), "uint");
        AddBclType(aliases, typeof(long), "long");
        AddBclType(aliases, typeof(UIntPtr), "UIntPtr", "nuint");
        AddBclType(aliases, typeof(ulong), "ulong");
        AddBclType(aliases, typeof(object), "object");
        AddBclType(aliases, typeof(short), "short");
        AddBclType(aliases, typeof(ushort), "ushort");
        AddBclType(aliases, typeof(string), "string");
        AddBclType(aliases, typeof(TimeSpan), "TimeSpan");
        AddBclType(aliases, typeof(Type), "Type");
        AddBclType(aliases, typeof(ValueType), "ValueType");
        return aliases;
    }

    private static bool TryResolveBclTypeAlias(string typeName, [NotNullWhen(true)] out Type? type)
    {
        type = null;
        if (!MayBeBclTypeAlias(typeName))
        {
            return false;
        }

        return BclTypeAliases.TryGetValue(typeName, out type);
    }

    private static bool MayBeBclTypeAlias(string typeName)
    {
        return typeName.IndexOf('.') < 0 ||
               typeName.StartsWith("System.", StringComparison.Ordinal);
    }

    private static bool MayResolveWithoutAssemblyScan(string typeName)
    {
        return typeName.StartsWith("System.", StringComparison.Ordinal);
    }

    private static void AddBclType(Dictionary<string, Type> aliases, Type type, params string[] names)
    {
        if (type.FullName is { } fullName)
        {
            aliases[fullName] = type;
        }

        for (var i = 0; i < names.Length; i++)
        {
            aliases[names[i]] = type;
        }
    }

    private static bool TryGetCachedResolution(string typeName, int observedGeneration, ref ResolutionState? resolution, [NotNullWhen(true)] out Type? type)
    {
        type = null;
        if (resolution is null)
        {
            return false;
        }

        if (resolution.IsAmbiguous)
        {
            ThrowAmbiguousType(typeName);
        }

        if (resolution.TryGetType(out type))
        {
            return true;
        }

        if (resolution.HasResolvedType)
        {
            RemoveResolutionIfCurrent(typeName, resolution);
            resolution = null;
        }
        else if (resolution.ScannedGeneration == observedGeneration)
        {
            ThrowUnknownTypeNoNewAssemblies(typeName);
        }

        return false;
    }

    private static Type GetResolvedTypeOrThrow(string typeName, ResolutionState resolution, bool noNewAssemblies)
    {
        if (resolution.IsAmbiguous)
        {
            ThrowAmbiguousType(typeName);
        }

        if (resolution.TryGetType(out var type))
        {
            return type;
        }

        if (noNewAssemblies)
        {
            ThrowUnknownTypeNoNewAssemblies(typeName);
        }

        throw UnknownType(typeName);
    }

    private static void AddResolvedType(string typeName, Type type, int scannedGeneration)
    {
        ClearCacheIfNeeded();
        var newResolution = new ResolutionState(type, isAmbiguous: false, scannedGeneration, EmptyAssemblyIdentities);
        while (true)
        {
            if (!ResolutionStates.TryGetValue(typeName, out var currentResolution))
            {
                if (ResolutionStates.TryAdd(typeName, newResolution))
                {
                    return;
                }

                continue;
            }

            var mergedResolution = MergeResolvedType(typeName, currentResolution, type, scannedGeneration);
            if (ResolutionStates.TryUpdate(typeName, mergedResolution, currentResolution))
            {
                return;
            }
        }
    }

    private static ResolutionState AddScannedResolution(string typeName, ResolutionState? currentResolution, ScanResult scanResult, int scannedGeneration)
    {
        ClearCacheIfNeeded();
        var newResolution = currentResolution is null
                                ? new ResolutionState(scanResult.Type, scanResult.IsAmbiguous, scannedGeneration, scanResult.ScannedAssemblies)
                                : MergeScannedResolution(typeName, currentResolution, scanResult, scannedGeneration);

        while (true)
        {
            if (!ResolutionStates.TryGetValue(typeName, out var latestResolution))
            {
                if (ResolutionStates.TryAdd(typeName, newResolution))
                {
                    return newResolution;
                }

                continue;
            }

            var mergedResolution = MergeScannedResolution(typeName, latestResolution, scanResult, scannedGeneration);
            if (ResolutionStates.TryUpdate(typeName, mergedResolution, latestResolution))
            {
                return mergedResolution;
            }
        }
    }

    private static ResolutionState MergeResolvedType(string typeName, ResolutionState currentResolution, Type resolvedType, int scannedGeneration)
    {
        var isAmbiguous = currentResolution.IsAmbiguous;
        var hasCurrentType = currentResolution.TryGetType(out var currentType);
        if (currentResolution.HasResolvedType && !hasCurrentType)
        {
            return new ResolutionState(resolvedType, isAmbiguous, scannedGeneration, EmptyAssemblyIdentities);
        }

        var type = currentType ?? resolvedType;
        if (currentType is not null && currentType != resolvedType)
        {
            isAmbiguous = true;
        }

        return new ResolutionState(type, isAmbiguous, Math.Max(currentResolution.ScannedGeneration, scannedGeneration), currentResolution.ScannedAssemblies);
    }

    private static ResolutionState MergeScannedResolution(string typeName, ResolutionState currentResolution, ScanResult scanResult, int scannedGeneration)
    {
        var isAmbiguous = currentResolution.IsAmbiguous || scanResult.IsAmbiguous;
        var hasCurrentType = currentResolution.TryGetType(out var currentType);
        if (currentResolution.HasResolvedType && !hasCurrentType)
        {
            return new ResolutionState(scanResult.Type, scanResult.IsAmbiguous, scannedGeneration, scanResult.ScannedAssemblies);
        }

        var type = currentType ?? scanResult.Type;
        if (currentType is not null &&
            scanResult.Type is not null &&
            currentType != scanResult.Type)
        {
            isAmbiguous = true;
        }

        return new ResolutionState(type, isAmbiguous, Math.Max(currentResolution.ScannedGeneration, scannedGeneration), MergeScannedAssemblies(currentResolution.ScannedAssemblies, scanResult.ScannedAssemblies));
    }

    private static void RemoveResolutionIfCurrent(string typeName, ResolutionState resolution)
    {
        ((ICollection<KeyValuePair<string, ResolutionState>>)ResolutionStates).Remove(new KeyValuePair<string, ResolutionState>(typeName, resolution));
    }

    private static AssemblyIdentity[] MergeScannedAssemblies(AssemblyIdentity[] currentAssemblies, AssemblyIdentity[] scannedAssemblies)
    {
        if (currentAssemblies is null || currentAssemblies.Length == 0)
        {
            return scannedAssemblies is null || scannedAssemblies.Length == 0 ? EmptyAssemblyIdentities : scannedAssemblies;
        }

        if (scannedAssemblies is null || scannedAssemblies.Length == 0)
        {
            return currentAssemblies;
        }

        var maxCapacity = currentAssemblies.Length + scannedAssemblies.Length;
        if (maxCapacity > LinearAssemblyIdentityScanThreshold)
        {
            return MergeScannedAssembliesWithSet(currentAssemblies, scannedAssemblies, maxCapacity);
        }

        var merged = new AssemblyIdentity[maxCapacity];
        var count = 0;
        for (var i = 0; i < currentAssemblies.Length; i++)
        {
            merged[count] = currentAssemblies[i];
            count++;
        }

        for (var i = 0; i < scannedAssemblies.Length; i++)
        {
            var scannedAssembly = scannedAssemblies[i];
            if (!ContainsAssemblyIdentity(merged, count, scannedAssembly))
            {
                merged[count] = scannedAssembly;
                count++;
            }
        }

        return ToExactAssemblyIdentityArray(merged, count);
    }

    private static AssemblyIdentity[] MergeScannedAssembliesWithSet(AssemblyIdentity[] currentAssemblies, AssemblyIdentity[] scannedAssemblies, int maxCapacity)
    {
        var mergedSet = new HashSet<AssemblyIdentity>(currentAssemblies);
        var merged = new AssemblyIdentity[maxCapacity];
        Array.Copy(currentAssemblies, merged, currentAssemblies.Length);
        var count = currentAssemblies.Length;

        for (var i = 0; i < scannedAssemblies.Length; i++)
        {
            var scannedAssembly = scannedAssemblies[i];
            if (mergedSet.Add(scannedAssembly))
            {
                merged[count] = scannedAssembly;
                count++;
            }
        }

        return ToExactAssemblyIdentityArray(merged, count);
    }

    private static void ClearCacheIfNeeded()
    {
        if (ResolutionStates.Count >= MaxCachedTypes)
        {
            ResolutionStates.Clear();
        }
    }

    private static void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
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
        throw UnknownType(typeName);
    }

    private static InstanceOfEvaluationException UnknownType(string typeName)
    {
        return new InstanceOfEvaluationException($"'{typeName}' is unknown type");
    }

    private static void ThrowUnknownTypeNoNewAssemblies(string typeName)
    {
        throw new InstanceOfEvaluationException($"'{typeName}' is unknown type. No new assemblies were loaded since the last lookup.");
    }

    private static void ThrowAmbiguousType(string typeName)
    {
        throw new InstanceOfEvaluationException($"Multiple types matching '{typeName}' were found. Use an assembly-qualified name.");
    }

    private readonly struct ScanResult
    {
        internal ScanResult(Type? type, bool isAmbiguous, AssemblyIdentity[] scannedAssemblies)
        {
            Type = type;
            IsAmbiguous = isAmbiguous;
            ScannedAssemblies = scannedAssemblies;
        }

        internal Type? Type { get; }

        internal bool IsAmbiguous { get; }

        internal AssemblyIdentity[] ScannedAssemblies { get; }
    }

    private readonly struct AssemblyIdentity : IEquatable<AssemblyIdentity>
    {
        private readonly string? _fullName;
        private readonly int _runtimeHashCode;

        internal AssemblyIdentity(Assembly assembly)
        {
            _fullName = assembly.FullName;
            _runtimeHashCode = RuntimeHelpers.GetHashCode(assembly);
        }

        public bool Equals(AssemblyIdentity other)
        {
            return _runtimeHashCode == other._runtimeHashCode &&
                   string.Equals(_fullName, other._fullName, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is AssemblyIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _runtimeHashCode;
        }
    }

    private readonly struct TypeNameInfo
    {
        internal TypeNameInfo(string searchTypeName, string? assemblyName, bool mayContainAssemblyQualifiedGenericArguments)
        {
            SearchTypeName = searchTypeName;
            AssemblyName = assemblyName;
            MayContainAssemblyQualifiedGenericArguments = mayContainAssemblyQualifiedGenericArguments;
        }

        internal string SearchTypeName { get; }

        internal string? AssemblyName { get; }

        internal bool MayContainAssemblyQualifiedGenericArguments { get; }

        internal bool IsAssemblyQualified => AssemblyName is not null;
    }

    private sealed class AssemblyQualifiedTypeResolver
    {
        internal static Type Resolve(string typeName, TypeNameInfo typeNameInfo)
        {
            if (TryResolveKnownFrameworkType(typeName, typeNameInfo, out var frameworkType))
            {
                AddResolvedType(typeName, frameworkType, Volatile.Read(ref _assemblyLoadGeneration));
                return frameworkType;
            }

            if (!IsFullyQualifiedTypeName(typeNameInfo.SearchTypeName))
            {
                ThrowFullyQualifiedNameRequired(typeName);
            }

            return ResolveLoadedType(typeName, typeNameInfo);
        }

        internal static bool IsMatchingAssembly(Assembly assembly, string assemblyName)
        {
            if (StringUtil.IsNullOrEmpty(assemblyName))
            {
                return false;
            }

            var fullName = assembly.FullName;
            return fullName is not null &&
                   (string.Equals(fullName, assemblyName, StringComparison.Ordinal) ||
                    AssemblySimpleNameEquals(fullName, assemblyName));
        }

        private static bool TryResolveKnownFrameworkType(string typeName, TypeNameInfo typeNameInfo, [NotNullWhen(true)] out Type? type)
        {
            type = null;
            if (typeNameInfo.AssemblyName is not { } assemblyName ||
                !IsKnownFrameworkAssemblyName(assemblyName))
            {
                return false;
            }

            type = typeNameInfo.MayContainAssemblyQualifiedGenericArguments
                       ? ResolveConstructedGenericFromKnownFrameworkAssemblies(typeNameInfo.SearchTypeName, ignoreCase: false)
                       : typeof(object).Assembly.GetType(typeNameInfo.SearchTypeName, throwOnError: false, ignoreCase: false);
            if (type is not null)
            {
                return true;
            }

            if (!IsFullyQualifiedTypeName(typeNameInfo.SearchTypeName))
            {
                return false;
            }

            type = Type.GetType(typeName, ResolveLoadedAssembly, typeResolver: null, throwOnError: false, ignoreCase: false);
            return type is not null;
        }

        private static Type? ResolveConstructedGenericFromKnownFrameworkAssemblies(string searchTypeName, bool ignoreCase)
        {
            return Type.GetType(
                searchTypeName,
                ResolveGenericArgumentAssembly,
                ResolveKnownFrameworkGenericTypePart,
                throwOnError: false,
                ignoreCase);
        }

        private static Type? ResolveKnownFrameworkGenericTypePart(Assembly? resolvedAssembly, string nestedTypeName, bool ignoreCase)
        {
            if (resolvedAssembly is not null)
            {
                return resolvedAssembly.GetType(nestedTypeName, throwOnError: false, ignoreCase: ignoreCase);
            }

            return IsKnownFrameworkTypeName(nestedTypeName)
                       ? typeof(object).Assembly.GetType(nestedTypeName, throwOnError: false, ignoreCase: ignoreCase)
                       : null;
        }

        private static Assembly? ResolveKnownFrameworkAssembly(AssemblyName assemblyName)
        {
            return IsKnownFrameworkAssemblyName(assemblyName.FullName) ? typeof(object).Assembly : null;
        }

        internal static Assembly? ResolveLoadedAssembly(AssemblyName assemblyName)
        {
            var requestedFullName = assemblyName.FullName;
            var assemblies = Volatile.Read(ref _getAssemblies)();

            for (var i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                if (string.Equals(assembly.FullName, requestedFullName, StringComparison.Ordinal))
                {
                    return assembly;
                }
            }

            var requestedName = assemblyName.Name;
            if (requestedName is null || AssemblyNameHasMetadata(requestedFullName))
            {
                return null;
            }

            for (var i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                if (AssemblySimpleNameEquals(assembly.FullName, requestedName))
                {
                    return assembly;
                }
            }

            return null;
        }

        internal static Assembly? ResolveGenericArgumentAssembly(AssemblyName assemblyName)
        {
            return ResolveKnownFrameworkAssembly(assemblyName) ?? ResolveLoadedAssembly(assemblyName);
        }

        private static bool AssemblyNameHasMetadata(string assemblyName)
        {
            return assemblyName.IndexOf(',') >= 0;
        }

        private static bool IsKnownFrameworkAssemblyName(string assemblyName)
        {
            var simpleNameLength = assemblyName.IndexOf(',');
            if (simpleNameLength < 0)
            {
                simpleNameLength = assemblyName.Length;
            }

            return AssemblyNameEquals(assemblyName, simpleNameLength, "mscorlib") ||
                   AssemblyNameEquals(assemblyName, simpleNameLength, "netstandard") ||
                   AssemblyNameEquals(assemblyName, simpleNameLength, "System") ||
                   AssemblyNameEquals(assemblyName, simpleNameLength, "System.Private.CoreLib") ||
                   AssemblyNameEquals(assemblyName, simpleNameLength, "System.Runtime");
        }

        private static bool IsKnownFrameworkTypeName(string typeName)
        {
            return typeName.StartsWith("System.", StringComparison.Ordinal);
        }

        private static bool AssemblyNameEquals(string assemblyName, int simpleNameLength, string expectedName)
        {
            return simpleNameLength == expectedName.Length &&
                   string.Compare(assemblyName, 0, expectedName, 0, expectedName.Length, StringComparison.Ordinal) == 0;
        }

        private static bool AssemblySimpleNameEquals(string? assemblyFullName, string expectedName)
        {
            if (assemblyFullName is null)
            {
                return false;
            }

            var simpleNameLength = assemblyFullName.IndexOf(',');
            if (simpleNameLength < 0)
            {
                simpleNameLength = assemblyFullName.Length;
            }

            return AssemblyNameEquals(assemblyFullName, simpleNameLength, expectedName);
        }
    }

    private sealed class ResolutionState
    {
        private readonly WeakReference<Type>? _type;

        internal ResolutionState(Type? type, bool isAmbiguous, int scannedGeneration, AssemblyIdentity[] scannedAssemblies)
        {
            _type = type is null ? null : new WeakReference<Type>(type);
            IsAmbiguous = isAmbiguous;
            ScannedGeneration = scannedGeneration;
            ScannedAssemblies = scannedAssemblies ?? EmptyAssemblyIdentities;
        }

        internal bool HasResolvedType => _type is not null;

        internal bool IsAmbiguous { get; }

        internal int ScannedGeneration { get; }

        internal AssemblyIdentity[] ScannedAssemblies { get; }

        internal Type? GetTypeOrDefault()
        {
            return TryGetType(out var type) ? type : null;
        }

        internal bool TryGetType([NotNullWhen(true)] out Type? type)
        {
            if (_type is null)
            {
                type = null;
                return false;
            }

            return _type.TryGetTarget(out type);
        }
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
