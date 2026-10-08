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
/// code they instantiate. The proxies of duck typing constraints on generic types can only be generated for those.
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
        var discovery = new GenericInstantiationDiscovery(modules, resolveType);
        foreach (var module in discovery._modules)
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
        var result = new Dictionary<MethodDef, List<Instantiation>>();
        foreach (var method in rewrittenMethods)
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

        var instantiation = new Instantiation(typeArguments, closedMethodArguments);
        if (!_methodInstantiations.TryGetValue(definition, out var known))
        {
            _methodInstantiations[definition] = known = new Dictionary<string, Instantiation>(StringComparer.Ordinal);
        }

        var key = Key(typeArguments) + "||" + Key(closedMethodArguments);
        if (!known.ContainsKey(key))
        {
            known[key] = instantiation;
            _pending.Enqueue((definition, instantiation));
        }
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
