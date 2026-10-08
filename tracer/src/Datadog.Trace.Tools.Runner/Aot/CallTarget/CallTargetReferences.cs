// <copyright file="CallTargetReferences.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// The types and members of <c>Datadog.Trace</c> and the core library a registration uses, referenced from the
/// instrumented module. <c>Datadog.Trace</c> types go through the assembly reference the native rewriter already
/// added, and must exist in the <c>Datadog.Trace.dll</c> the application ships.
/// </summary>
internal sealed class CallTargetReferences
{
    internal const string CallTargetNamespace = "Datadog.Trace.ClrProfiler.CallTarget";
    internal const string HandlersNamespace = CallTargetNamespace + ".Handlers";
    internal const string ContinuationsNamespace = HandlersNamespace + ".Continuations";

    private readonly ModuleDef _module;
    private readonly ModuleDef _datadogTrace;
    private readonly IResolutionScope _datadogTraceScope;
    private readonly Importer _importer;
    private readonly Dictionary<string, TypeRef> _types = new(StringComparer.Ordinal);

    public CallTargetReferences(ModuleDef module, ModuleDef datadogTrace, IResolutionScope datadogTraceScope)
    {
        _module = module;
        _datadogTrace = datadogTrace;
        _datadogTraceScope = datadogTraceScope;
        _importer = new Importer(module, ImporterOptions.TryToUseDefs, default, new LocalTypeMapper(module));
        if (datadogTrace.Find(HandlersNamespace + ".CallTargetAot`2", isReflectionName: false) is null)
        {
            throw new InvalidOperationException($"{datadogTrace.Location} doesn't support NativeAOT CallTarget registrations: build the application with a Datadog.Trace version that has {HandlersNamespace}.CallTargetAot.");
        }

        CallTargetState = new ValueTypeSig(DatadogType(CallTargetNamespace + ".CallTargetState"));
        CallTargetReturn = new ValueTypeSig(DatadogType(CallTargetNamespace + ".CallTargetReturn"));
        Exception = new ClassSig(module.CorLibTypes.GetTypeRef("System", "Exception"));
        Type = new ClassSig(module.CorLibTypes.GetTypeRef("System", "Type"));
        GetTypeFromHandle = new MemberRefUser(
            module,
            "GetTypeFromHandle",
            MethodSig.CreateStatic(Type, new ValueTypeSig(module.CorLibTypes.GetTypeRef("System", "RuntimeTypeHandle"))),
            module.CorLibTypes.GetTypeRef("System", "Type"));
    }

    public ModuleDef Module => _module;

    public TypeSig CallTargetState { get; }

    public TypeSig CallTargetReturn { get; }

    public TypeSig Exception { get; }

    public TypeSig Type { get; }

    public IMethod GetTypeFromHandle { get; }

    public static ITypeDefOrRef ToTypeDefOrRef(TypeSig type) => type is TypeDefOrRefSig simple ? simple.TypeDefOrRef : new TypeSpecUser(type);

    public GenericInstSig CallTargetReturnOf(TypeSig returnType)
        => new(new ValueTypeSig(DatadogType(CallTargetNamespace + ".CallTargetReturn`1")), returnType);

    public GenericInstSig TaskOf(TypeSig result)
        => new(new ClassSig(_module.CorLibTypes.GetTypeRef("System.Threading.Tasks", "Task`1")), result);

    public GenericInstSig FuncOf(TypeSig result)
        => new(new ClassSig(_module.CorLibTypes.GetTypeRef("System", "Func`1")), result);

    /// <summary>
    /// A generic <c>Datadog.Trace</c> class instantiated with <paramref name="arguments"/>; nested types use <c>/</c>.
    /// </summary>
    public GenericInstSig DatadogGeneric(string fullName, params TypeSig[] arguments) => new(new ClassSig(DatadogType(fullName)), arguments);

    public TypeRef DatadogType(string fullName)
    {
        if (_types.TryGetValue(fullName, out var existing))
        {
            return existing;
        }

        if (_datadogTrace.Find(fullName, isReflectionName: false) is null)
        {
            throw new InvalidOperationException($"{_datadogTrace.Location} doesn't define {fullName}.");
        }

        TypeRef created;
        var nested = fullName.LastIndexOf('/');
        if (nested >= 0)
        {
            created = new TypeRefUser(_module, string.Empty, fullName.Substring(nested + 1), DatadogType(fullName.Substring(0, nested)));
        }
        else
        {
            var separator = fullName.LastIndexOf('.');
            created = new TypeRefUser(_module, fullName.Substring(0, separator), fullName.Substring(separator + 1), _datadogTraceScope);
        }

        _types[fullName] = created;
        return created;
    }

    public MemberRef DatadogStaticMethod(TypeSig declaringType, string name, MethodSig signature)
        => new MemberRefUser(_module, name, signature, ToTypeDefOrRef(declaringType));

    public MemberRef Constructor(TypeSig declaringType, params TypeSig[] parameters)
        => new MemberRefUser(_module, ".ctor", MethodSig.CreateInstance(_module.CorLibTypes.Void, parameters), ToTypeDefOrRef(declaringType));

    /// <summary>
    /// Brings a type of another module (an integration's) into the instrumented module; types of the instrumented
    /// module stay as they are.
    /// </summary>
    public TypeSig Import(TypeSig type) => IsLocal(type) ? type : _importer.Import(type);

    public IMethod Import(MethodDef method) => method.Module == _module ? method : _importer.Import(method);

    private bool IsLocal(TypeSig? type)
    {
        while (type is not null)
        {
            switch (type)
            {
                case CorLibTypeSig corLib:
                    return corLib.TypeDefOrRef.Module == _module;
                case TypeDefOrRefSig simple:
                    return IsLocal(simple.TypeDefOrRef);
                case GenericInstSig instance:
                    return IsLocal(instance.GenericType) && instance.GenericArguments.All(IsLocal);
                case GenericSig:
                    return true;
                default:
                    type = type.Next;
                    break;
            }
        }

        return true;
    }

    private bool IsLocal(ITypeDefOrRef type)
        => type switch
        {
            TypeDef definition => definition.Module == _module,
            TypeRef reference => reference.Module == _module,
            TypeSpec specification => specification.Module == _module && IsLocal(specification.TypeSig),
            _ => false,
        };

    /// <summary>
    /// Types of the instrumented assembly referenced from another module (the proxy constructors of the DuckType AOT
    /// registry take the target types): the importer would otherwise make them references to that module.
    /// </summary>
    private sealed class LocalTypeMapper : ImportMapper
    {
        private readonly ModuleDef _module;

        public LocalTypeMapper(ModuleDef module)
        {
            _module = module;
        }

        public override ITypeDefOrRef? Map(ITypeDefOrRef source)
        {
            if (source is TypeRef reference
             && reference.Module != _module
             && reference.DefinitionAssembly is { } assembly
             && _module.Assembly is { } local
             && string.Equals(assembly.Name, local.Name, StringComparison.Ordinal))
            {
                return _module.Find(reference.FullName, isReflectionName: false);
            }

            return null;
        }
    }
}
#endif
