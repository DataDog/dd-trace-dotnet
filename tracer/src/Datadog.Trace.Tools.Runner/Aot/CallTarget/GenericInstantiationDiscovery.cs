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
/// The search only follows the generic code that leads to a rewritten method (H-76): a first pass over the method bodies
/// finds the generic types and methods whose instantiation can reach one (they reference one with their own generic
/// parameters, directly or not); the closed references to them, in any method, start the search, which only propagates
/// type arguments through them. Spending a global budget on the whole application instead (the framework first) left the
/// instantiations of the packages out in large applications.
/// </summary>
internal sealed class GenericInstantiationDiscovery
{
    /// <summary>
    /// The nesting depth of type arguments beyond which an instantiation isn't followed: generic code that instantiates itself
    /// with a bigger argument (<c>M&lt;T&gt;</c> calling <c>M&lt;List&lt;T&gt;&gt;</c>) would expand forever, like the generic
    /// cycles ILC cuts.
    /// </summary>
    internal const int MaxGenericDepth = 8;

    /// <summary>
    /// A safety net: the search only follows the code that leads to rewritten methods.
    /// </summary>
    internal const int DefaultMaxContexts = 1_000_000;

    private readonly HashSet<ModuleDef> _modules;
    private readonly Func<IMethod, MethodDef?> _resolveMethod;
    private readonly Func<ITypeDefOrRef, TypeDef?> _resolveType;
    private readonly int _maxContexts;
    private readonly HashSet<IMemberDef> _relevant = new();
    private readonly HashSet<object> _closedReferences = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<TypeDef, Dictionary<string, IList<TypeSig>>> _typeInstantiations = new();
    private readonly Dictionary<MethodDef, Dictionary<string, Instantiation>> _methodInstantiations = new();
    private readonly Queue<(MethodDef Method, Instantiation Context)> _pending = new();
    private Dictionary<string, List<MethodDef>>? _genericVirtualMethods;
    private Dictionary<string, List<MethodDef>>? _genericVirtualDeclarations;
    private int _contexts;
    private int _depthCuts;

    private GenericInstantiationDiscovery(IEnumerable<ModuleDef> modules, Func<ITypeDefOrRef, TypeDef?> resolveType, int maxContexts)
    {
        _modules = new HashSet<ModuleDef>(modules);
        _resolveType = resolveType;
        _resolveMethod = ResolveMethod;
        _maxContexts = maxContexts;
    }

    /// <summary>
    /// Returns the closed instantiations (type arguments of the declaring type, then of the method) of every rewritten
    /// method with a generic context.
    /// </summary>
    public static Dictionary<MethodDef, List<Instantiation>> Discover(IEnumerable<ModuleDef> modules, IEnumerable<MethodDef> rewrittenMethods, Func<ITypeDefOrRef, TypeDef?> resolveType)
        => Discover(modules, rewrittenMethods, resolveType, out _);

    /// <summary>
    /// Returns the closed instantiations (type arguments of the declaring type, then of the method) of every rewritten
    /// method with a generic context, and how the search went (whether it was cut).
    /// </summary>
    public static Dictionary<MethodDef, List<Instantiation>> Discover(IEnumerable<ModuleDef> modules, IEnumerable<MethodDef> rewrittenMethods, Func<ITypeDefOrRef, TypeDef?> resolveType, out Statistics statistics, int maxContexts = DefaultMaxContexts)
    {
        var rewritten = rewrittenMethods.ToList();
        var discovery = new GenericInstantiationDiscovery(modules, resolveType, maxContexts);

        // Only the code of the modules of the rewritten methods and of those that reference them can instantiate their generic
        // types and methods.
        discovery.FindRelevantMembers(discovery.ReferencingModules(rewritten.Select(m => m.Module)), rewritten);
        foreach (var operand in discovery._closedReferences)
        {
            discovery.Visit(operand, Instantiation.Empty);
        }

        discovery.Run();
        statistics = new Statistics(discovery._relevant.Count, discovery._closedReferences.Count, discovery._contexts, discovery._depthCuts, limitReached: discovery._pending.Count > 0);

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

    private static int Depth(TypeSig? type)
        => type switch
        {
            GenericInstSig instance => 1 + instance.GenericArguments.Select(Depth).DefaultIfEmpty(0).Max(),
            NonLeafSig nonLeaf => Depth(nonLeaf.Next),
            _ => 0,
        };

    private static int Depth(Instantiation instantiation)
        => instantiation.TypeArguments.Concat(instantiation.MethodArguments).Select(Depth).DefaultIfEmpty(0).Max();

    private static string ShortName(string name) => name.Substring(name.LastIndexOf('.') + 1);

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

    /// <summary>
    /// Finds the generic types and methods whose instantiation can reach a rewritten method (they reference one with their own
    /// generic parameters, directly or not, or override a generic virtual method that is one), and the references with closed
    /// type arguments of every method, which start the search.
    /// </summary>
    private void FindRelevantMembers(List<ModuleDef> scanned, List<MethodDef> rewritten)
    {
        // The methods that reference a generic type or method with their own generic parameters: they instantiate it with
        // the type arguments of their own instantiations.
        var referencers = new Dictionary<IMemberDef, List<MethodDef>>();
        foreach (var module in scanned)
        {
            foreach (var type in module.GetTypes())
            {
                foreach (var method in type.Methods.Where(m => m.HasBody))
                {
                    foreach (var instruction in method.Body.Instructions)
                    {
                        var operand = instruction.Operand;
                        var closed = false;
                        foreach (var (member, open) in GenericMembers(operand))
                        {
                            if (!open)
                            {
                                closed = true;
                            }
                            else if (referencers.TryGetValue(member, out var methods))
                            {
                                if (methods[methods.Count - 1] != method)
                                {
                                    methods.Add(method);
                                }
                            }
                            else
                            {
                                referencers[member] = new List<MethodDef> { method };
                            }
                        }

                        if (closed)
                        {
                            _closedReferences.Add(operand);
                        }
                    }
                }
            }
        }

        var pending = new Queue<IMemberDef>();
        foreach (var method in rewritten)
        {
            if (method.HasGenericParameters)
            {
                Mark(method);
            }

            if (method.DeclaringType.HasGenericParameters)
            {
                Mark(method.DeclaringType);
            }
        }

        while (pending.Count > 0)
        {
            var member = pending.Dequeue();
            if (member is MethodDef method)
            {
                // A method of a generic type runs with the instantiations of its type.
                if (method.DeclaringType.HasGenericParameters)
                {
                    Mark(method.DeclaringType);
                }

                // A call to a generic virtual method runs one of its implementations.
                foreach (var overridden in Overridden(method))
                {
                    Mark(overridden);
                }
            }

            if (referencers.TryGetValue(member, out var methods))
            {
                foreach (var referencer in methods)
                {
                    Mark(referencer);
                }
            }
        }

        void Mark(IMemberDef member)
        {
            if (_relevant.Add(member))
            {
                pending.Enqueue(member);
            }
        }
    }

    /// <summary>
    /// The generic types and methods of the scanned modules an operand references, and whether their type arguments are
    /// open (generic parameters of the referencing method or type).
    /// </summary>
    private IEnumerable<(IMemberDef Member, bool Open)> GenericMembers(object? operand)
    {
        TypeSig? declaringType = null;
        switch (operand)
        {
            case MethodSpec specification:
                declaringType = specification.Method.DeclaringType?.ToTypeSig();
                if (_resolveMethod(specification.Method) is { HasGenericParameters: true } method && _modules.Contains(method.Module))
                {
                    var open = specification.GenericInstMethodSig.GenericArguments.Any(a => a.ContainsGenericParameter) || declaringType?.ContainsGenericParameter == true;
                    yield return (method, open);
                }

                break;
            case MethodDef or MemberRef { IsMethodRef: true }:
                declaringType = ((IMethod)operand).DeclaringType?.ToTypeSig();
                break;
            case FieldDef or MemberRef { IsFieldRef: true }:
                declaringType = ((IField)operand).DeclaringType?.ToTypeSig();
                break;
            case ITypeDefOrRef type:
                declaringType = type.ToTypeSig();
                break;
        }

        foreach (var instance in GenericInstances(declaringType))
        {
            if (_resolveType(instance.GenericType.TypeDefOrRef) is { } definition && _modules.Contains(definition.Module))
            {
                yield return (definition, instance.ContainsGenericParameter);
            }
        }
    }

    /// <summary>
    /// The generic virtual methods a generic method of a non-generic type implements or overrides (the reverse of
    /// <see cref="Implementations"/>).
    /// </summary>
    private IEnumerable<MethodDef> Overridden(MethodDef method)
    {
        if (!method.IsVirtual || !method.HasGenericParameters || method.DeclaringType.HasGenericParameters)
        {
            yield break;
        }

        _genericVirtualDeclarations ??= _modules.SelectMany(m => m.GetTypes())
                                                .SelectMany(t => t.Methods)
                                                .Where(m => m.IsVirtual && m.HasGenericParameters)
                                                .GroupBy(m => ShortName(m.Name))
                                                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        if (!_genericVirtualDeclarations.TryGetValue(ShortName(method.Name), out var candidates))
        {
            yield break;
        }

        foreach (var candidate in candidates)
        {
            if (candidate != method
             && candidate.GenericParameters.Count == method.GenericParameters.Count
             && candidate.MethodSig.Params.Count == method.MethodSig.Params.Count
             && DerivesFrom(method.DeclaringType, candidate.DeclaringType))
            {
                yield return candidate;
            }
        }
    }

    private void Run()
    {
        while (_pending.Count > 0 && _contexts < _maxContexts)
        {
            var (method, context) = _pending.Dequeue();
            _contexts++;
            if (!method.HasBody)
            {
                continue;
            }

            foreach (var instruction in method.Body.Instructions)
            {
                Visit(instruction.Operand, context);
            }
        }
    }

    private void Visit(object? operand, Instantiation context)
    {
        switch (operand)
        {
            case MethodSpec specification:
                VisitMethod(specification.Method, specification.GenericInstMethodSig.GenericArguments, context);
                break;
            case MethodDef or MemberRef { IsMethodRef: true }:
                VisitMethod((IMethod)operand, Array.Empty<TypeSig>(), context);
                break;
            case FieldDef or MemberRef { IsFieldRef: true }:
                if (((IField)operand).DeclaringType is { } fieldType)
                {
                    VisitType(fieldType.ToTypeSig(), context);
                }

                break;
            case ITypeDefOrRef type:
                VisitType(type.ToTypeSig(), context);
                break;
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
        if (!_relevant.Contains(definition))
        {
            return;
        }

        if (Depth(instantiation) > MaxGenericDepth)
        {
            _depthCuts++;
            return;
        }

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
            if (instance.ContainsGenericParameter || _resolveType(instance.GenericType.TypeDefOrRef) is not { } definition || !_relevant.Contains(definition))
            {
                continue;
            }

            if (Depth(instance) > MaxGenericDepth)
            {
                _depthCuts++;
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
            foreach (var method in definition.Methods.Where(m => !m.HasGenericParameters && _relevant.Contains(m)))
            {
                _pending.Enqueue((method, typeContext));
            }
        }
    }

    /// <summary>
    /// How the search went.
    /// </summary>
    internal sealed class Statistics
    {
        public Statistics(int relevantMembers, int closedReferences, int contexts, int depthCuts, bool limitReached)
        {
            RelevantMembers = relevantMembers;
            ClosedReferences = closedReferences;
            Contexts = contexts;
            DepthCuts = depthCuts;
            LimitReached = limitReached;
        }

        /// <summary>Gets the generic types and methods whose instantiation can reach a rewritten method.</summary>
        public int RelevantMembers { get; }

        /// <summary>Gets the references with closed type arguments the search started from.</summary>
        public int ClosedReferences { get; }

        /// <summary>Gets the method bodies followed with a closed generic context.</summary>
        public int Contexts { get; }

        /// <summary>Gets the instantiations left out for their nesting depth (<see cref="MaxGenericDepth"/>).</summary>
        public int DepthCuts { get; }

        /// <summary>Gets a value indicating whether the search stopped at its limit of contexts.</summary>
        public bool LimitReached { get; }

        /// <summary>Gets a value indicating whether some instantiations may be missing.</summary>
        public bool Truncated => DepthCuts > 0 || LimitReached;
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
