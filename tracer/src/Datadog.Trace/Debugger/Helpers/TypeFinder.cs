// <copyright file="TypeFinder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Helpers;

internal class TypeFinder
{
    // ReSharper disable once InconsistentNaming
    private static readonly Lazy<TypeFinder> _instance = new Lazy<TypeFinder>(() => new TypeFinder(), LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TypeFinder));

    private readonly TrieNode _root = new TrieNode();
    private readonly HashSet<string> _processedAssemblies = new HashSet<string>();
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

    private TypeFinder()
    {
        Initialize();
    }

    internal static TypeFinder Instance => _instance.Value;

    private void Initialize()
    {
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        LoadTypesFromLoadedAssemblies();
    }

    internal IEnumerable<Type> FindTypes(string typeName)
    {
        _lock.EnterReadLock();
        try
        {
            var parts = typeName.Split('.');
            return FindTypesRecursive(_root, parts, 0);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
    {
        LoadTypesFromAssembly(args.LoadedAssembly);
    }

    private void LoadTypesFromLoadedAssemblies()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            LoadTypesFromAssembly(assembly);
        }
    }

    private void LoadTypesFromAssembly(Assembly assembly)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_processedAssemblies.Add(assembly.FullName))
            {
                try
                {
                    foreach (TypeInfo typeInfo in assembly.DefinedTypes)
                    {
                        InsertType(typeInfo);
                    }
                }
                catch (ReflectionTypeLoadException e)
                {
                    Log.Warning(e, "Fail when reading types from assembly {Assembly}", assembly.FullName);

                    if (e.Types is { } types)
                    {
                        foreach (var type in types)
                        {
                            InsertType(type?.GetTypeInfo());
                        }
                    }
                }
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void InsertType(TypeInfo typeInfo)
    {
        if (typeInfo == null)
        {
            return;
        }

        string fullName = null;
        try
        {
            fullName = GetFullTypeName(typeInfo);
            var parts = fullName.Split('.');
            TrieNode current = _root;

            foreach (var part in parts)
            {
                if (!current.Children.TryGetValue(part, out var node))
                {
                    node = new TrieNode();
                    current.Children[part] = node;
                }

                current = node;
            }

            current.Types.Add(new Lazy<Type>(typeInfo.AsType, LazyThreadSafetyMode.ExecutionAndPublication));
        }
        catch (Exception e)
        {
            Log.Warning(e, "Fail when insert type {Type} to trie node", fullName ?? typeInfo.FullName ?? typeInfo.Name);
        }
    }

    private string GetFullTypeName(TypeInfo typeInfo)
    {
        if (!typeInfo.IsGenericType)
        {
            return typeInfo.FullName ?? typeInfo.Name;
        }

        var genericArguments = typeInfo.GenericTypeParameters.Select(t => t.Name).ToArray();
        var genericTypeName = typeInfo.Name.Split('`')[0];
        return $"{typeInfo.Namespace}.{genericTypeName}<{string.Join(",", genericArguments)}>";
    }

    private IEnumerable<Type> FindTypesRecursive(TrieNode node, string[] parts, int index)
    {
        if (index == parts.Length)
        {
            return node.Types.Select(t => t.Value);
        }

        var results = new List<Type>();

        // Exact match
        if (node.Children.TryGetValue(parts[index], out var exactMatch))
        {
            results.AddRange(FindTypesRecursive(exactMatch, parts, index + 1));
        }

        // Partial match
        foreach (var child in node.Children.Values)
        {
            if (child.Children.Count > 0)
            {
                results.AddRange(FindTypesRecursive(child, parts, index));
            }
        }

        return results;
    }

    private class TrieNode
    {
        internal Dictionary<string, TrieNode> Children { get; } = new Dictionary<string, TrieNode>(StringComparer.OrdinalIgnoreCase);

        internal List<Lazy<Type>> Types { get; } = new List<Lazy<Type>>();
    }
}
