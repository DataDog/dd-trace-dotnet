using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Datadog.Trace.Debugger.Helpers;

internal class TypeFinder
{
    private class TrieNode
    {
        internal Dictionary<string, TrieNode> Children { get; } = new Dictionary<string, TrieNode>(StringComparer.OrdinalIgnoreCase);

        internal List<Lazy<Type>> Types { get; } = new List<Lazy<Type>>();
    }

    private static readonly Lazy<TypeFinder> _instance = new Lazy<TypeFinder>(() => new TypeFinder(), LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly TrieNode _root = new TrieNode();
    private readonly HashSet<string> _processedAssemblies = new HashSet<string>();
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

    internal static TypeFinder Instance => _instance.Value;

    private TypeFinder()
    {
        Initialize();
    }

    private void Initialize()
    {
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        LoadTypesFromLoadedAssemblies();
    }

    internal static void EnsureInitialized()
    {
        // This will trigger lazy initialization if it hasn't happened yet
        _ = Instance;
    }

    internal IEnumerable<Type> FindTypes(string typeName)
    {
        _lock.EnterReadLock();
        try
        {
            string[] parts = typeName.Split('.');
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
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
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
                catch (ReflectionTypeLoadException ex)
                {
                    foreach (Type type in ex.Types)
                    {
                        if (type != null)
                        {
                            InsertType(type.GetTypeInfo());
                        }
                    }
                    // Log the exception details here
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
        string fullName = GetFullTypeName(typeInfo);
        string[] parts = fullName.Split('.');
        TrieNode current = _root;

        foreach (string part in parts)
        {
            if (!current.Children.TryGetValue(part, out TrieNode node))
            {
                node = new TrieNode();
                current.Children[part] = node;
            }
            current = node;
        }

        current.Types.Add(new Lazy<Type>(() => typeInfo.AsType(), LazyThreadSafetyMode.ExecutionAndPublication));
    }

    private string GetFullTypeName(TypeInfo typeInfo)
    {
        if (!typeInfo.IsGenericType)
            return typeInfo.FullName ?? typeInfo.Name;

        var genericArguments = typeInfo.GenericTypeParameters.Select(t => t.Name).ToArray();
        string genericTypeName = typeInfo.Name.Split('`')[0];
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
}
