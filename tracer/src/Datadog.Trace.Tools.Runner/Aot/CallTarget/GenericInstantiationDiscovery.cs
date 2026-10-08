// <copyright file="GenericInstantiationDiscovery.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// Finds the closed instantiations of the generic contexts of the rewritten methods (C3): the generic types created and
/// the generic methods called with closed type arguments in the instrumented assemblies, propagated through the generic
/// code they instantiate, and from generic virtual and interface methods to their implementations (the instrumented
/// method is the implementation the call reaches). The proxies of duck typing constraints on generic types can only be
/// generated for those.
/// </summary>
internal sealed class GenericInstantiationDiscovery
{
    private const int MaxContexts = 50_000;

    private readonly HashSet<ModuleDef> _modules;
    private readonly Func<IMethod, MethodDef?> _resolveMethod;
    private readonly Func<ITypeDefOrRef, TypeDef?> _resolveType;
    private readonly Dictionary<TypeDef, Dictionary<string, IList<TypeSig>>> _typeInstantiations = new();
    private readonly Dictionary<MethodDef, Dictionary<string, Instantiation>> _methodInstantiations = new();
    private readonly Queue<(MethodDef Method, Instantiation Context)> _pending = new();
    private Dictionary<string, List<MethodDef>>? _genericVirtualMethods;
    private int _contexts;

    private GenericInstantiationDiscovery(IEnumerable<ModuleDef> modules, Func<ITypeDefOrRef, TypeDef?> resolveType)
    {
        _modules = new HashSet<ModuleDef>(modules);
        _resolveType = resolveType;
        _resolveMethod = ResolveMethod;
    }

    /// <summary>
    /// Returns the closed instantiations (type arguments of the declaring type, then of the method) of every rewritten
    /// method with a generic context.
    /// </summary>
    public static Dictionary<MethodDef, List<Instantiation>> Discover(IEnumerable<ModuleDef> modules, IEnumerable<MethodDef> rewrittenMethods, Func<ITypeDefOrRef, TypeDef?> resolveType)
    {
        var rewritten = rewrittenMethods.ToList();
        var discovery = new GenericInstantiationDiscovery(modules, resolveType);

        // Only the code of the modules of the rewritten methods and of those that reference them can instantiate their generic
        // types and methods: not the framework, most of the inputs (whose methods would use up the contexts first).
        foreach (var module in discovery.ReferencingModules(rewritten.Select(m => m.Module)))
        {
            foreach (var type in module.GetTypes().Where(t => !t.HasGenericParameters))
            {
                foreach (var method in type.Methods.Where(m => !m.HasGenericParameters))
                {
                    discovery._pending.Enqueue((method, Instantiation.Empty));
                }
            }
        }

        discovery.Run();
        if (discovery._contexts >= MaxContexts)
        {
            Native.AotLog.Warn($"Generic instantiation discovery stopped after {MaxContexts} generic contexts: some instantiations of instrumented generic methods may be missing.");
        }

        var result = new Dictionary<MethodDef, List<Instantiation>>();
        foreach (var method in rewritten)
        {
            var typeParameters = method.DeclaringType.GenericParameters.Count;
            if (typeParameters == 0 && !method.HasGenericParameters)
            {
                continue;
            }

            var instantiations = new List<Instantiation>();
            if (method.HasGenericParameters)
            {
                if (discovery._methodInstantiations.TryGetValue(method, out var known))
                {
                    instantiations.AddRange(known.Values);
                }
            }
            else if (discovery._typeInstantiations.TryGetValue(method.DeclaringType, out var types))
            {
                instantiations.AddRange(types.Values.Select(t => new Instantiation(t, Array.Empty<TypeSig>())));
            }

            if (instantiations.Count > 0)
            {
                result[method] = instantiations;
            }
        }

        return result;
    }

    /// <summary>
    /// Replaces the generic parameters (<c>!n</c> by the type arguments, <c>!!n</c> by the method arguments).
    /// </summary>
    public static TypeSig Substitute(TypeSig type, Instantiation instantiation)
    {
        switch (type)
        {
            case GenericMVar methodVariable when methodVariable.Number < instantiation.MethodArguments.Count:
                return instantiation.MethodArguments[(int)methodVariable.Number];
            case GenericVar typeVariable when typeVariable.Number < instantiation.TypeArguments.Count:
                return instantiation.TypeArguments[(int)typeVariable.Number];
            case GenericSig or CorLibTypeSig or TypeDefOrRefSig:
                return type;
            case GenericInstSig instance:
                return new GenericInstSig(instance.GenericType, instance.GenericArguments.Select(a => Substitute(a, instantiation)).ToList());
            case SZArraySig array:
                return new SZArraySig(Substitute(array.Next, instantiation));
            case ArraySig array:
                return new ArraySig(Substitute(array.Next, instantiation), array.Rank, array.Sizes, array.LowerBounds);
            case ByRefSig byRef:
                return new ByRefSig(Substitute(byRef.Next, instantiation));
            case PtrSig pointer:
                return new PtrSig(Substitute(pointer.Next, instantiation));
            case PinnedSig pinned:
                return new PinnedSig(Substitute(pinned.Next, instantiation));
            case CModReqdSig required:
                return new CModReqdSig(required.Modifier, Substitute(required.Next, instantiation));
            case CModOptSig optional:
                return new CModOptSig(optional.Modifier, Substitute(optional.Next, instantiation));
            default:
                return type;
        }
    }

    private static string Key(IEnumerable<TypeSig> types) => string.Join("|", types.Select(t => t.FullName));

    private static IEnumerable<GenericInstSig> GenericInstances(TypeSig? type)
    {
        while (type is not null)
        {
            if (type is GenericInstSig instance)
            {
                yield return instance;
                foreach (var argument in instance.GenericArguments)
                {
                    foreach (var nested in GenericInstances(argument))
                    {
                        yield return nested;
                    }
                }

                yield break;
            }

            type = type.Next;
        }
    }

    /// <summary>
    /// The modules of the inputs that are, or reference (directly or not), one of <paramref name="targets"/>.
    /// </summary>
    private List<ModuleDef> ReferencingModules(IEnumerable<ModuleDef> targets)
    {
        var names = new HashSet<string>(targets.Select(m => m.Assembly?.Name.String ?? m.Name.String), StringComparer.OrdinalIgnoreCase);
        var referencing = new List<ModuleDef>();
        var remaining = _modules.ToList();
        bool added;
        do
        {
            added = false;
            foreach (var module in remaining.ToList())
            {
                if (names.Contains(module.Assembly?.Name.String ?? module.Name.String) || module.GetAssemblyRefs().Any(r => names.Contains(r.Name.String)))
                {
                    names.Add(module.Assembly?.Name.String ?? module.Name.String);
                    referencing.Add(module);
                    remaining.Remove(module);
                    added = true;
                }
            }
        }
        while (added);

        return referencing;
    }

    private MethodDef? ResolveMethod(IMethod method)
        => method switch
        {
            MethodDef definition => definition,
            MemberRef reference when reference.IsMethodRef => reference.ResolveMethod(),
            MethodSpec specification => ResolveMethod(specification.Method),
            _ => null,
        };

    private void Run()
    {
        while (_pending.Count > 0 && _contexts < MaxContexts)
        {
            var (method, context) = _pending.Dequeue();
            _contexts++;
            if (!method.HasBody)
            {
                continue;
            }

            foreach (var instruction in method.Body.Instructions)
            {
                switch (instruction.Operand)
                {
                    case MethodSpec specification:
                        VisitMethod(specification.Method, specification.GenericInstMethodSig.GenericArguments, context);
                        break;
                    case MethodDef or MemberRef { IsMethodRef: true }:
                        VisitMethod((IMethod)instruction.Operand, Array.Empty<TypeSig>(), context);
                        break;
                    case FieldDef or MemberRef { IsFieldRef: true }:
                        if (((IField)instruction.Operand).DeclaringType is { } fieldType)
                        {
                            VisitType(fieldType.ToTypeSig(), context);
                        }

                        break;
                    case ITypeDefOrRef type:
                        VisitType(type.ToTypeSig(), context);
                        break;
                }
            }
        }
    }

    private void VisitMethod(IMethod reference, IList<TypeSig> methodArguments, Instantiation context)
    {
        if (reference.DeclaringType is { } declaringType)
        {
            VisitType(declaringType.ToTypeSig(), context);
        }

        var definition = _resolveMethod(reference);
        if (definition is null || !definition.HasGenericParameters || !_modules.Contains(definition.Module) || methodArguments.Count != definition.GenericParameters.Count)
        {
            return;
        }

        var closedMethodArguments = methodArguments.Select(a => Substitute(a, context)).ToList();
        var typeArguments = reference.DeclaringType?.ToTypeSig() is GenericInstSig instance
                                ? instance.GenericArguments.Select(a => Substitute(a, context)).ToList()
                                : new List<TypeSig>();
        if (closedMethodArguments.Concat(typeArguments).Any(a => a.ContainsGenericParameter))
        {
            return;
        }

        AddMethodInstantiation(definition, new Instantiation(typeArguments, closedMethodArguments));

        // A virtual or interface method runs one of its implementations (non-generic types: their type arguments are known).
        foreach (var implementation in Implementations(definition))
        {
            AddMethodInstantiation(implementation, new Instantiation(Array.Empty<TypeSig>(), closedMethodArguments));
        }
    }

    private void AddMethodInstantiation(MethodDef definition, Instantiation instantiation)
    {
        if (!_methodInstantiations.TryGetValue(definition, out var known))
        {
            _methodInstantiations[definition] = known = new Dictionary<string, Instantiation>(StringComparer.Ordinal);
        }

        var key = Key(instantiation.TypeArguments) + "||" + Key(instantiation.MethodArguments);
        if (!known.ContainsKey(key))
        {
            known[key] = instantiation;
            _pending.Enqueue((definition, instantiation));
        }
    }

    /// <summary>
    /// The generic methods of non-generic types of the modules that implement or override a generic virtual method: same
    /// name (explicit implementations end with it), generic arity and parameter count, in a type that derives from or
    /// implements its declaring type.
    /// </summary>
    private IEnumerable<MethodDef> Implementations(MethodDef method)
    {
        if (!method.IsVirtual)
        {
            yield break;
        }

        _genericVirtualMethods ??= _modules.SelectMany(m => m.GetTypes())
                                           .Where(t => !t.HasGenericParameters)
                                           .SelectMany(t => t.Methods)
                                           .Where(m => m.IsVirtual && m.HasGenericParameters && m.HasBody)
                                           .GroupBy(m => ShortName(m.Name))
                                           .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        if (!_genericVirtualMethods.TryGetValue(ShortName(method.Name), out var candidates))
        {
            yield break;
        }

        foreach (var candidate in candidates)
        {
            if (candidate != method
             && candidate.GenericParameters.Count == method.GenericParameters.Count
             && candidate.MethodSig.Params.Count == method.MethodSig.Params.Count
             && DerivesFrom(candidate.DeclaringType, method.DeclaringType))
            {
                yield return candidate;
            }
        }

        static string ShortName(string name) => name.Substring(name.LastIndexOf('.') + 1);
    }

    private bool DerivesFrom(TypeDef type, TypeDef ancestor)
    {
        var visited = new HashSet<TypeDef>();
        var pending = new Stack<TypeDef>();
        pending.Push(type);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (current != type && current == ancestor)
            {
                return true;
            }

            foreach (var parent in current.Interfaces.Select(i => i.Interface).Append(current.BaseType))
            {
                if (parent is not null && _resolveType(parent) is { } resolved)
                {
                    pending.Push(resolved);
                }
            }
        }

        return false;
    }

    private void VisitType(TypeSig type, Instantiation context)
    {
        var closed = Substitute(type, context);
        foreach (var instance in GenericInstances(closed))
        {
            if (instance.ContainsGenericParameter || _resolveType(instance.GenericType.TypeDefOrRef) is not { } definition || !_modules.Contains(definition.Module))
            {
                continue;
            }

            if (!_typeInstantiations.TryGetValue(definition, out var known))
            {
                _typeInstantiations[definition] = known = new Dictionary<string, IList<TypeSig>>(StringComparer.Ordinal);
            }

            var key = Key(instance.GenericArguments);
            if (known.ContainsKey(key))
            {
                continue;
            }

            known[key] = instance.GenericArguments;
            var typeContext = new Instantiation(instance.GenericArguments, Array.Empty<TypeSig>());
            foreach (var method in definition.Methods.Where(m => !m.HasGenericParameters))
            {
                _pending.Enqueue((method, typeContext));
            }
        }
    }

    /// <summary>
    /// The type arguments of the declaring type and of the method of one closed instantiation.
    /// </summary>
    internal sealed class Instantiation
    {
        public static readonly Instantiation Empty = new(Array.Empty<TypeSig>(), Array.Empty<TypeSig>());

        public Instantiation(IList<TypeSig> typeArguments, IList<TypeSig> methodArguments)
        {
            TypeArguments = typeArguments;
            MethodArguments = methodArguments;
        }

        public IList<TypeSig> TypeArguments { get; }

        public IList<TypeSig> MethodArguments { get; }

        public override string ToString() => $"<{string.Join(", ", TypeArguments.Concat(MethodArguments).Select(t => t.FullName))}>";
    }
}
#endif
