// <copyright file="CallTargetRegistryGenerator.cs" company="Datadog">
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
using dnlib.DotNet.Writer;

namespace Datadog.Trace.Tools.Runner.Aot.CallTarget;

/// <summary>
/// Generates the CallTarget registrations of an instrumented module (D-12, B1-B5). Every rewritten method gets a
/// registration type, generic over the type and method generic parameters of the method, whose static constructor
/// fills the handler holders (<c>CallTargetAot</c>) with adapters that replicate <c>IntegrationMapper</c>; a call to
/// its <c>Ensure</c> method at the entry of the rewritten method runs it before the first handler is used.
/// </summary>
internal sealed class CallTargetRegistryGenerator
{
    internal const string RegistrationTypePrefix = "<DatadogCallTarget>Registration";

    private const string HandlersNamespace = CallTargetReferences.HandlersNamespace;
    private const string ContinuationsNamespace = CallTargetReferences.ContinuationsNamespace;

    private readonly ModuleDef _module;
    private readonly CallTargetReferences _references;
    private readonly Func<ITypeDefOrRef, TypeDef?> _resolveType;
    private readonly Func<TypeSig, bool> _derivesFromTask;
    private readonly IDuckProxyProvider? _proxies;
    private readonly IReadOnlyDictionary<MethodDef, List<GenericInstantiationDiscovery.Instantiation>>? _instantiations;
    private readonly CallTargetRegistryResult _result = new();
    private readonly SortedSet<string> _accessedAssemblies = new(StringComparer.Ordinal);
    private bool _usesProxies;
    private int _registrationTypes;

    private CallTargetRegistryGenerator(ModuleDef module, CallTargetReferences references, Func<ITypeDefOrRef, TypeDef?> resolveType, Func<TypeSig, bool> derivesFromTask, IDuckProxyProvider? proxies, IReadOnlyDictionary<MethodDef, List<GenericInstantiationDiscovery.Instantiation>>? instantiations)
    {
        _instantiations = instantiations;
        _module = module;
        _references = references;
        _resolveType = resolveType;
        _derivesFromTask = derivesFromTask;
        _proxies = proxies;
    }

    /// <param name="module">The instrumented module, with the rewritten bodies already set.</param>
    /// <param name="rewrittenMethods">The methods the native rewriter instrumented.</param>
    /// <param name="datadogTrace">The <c>Datadog.Trace</c> module the application ships.</param>
    /// <param name="resolveType">Resolves a type reference across the loaded modules.</param>
    /// <param name="duckTypeRegistry">The DuckType AOT registry with the proxies of the duck typing constraints, if any.</param>
    /// <param name="instantiations">The closed instantiations of the rewritten methods with a generic context (C3).</param>
    /// <param name="isApplication">Whether the module is the application's, whose module initializer initializes the instrumentation.</param>
    /// <param name="userStrings">For the application: the string literals of the instrumented assemblies (location, value) for IAST's hardcoded secrets analysis.</param>
    /// <param name="sourceLink">For the application: the SourceLink document of its PDB (git metadata).</param>
    public static CallTargetRegistryResult Generate(
        ModuleDef module,
        IEnumerable<MethodDef> rewrittenMethods,
        ModuleDef datadogTrace,
        Func<ITypeDefOrRef, TypeDef?> resolveType,
        CallTargetDuckTypeRegistry? duckTypeRegistry = null,
        IReadOnlyDictionary<MethodDef, List<GenericInstantiationDiscovery.Instantiation>>? instantiations = null,
        bool isApplication = false,
        IReadOnlyList<(string Location, string Value)>? userStrings = null,
        string? sourceLink = null)
    {
        var scanned = rewrittenMethods.Where(m => m.Body is not null)
                                      .Select(m => (Method: m, Invocations: CallTargetInvocationScanner.Scan(m.Body)))
                                      .Where(s => s.Invocations.Count > 0)
                                      .ToList();
        var scope = scanned.Count > 0 ? FindDatadogTraceScope(scanned[0].Method.Body) : null;
        if (scope is null)
        {
            if (!isApplication)
            {
                return new CallTargetRegistryResult();
            }

            // Nothing of the application itself is instrumented: it still initializes the instrumentation.
            scope = module.UpdateRowId(new AssemblyRefUser(datadogTrace.Assembly));
        }

        var references = new CallTargetReferences(module, datadogTrace, scope);
        var generator = new CallTargetRegistryGenerator(module, references, resolveType, type => DerivesFromTask(type, resolveType), duckTypeRegistry, instantiations);
        foreach (var (method, invocations) in scanned)
        {
            generator.AddRegistration(method, invocations);
        }

        var usesRegistry = generator._usesProxies && duckTypeRegistry is not null;
        if (usesRegistry)
        {
            generator._accessedAssemblies.Add(duckTypeRegistry!.AssemblyName);
        }

        if (usesRegistry || isApplication)
        {
            generator.AddModuleInitializer(duckTypeRegistry, initializeInstrumentation: isApplication, isApplication ? userStrings : null, isApplication ? sourceLink : null);
        }

        generator.AddIgnoresAccessChecks(force: isApplication);
        return generator._result;
    }

    /// <summary>
    /// First pass: binds every instantiation of the rewritten methods (nothing is emitted) to find the proxies the
    /// adapters need. Only closed types can be named, so the instantiations are used as they are.
    /// </summary>
    public static void CollectProxyRequests(
        IEnumerable<MethodDef> rewrittenMethods,
        Func<ITypeDefOrRef, TypeDef?> resolveType,
        DuckProxyRequestCollector collector,
        IReadOnlyDictionary<MethodDef, List<GenericInstantiationDiscovery.Instantiation>>? instantiations = null)
    {
        foreach (var method in rewrittenMethods.Where(m => m.Body is not null))
        {
            var invocations = CallTargetInvocationScanner.Scan(method.Body);
            if (instantiations is not null && instantiations.TryGetValue(method, out var closedInstantiations))
            {
                invocations.AddRange(closedInstantiations.SelectMany(i => invocations.Select(invocation => invocation.Remap(t => GenericInstantiationDiscovery.Substitute(t, i)))).ToList());
            }

            foreach (var invocation in invocations)
            {
                if (invocation.Integration is not TypeDefOrRefSig { TypeDefOrRef: { } reference } || resolveType(reference) is not { } integration)
                {
                    continue;
                }

                var target = invocation.Target;
                var objectType = method.Module.CorLibTypes.Object;
                switch (invocation.Kind)
                {
                    case CallTargetInvocationKind.Begin:
                        IntegrationBinder.BindBegin(integration, target, invocation.Arguments, collector);
                        break;
                    case CallTargetInvocationKind.BeginSlow:
                        IntegrationBinder.BindSlowBegin(integration, target, collector, method.MethodSig.Params.Select(p => p is ByRefSig byRef ? byRef.Next : p).ToList());
                        break;
                    case CallTargetInvocationKind.EndVoid:
                        IntegrationBinder.BindEndVoid(integration, target, collector);
                        break;
                    case CallTargetInvocationKind.EndReturn:
                        IntegrationBinder.BindEndReturn(integration, target, invocation.ReturnType!, collector);
                        if (ContinuationResultType(invocation.ReturnType!, objectType) is { } result)
                        {
                            IntegrationBinder.BindAsyncEnd(integration, target, result, collector);
                        }

                        break;
                    default:
                        IntegrationBinder.BindEndReturn(integration, target, invocation.DeclaredReturnType!, collector);
                        IntegrationBinder.BindAsyncEnd(integration, target, invocation.Kind == CallTargetInvocationKind.EndRuntimeAsyncVoid ? objectType : invocation.ReturnType!, collector);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// The value <c>OnAsyncMethodEnd</c> receives for a Task/ValueTask return type (object without one), or null.
    /// </summary>
    private static TypeSig? ContinuationResultType(TypeSig returnType, TypeSig objectType)
        => returnType.FullName is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask"
               ? objectType
               : returnType is GenericInstSig { GenericArguments.Count: 1 } instance ? instance.GenericArguments[0] : null;

    private static IResolutionScope? FindDatadogTraceScope(CilBody body)
        => body.Instructions.Select(i => i.Operand)
               .OfType<MethodSpec>()
               .Select(s => s.Method.DeclaringType)
               .OfType<TypeRef>()
               .FirstOrDefault(t => t.FullName == CallTargetInvocationScanner.CallTargetInvokerTypeName)
              ?.ResolutionScope;

    /// <summary>
    /// <c>typeof(Task).IsAssignableFrom(returnType)</c> for a generic type that isn't <c>Task&lt;T&gt;</c>.
    /// </summary>
    private static bool DerivesFromTask(TypeSig type, Func<ITypeDefOrRef, TypeDef?> resolveType)
    {
        var current = type is GenericInstSig instance ? resolveType(instance.GenericType.TypeDefOrRef) : null;
        for (var depth = 0; current is not null && depth < 32; depth++)
        {
            if (current.FullName == "System.Threading.Tasks.Task")
            {
                return true;
            }

            current = current.BaseType is { } baseType ? resolveType(baseType is TypeSpec { TypeSig: GenericInstSig generic } ? generic.GenericType.TypeDefOrRef : baseType) : null;
        }

        return false;
    }

    private static void Finish(CilBody body)
    {
        body.OptimizeMacros();
        MaxStackCalculator.GetMaxStack(body.Instructions, body.ExceptionHandlers, out var maxStack);
        body.MaxStack = (ushort)maxStack;
    }

    private void AddRegistration(MethodDef method, List<CallTargetInvocation> invocations)
    {
        var typeParameters = method.DeclaringType.GenericParameters;
        var methodParameters = method.GenericParameters;
        var arity = typeParameters.Count + methodParameters.Count;
        var index = _registrationTypes++;
        var name = $"{RegistrationTypePrefix}{index}" + (arity > 0 ? $"`{arity}" : string.Empty);

        // Not beforefieldinit: calling Ensure must run the static constructor.
        var registration = new TypeDefUser(string.Empty, name, _module.CorLibTypes.Object.TypeDefOrRef)
        {
            Attributes = TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class,
        };
        var remapper = new GenericContextRemapper(typeParameters.Count, registration);
        var sources = typeParameters.Concat(methodParameters).ToList();
        for (var i = 0; i < sources.Count; i++)
        {
            registration.GenericParameters.Add(new GenericParamUser((ushort)i, sources[i].Flags & ~GenericParamAttributes.VarianceMask, sources[i].Name));
        }

        // The constraints of the target's generic parameters keep the instantiations in the registration valid.
        for (var i = 0; i < sources.Count; i++)
        {
            foreach (var constraint in sources[i].GenericParamConstraints)
            {
                var remapped = remapper.Remap(constraint.Constraint.ToTypeSig());
                registration.GenericParameters[i].GenericParamConstraints.Add(new GenericParamConstraintUser(CallTargetReferences.ToTypeDefOrRef(remapped)));
            }
        }

        var builder = new RegistrationBuilder(registration, method.FullName);
        foreach (var invocation in invocations)
        {
            CallTargetInvocation remapped;
            try
            {
                remapped = invocation.Remap(remapper.Remap);
            }
            catch (NotSupportedException ex)
            {
                _result.Record(AdapterBindingStatus.Deferred, method.FullName, invocation, ex.Message, instantiation: false);
                continue;
            }

            AddInvocation(builder, remapped);
        }

        var ensureCalls = new List<IMethod>();
        if (builder.Items.Count > 0 || builder.Factories.Count > 0 || builder.Integrations.Count > 0)
        {
            var ensure = CompleteRegistration(builder);
            IMethod ensureReference = ensure;
            if (arity > 0)
            {
                var context = new List<TypeSig>();
                context.AddRange(typeParameters.Select(p => (TypeSig)new GenericVar(p.Number, method.DeclaringType)));
                context.AddRange(methodParameters.Select(p => (TypeSig)new GenericMVar(p.Number, method)));
                ensureReference = new MemberRefUser(_module, ensure.Name, ensure.MethodSig, new TypeSpecUser(new GenericInstSig(new ClassSig(registration), context)));
            }

            ensureCalls.Add(ensureReference);
        }

        // C3: the shapes the generic context leaves deferred (duck typing proxies of open types) are registered for the
        // closed instantiations found in the instrumented assemblies, by a registration of their own.
        if (builder.HasDeferred && _instantiations is not null && _instantiations.TryGetValue(method, out var instantiations))
        {
            var instances = new TypeDefUser(string.Empty, $"{RegistrationTypePrefix}{index}_Instantiations", _module.CorLibTypes.Object.TypeDefOrRef)
            {
                Attributes = TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class,
            };
            var instancesBuilder = new RegistrationBuilder(instances, method.FullName) { IsInstantiation = true };
            foreach (var found in instantiations)
            {
                // The type arguments can be types of another assembly (the application's): references of this module.
                var instantiation = new GenericInstantiationDiscovery.Instantiation(
                    found.TypeArguments.Select(_references.Import).ToList(),
                    found.MethodArguments.Select(_references.Import).ToList());
                foreach (var invocation in invocations)
                {
                    var closed = invocation.Remap(t => GenericInstantiationDiscovery.Substitute(t, instantiation));
                    if (!closed.ContainsGenericParameter)
                    {
                        AddInvocation(instancesBuilder, closed);
                    }
                }
            }

            if (instancesBuilder.Items.Count > 0 || instancesBuilder.Factories.Count > 0)
            {
                _result.Instantiations += instantiations.Count;
                ensureCalls.Add(CompleteRegistration(instancesBuilder));
            }
        }

        // First instructions: branches and protected regions keep pointing at the original first instruction.
        for (var i = ensureCalls.Count - 1; i >= 0; i--)
        {
            method.Body.Instructions.Insert(0, OpCodes.Call.ToInstruction(ensureCalls[i]));
        }
    }

    /// <summary>
    /// Adds a registration type with its static constructor and its <c>Ensure</c> method, which it returns.
    /// </summary>
    private MethodDef CompleteRegistration(RegistrationBuilder builder)
    {
        var registration = builder.Registration;
        _module.Types.Add(registration);
        _result.Registrations++;
        HideFromStackTraces(registration);
        EmitStaticConstructor(builder);
        var ensure = new MethodDefUser("Ensure", MethodSig.CreateStatic(_module.CorLibTypes.Void), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig);
        ensure.Body = new CilBody();
        ensure.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        registration.Methods.Add(ensure);
        return ensure;
    }

    private void AddInvocation(RegistrationBuilder builder, CallTargetInvocation invocation)
    {
        var integrationType = invocation.Integration;
        var integration = integrationType is TypeDefOrRefSig { TypeDefOrRef: { } reference } ? _resolveType(reference) : null;
        if (integration is null)
        {
            Defer(builder, invocation, integrationType is GenericInstSig ? "generic integration type" : "the integration type can't be resolved");
            return;
        }

        if (integration.Module != _module && integration.Module?.Assembly?.Name is { } assemblyName)
        {
            _accessedAssemblies.Add(assemblyName);
        }

        var target = invocation.Target;

        // The integration is enabled while one of its instrumentation categories is (CallTargetAotCategories), like the
        // native tracer enables its definitions. The open registration runs for every instantiation that is called.
        var key = integrationType.FullName + "|" + target.FullName;
        if (!builder.IsInstantiation && !builder.Integrations.ContainsKey(key))
        {
            builder.Integrations[key] = new IntegrationItem(integrationType, target, IntegrationCategories.Get(_references.DatadogTrace, integration));
        }

        var state = new ByRefSig(_references.CallTargetState);
        switch (invocation.Kind)
        {
            case CallTargetInvocationKind.Begin:
            {
                var arguments = invocation.Arguments;
                var handler = $"{HandlersNamespace}.BeginMethodHandler`{arguments.Count + 2}/InvokeDelegate";
                var delegateType = _references.DatadogGeneric(handler, new[] { integrationType, target }.Concat(arguments).ToArray());
                var binding = IntegrationBinder.BindBegin(integration, target, arguments, _proxies);
                var parameters = new[] { target }.Concat(arguments.Select(a => (TypeSig)new ByRefSig(a))).ToArray();
                AddItem(builder, invocation, integrationType, delegateType, binding, "Begin", _references.CallTargetState, parameters);
                break;
            }

            case CallTargetInvocationKind.BeginSlow:
            {
                var delegateType = _references.DatadogGeneric($"{HandlersNamespace}.BeginMethodSlowHandler`2/InvokeDelegate", integrationType, target);
                var binding = IntegrationBinder.BindSlowBegin(integration, target, _proxies);
                AddItem(builder, invocation, integrationType, delegateType, binding, "BeginSlow", _references.CallTargetState, target, new SZArraySig(_module.CorLibTypes.Object));
                break;
            }

            case CallTargetInvocationKind.EndVoid:
            {
                var delegateType = _references.DatadogGeneric($"{HandlersNamespace}.EndMethodHandler`2/InvokeDelegate", integrationType, target);
                var binding = IntegrationBinder.BindEndVoid(integration, target, _proxies);
                AddItem(builder, invocation, integrationType, delegateType, binding, "End", _references.CallTargetReturn, target, _references.Exception, state);
                break;
            }

            case CallTargetInvocationKind.EndReturn:
            {
                var returnType = invocation.ReturnType!;
                AddEndReturn(builder, invocation, integration, integrationType, returnType);
                AddContinuation(builder, invocation, integration, integrationType, returnType);
                break;
            }

            case CallTargetInvocationKind.EndRuntimeAsyncVoid:
            case CallTargetInvocationKind.EndRuntimeAsyncReturn:
            {
                // RuntimeAsyncEndMethodHandler: OnMethodEnd with the declared Task/ValueTask, OnAsyncMethodEnd with the
                // unwrapped value (object without one). All or nothing: it takes the AOT path when OnMethodEnd is registered.
                var declaredReturn = invocation.DeclaredReturnType!;
                var endBinding = IntegrationBinder.BindEndReturn(integration, target, declaredReturn, _proxies);
                var isVoid = invocation.Kind == CallTargetInvocationKind.EndRuntimeAsyncVoid;
                var result = isVoid ? _module.CorLibTypes.Object : invocation.ReturnType!;
                var asyncBinding = IntegrationBinder.BindAsyncEnd(integration, target, result, _proxies);
                if (endBinding.Status == AdapterBindingStatus.Deferred || asyncBinding.Status == AdapterBindingStatus.Deferred)
                {
                    Defer(builder, invocation, endBinding.Message ?? asyncBinding.Message);
                    break;
                }

                AddEndReturn(builder, invocation, integration, integrationType, declaredReturn, endBinding);
                var (syncKey, asyncKey) = isVoid
                    ? (_references.DatadogGeneric($"{ContinuationsNamespace}.ContinuationGenerator`2/ObjectContinuationMethodDelegate", target, result),
                       _references.DatadogGeneric($"{ContinuationsNamespace}.ContinuationGenerator`2/AsyncObjectContinuationMethodDelegate", target, result))
                    : (_references.DatadogGeneric($"{ContinuationsNamespace}.ContinuationGenerator`3/ContinuationMethodDelegate", target, result, result),
                       _references.DatadogGeneric($"{ContinuationsNamespace}.ContinuationGenerator`3/AsyncContinuationMethodDelegate", target, result, result));
                AddAsyncEnd(builder, invocation, integrationType, asyncBinding, target, result, syncKey, asyncKey);
                break;
            }
        }
    }

    private void Defer(RegistrationBuilder builder, CallTargetInvocation invocation, string? message)
    {
        builder.HasDeferred = true;
        _result.Record(AdapterBindingStatus.Deferred, builder.MethodName, invocation, message, builder.IsInstantiation);
    }

    private void AddEndReturn(RegistrationBuilder builder, CallTargetInvocation invocation, TypeDef integration, TypeSig integrationType, TypeSig returnType, AdapterBinding? binding = null)
    {
        var target = invocation.Target;
        var delegateType = _references.DatadogGeneric($"{HandlersNamespace}.EndMethodHandler`3/InvokeDelegate", integrationType, target, returnType);
        binding ??= IntegrationBinder.BindEndReturn(integration, target, returnType, _proxies);
        AddItem(builder, invocation, integrationType, delegateType, binding, "EndReturn", _references.CallTargetReturnOf(returnType), target, returnType, _references.Exception, new ByRefSig(_references.CallTargetState));
    }

    /// <summary>
    /// <c>EndMethodHandler&lt;TIntegration, TTarget, TReturn&gt;</c> continuation generators: a factory for Task&lt;T&gt; and
    /// ValueTask&lt;T&gt; (created with MakeGenericType otherwise) and the <c>OnAsyncMethodEnd</c> holders the
    /// generators read.
    /// </summary>
    private void AddContinuation(RegistrationBuilder builder, CallTargetInvocation invocation, TypeDef integration, TypeSig integrationType, TypeSig returnType)
    {
        var target = invocation.Target;
        if (returnType.FullName is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask")
        {
            var objectType = _module.CorLibTypes.Object;
            AddAsyncEnd(
                builder,
                invocation,
                integrationType,
                IntegrationBinder.BindAsyncEnd(integration, target, objectType, _proxies),
                target,
                objectType,
                _references.DatadogGeneric($"{ContinuationsNamespace}.ContinuationGenerator`2/ObjectContinuationMethodDelegate", target, returnType),
                _references.DatadogGeneric($"{ContinuationsNamespace}.ContinuationGenerator`2/AsyncObjectContinuationMethodDelegate", target, returnType));
            return;
        }

        if (returnType is GenericVar)
        {
            Defer(builder, invocation, "generic return type: a Task<T> or ValueTask<T> instantiation needs MakeGenericType for its continuation");
            return;
        }

        if (returnType is not GenericInstSig instance)
        {
            return;
        }

        string generator;
        switch (instance.GenericType.FullName)
        {
            case "System.Threading.Tasks.Task`1":
                generator = "TaskContinuationGenerator`4";
                break;
            case "System.Threading.Tasks.ValueTask`1":
                generator = "ValueTaskContinuationGenerator`4";
                break;
            default:
                if (!_derivesFromTask(returnType))
                {
                    return;
                }

                if (instance.GenericArguments.Count != 1)
                {
                    Defer(builder, invocation, $"continuation of {returnType.FullName}: result type with several generic arguments");
                    return;
                }

                generator = "TaskContinuationGenerator`4";
                break;
        }

        var result = instance.GenericArguments[0];
        var factory = new MethodDefUser(
            $"CreateContinuation{builder.Factories.Count}",
            MethodSig.CreateStatic(_references.DatadogGeneric($"{ContinuationsNamespace}.ContinuationGenerator`2", target, returnType)),
            MethodImplAttributes.IL | MethodImplAttributes.Managed,
            MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
        factory.Body = new CilBody();
        var generatorType = _references.DatadogGeneric($"{ContinuationsNamespace}.{generator}", integrationType, target, returnType, result);
        factory.Body.Instructions.Add(OpCodes.Newobj.ToInstruction(_references.Constructor(generatorType)));
        factory.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        Finish(factory.Body);
        builder.Registration.Methods.Add(factory);
        builder.Factories.Add(new FactoryItem(integrationType, target, returnType, factory));
        _result.ContinuationFactories++;

        AddAsyncEnd(
            builder,
            invocation,
            integrationType,
            IntegrationBinder.BindAsyncEnd(integration, target, result, _proxies),
            target,
            result,
            _references.DatadogGeneric($"{ContinuationsNamespace}.ContinuationGenerator`3/ContinuationMethodDelegate", target, returnType, result),
            _references.DatadogGeneric($"{ContinuationsNamespace}.ContinuationGenerator`3/AsyncContinuationMethodDelegate", target, returnType, result));
    }

    /// <summary>
    /// The generators read the async holder first (a task-returning <c>OnAsyncMethodEnd</c>), then the sync one (null:
    /// no-op); a failure goes to the async holder so it is the first thing they see.
    /// </summary>
    private void AddAsyncEnd(RegistrationBuilder builder, CallTargetInvocation invocation, TypeSig integrationType, AdapterBinding binding, TypeSig target, TypeSig result, TypeSig syncKey, TypeSig asyncKey)
    {
        var key = binding.Status switch
        {
            AdapterBindingStatus.Bound => binding.IsTaskReturn ? asyncKey : syncKey,
            AdapterBindingStatus.Failure => asyncKey,
            _ => syncKey,
        };
        var returnType = binding.IsTaskReturn ? _references.TaskOf(result) : result;
        AddItem(builder, invocation, integrationType, key, binding, "AsyncEnd", returnType, target, result, _references.Exception, new ByRefSig(_references.CallTargetState));
    }

    private void AddItem(RegistrationBuilder builder, CallTargetInvocation invocation, TypeSig integrationType, TypeSig delegateType, AdapterBinding binding, string kind, TypeSig returnType, params TypeSig[] parameters)
    {
        _result.Record(binding.Status, builder.MethodName, invocation, binding.Message, builder.IsInstantiation);
        if (binding.Status == AdapterBindingStatus.Deferred)
        {
            builder.HasDeferred = true;
            return;
        }

        MethodDef? adapter = null;
        if (binding.Status == AdapterBindingStatus.Bound)
        {
            adapter = new MethodDefUser(
                $"{kind}{builder.Items.Count}",
                MethodSig.CreateStatic(returnType, parameters),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig);
            builder.Registration.Methods.Add(adapter);
            EmitAdapter(adapter, binding);
        }

        builder.Items.Add(new RegistrationItem(integrationType, delegateType, binding, adapter));
    }

    /// <summary>
    /// The body <c>IntegrationMapper</c> emits into its dynamic method: load the parameters, call the integration method.
    /// </summary>
    private void EmitAdapter(MethodDef adapter, AdapterBinding binding)
    {
        var body = adapter.Body = new CilBody();
        var instructions = body.Instructions;
        foreach (var load in binding.Loads)
        {
            switch (load.Kind)
            {
                case AdapterLoadKind.Argument:
                    instructions.Add(OpCodes.Ldarg.ToInstruction(adapter.Parameters[load.Index]));
                    break;
                case AdapterLoadKind.ArgumentValue:
                    instructions.Add(OpCodes.Ldarg.ToInstruction(adapter.Parameters[load.Index]));
                    instructions.Add(OpCodes.Ldobj.ToInstruction(TypeOperand(load.Type!)));
                    break;
                default:
                    instructions.Add(OpCodes.Ldarg.ToInstruction(adapter.Parameters[1]));
                    instructions.Add(Instruction.CreateLdcI4(load.Index));
                    instructions.Add(OpCodes.Ldelem_Ref.ToInstruction());
                    if (load.Kind == AdapterLoadKind.ArrayElementUnbox)
                    {
                        instructions.Add(OpCodes.Unbox_Any.ToInstruction(TypeOperand(load.Type!)));
                    }
                    else if (load.Kind == AdapterLoadKind.ArrayElementConvert)
                    {
                        var convertType = new MemberRefUser(
                            _module,
                            "ConvertType",
                            MethodSig.CreateStaticGeneric(1, new GenericMVar(0), _module.CorLibTypes.Object),
                            _references.DatadogType($"{HandlersNamespace}.IntegrationMapper"));
                        instructions.Add(OpCodes.Call.ToInstruction(new MethodSpecUser(convertType, new GenericInstMethodSig(_references.Import(load.Type!)))));
                    }

                    break;
            }

            if (load.BoxType is { } boxType)
            {
                instructions.Add(OpCodes.Box.ToInstruction(TypeOperand(boxType)));
            }

            if (load.Proxy is { } proxy)
            {
                EmitCreateProxy(instructions, proxy, load.ProxySource!);
            }
        }

        var method = (IMethodDefOrRef)_references.Import(binding.Method!);
        var instantiation = new GenericInstMethodSig(binding.GenericArguments.Select(_references.Import).ToList());
        instructions.Add(OpCodes.Call.ToInstruction(new MethodSpecUser(method, instantiation)));
        if (binding.ReturnProxy is { } returnProxy)
        {
            EmitUnwrapReturnValue(body, binding, returnProxy);
        }

        instructions.Add(OpCodes.Ret.ToInstruction());
        Finish(body);
    }

    /// <summary>
    /// <c>IntegrationMapper.WriteCreateNewProxyInstance</c>: box a value type when the proxy stores an object, report the
    /// static type when the proxy also serves other runtime types.
    /// </summary>
    private void EmitCreateProxy(IList<Instruction> instructions, DuckProxy proxy, TypeSig source)
    {
        _usesProxies = true;
        var constructor = proxy.Constructor!;
        if (source.IsValueType && !constructor.MethodSig.Params[0].IsValueType)
        {
            instructions.Add(OpCodes.Box.ToInstruction(TypeOperand(source)));
        }

        if (proxy.ReportsTargetType)
        {
            instructions.Add(OpCodes.Ldtoken.ToInstruction(TypeOperand(source)));
            instructions.Add(OpCodes.Call.ToInstruction(_references.GetTypeFromHandle));
        }

        instructions.Add(OpCodes.Newobj.ToInstruction(_references.Import(constructor)));
    }

    /// <summary>
    /// <c>IntegrationMapper.UnwrapReturnValue</c> (<c>UnwrapTaskReturnValue</c> for a task-returning
    /// <c>OnAsyncMethodEnd</c>). <c>OnMethodEnd</c> returns <c>CallTargetReturn&lt;TProxy&gt;</c>: its value is unwrapped
    /// into a new <c>CallTargetReturn&lt;TReturn&gt;</c> (<c>IntegrationMapper</c> passes the struct as the proxy, which
    /// only works because of how the JIT passes a struct with a single reference).
    /// </summary>
    private void EmitUnwrapReturnValue(CilBody body, AdapterBinding binding, DuckProxy returnProxy)
    {
        var instructions = body.Instructions;
        var proxyType = _references.Import(returnProxy.Type!.ToTypeSig());
        var returnType = binding.ReturnType!;
        var mapper = _references.DatadogType($"{HandlersNamespace}.IntegrationMapper");
        if (binding.IsTaskReturn)
        {
            var unwrapTask = new MemberRefUser(
                _module,
                "UnwrapTaskReturnValue",
                MethodSig.CreateStaticGeneric(2, _references.TaskOf(new GenericMVar(1)), _references.TaskOf(new GenericMVar(0)), _module.CorLibTypes.Boolean),
                mapper);
            instructions.Add(Instruction.CreateLdcI4(binding.PreserveContext ? 1 : 0));
            instructions.Add(OpCodes.Call.ToInstruction(new MethodSpecUser(unwrapTask, new GenericInstMethodSig(proxyType, returnType))));
            return;
        }

        var unwrap = new MethodSpecUser(
            new MemberRefUser(_module, "UnwrapReturnValue", MethodSig.CreateStaticGeneric(2, new GenericMVar(1), new GenericMVar(0)), mapper),
            new GenericInstMethodSig(proxyType, returnType));
        if (binding.Method!.Name != IntegrationBinder.EndMethodName)
        {
            instructions.Add(OpCodes.Call.ToInstruction(unwrap));
            return;
        }

        var proxyReturn = _references.CallTargetReturnOf(proxyType);
        var local = new Local(proxyReturn);
        body.Variables.Add(local);
        body.InitLocals = true;
        instructions.Add(OpCodes.Stloc.ToInstruction(local));
        instructions.Add(OpCodes.Ldloca.ToInstruction(local));
        instructions.Add(OpCodes.Call.ToInstruction(new MemberRefUser(_module, "GetReturnValue", MethodSig.CreateInstance(new GenericVar(0)), new TypeSpecUser(proxyReturn))));
        instructions.Add(OpCodes.Call.ToInstruction(unwrap));
        instructions.Add(OpCodes.Newobj.ToInstruction(_references.Constructor(_references.CallTargetReturnOf(returnType), new GenericVar(0))));
    }

    /// <summary>
    /// The module initializer of an instrumented assembly, before its own code: the DuckType AOT registry must enable the
    /// AOT mode before anything uses duck typing (also under the JIT, where the registry's own module initializer would
    /// run too late), then the application's assembly initializes the instrumentation (<c>Instrumentation.InitializeAot</c>).
    /// Each step is isolated: a failure is logged and the application starts anyway.
    /// </summary>
    private void AddModuleInitializer(CallTargetDuckTypeRegistry? registry, bool initializeInstrumentation, IReadOnlyList<(string Location, string Value)>? userStrings, string? sourceLink)
    {
        var steps = new List<(string Name, List<Instruction> Body)>();
        if (registry?.Module.Find(CallTargetDuckTypeRegistry.BootstrapTypeName, isReflectionName: true)?.FindMethod("Initialize") is { } registryInitialize)
        {
            steps.Add(("DuckTypeRegistry", [OpCodes.Call.ToInstruction(_references.Import(registryInitialize))]));
        }

        if (userStrings is { Count: > 0 })
        {
            // HardcodedSecretsAnalyzer.AddBuildTimeUserStrings(new[] { location0, value0, location1, value1, ... })
            var analyzer = new ClassSig(_references.DatadogType("Datadog.Trace.Iast.Analyzers.HardcodedSecretsAnalyzer"));
            var stringArray = new SZArraySig(_module.CorLibTypes.String);
            var add = _references.DatadogStaticMethod(analyzer, "AddBuildTimeUserStrings", MethodSig.CreateStatic(_module.CorLibTypes.Void, stringArray));
            var userStringsBody = new List<Instruction> { Instruction.CreateLdcI4(userStrings.Count * 2), OpCodes.Newarr.ToInstruction(_module.CorLibTypes.String.TypeDefOrRef) };
            var index = 0;
            foreach (var (location, value) in userStrings)
            {
                foreach (var item in new[] { location, value })
                {
                    userStringsBody.Add(OpCodes.Dup.ToInstruction());
                    userStringsBody.Add(Instruction.CreateLdcI4(index++));
                    userStringsBody.Add(OpCodes.Ldstr.ToInstruction(item));
                    userStringsBody.Add(OpCodes.Stelem_Ref.ToInstruction());
                }
            }

            userStringsBody.Add(OpCodes.Call.ToInstruction(add));
            steps.Add(("UserStrings", userStringsBody));
        }

        if (sourceLink is not null && _module.Assembly is { } assembly)
        {
            // SourceLinkInformationExtractor.AddBuildTimeSourceLink(assemblyName, sourceLink): the git metadata of the PDB.
            var extractor = new ClassSig(_references.DatadogType("Datadog.Trace.Pdb.SourceLinkInformationExtractor"));
            var add = _references.DatadogStaticMethod(extractor, "AddBuildTimeSourceLink", MethodSig.CreateStatic(_module.CorLibTypes.Void, _module.CorLibTypes.String, _module.CorLibTypes.String));
            steps.Add(("SourceLink", [OpCodes.Ldstr.ToInstruction(assembly.Name.String), OpCodes.Ldstr.ToInstruction(sourceLink), OpCodes.Call.ToInstruction(add)]));
        }

        if (initializeInstrumentation)
        {
            var instrumentation = new ClassSig(_references.DatadogType("Datadog.Trace.ClrProfiler.Instrumentation"));
            steps.Add(("Instrumentation", [OpCodes.Call.ToInstruction(_references.DatadogStaticMethod(instrumentation, "InitializeAot", MethodSig.CreateStatic(_module.CorLibTypes.Void)))]));
        }

        if (steps.Count == 0)
        {
            return;
        }

        var globalType = _module.GlobalType;
        var initialize = new MethodDefUser(
            "<DatadogAot>Initialize",
            MethodSig.CreateStatic(_module.CorLibTypes.Void),
            MethodImplAttributes.IL | MethodImplAttributes.Managed,
            MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig);
        var body = initialize.Body = new CilBody();
        var registryHelpers = new ClassSig(_references.DatadogType($"{HandlersNamespace}.CallTargetAotRegistry"));
        var logError = _references.DatadogStaticMethod(registryHelpers, "LogInitializationError", MethodSig.CreateStatic(_module.CorLibTypes.Void, _references.Exception));
        foreach (var (name, stepBody) in steps)
        {
            // The call is in its own method: the JIT resolves it (and loads its assembly) when it compiles the method that
            // contains it, which must be inside the try block.
            var call = new MethodDefUser(
                $"<DatadogAot>Initialize{name}",
                MethodSig.CreateStatic(_module.CorLibTypes.Void),
                MethodImplAttributes.IL | MethodImplAttributes.Managed | MethodImplAttributes.NoInlining,
                MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig);
            call.Body = new CilBody();
            foreach (var instruction in stepBody)
            {
                call.Body.Instructions.Add(instruction);
            }

            call.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            Finish(call.Body);
            globalType.Methods.Add(call);

            var next = OpCodes.Nop.ToInstruction();
            var tryStart = OpCodes.Call.ToInstruction(call);
            var handlerStart = OpCodes.Call.ToInstruction(logError);
            body.Instructions.Add(tryStart);
            body.Instructions.Add(OpCodes.Leave.ToInstruction(next));
            body.Instructions.Add(handlerStart);
            body.Instructions.Add(OpCodes.Leave.ToInstruction(next));
            body.Instructions.Add(next);
            body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Catch)
            {
                TryStart = tryStart,
                TryEnd = handlerStart,
                HandlerStart = handlerStart,
                HandlerEnd = next,
                CatchType = CallTargetReferences.ToTypeDefOrRef(_references.Exception),
            });
        }

        body.Instructions.Add(OpCodes.Ret.ToInstruction());
        Finish(body);
        globalType.Methods.Add(initialize);

        var initializer = globalType.FindStaticConstructor();
        if (initializer is null)
        {
            initializer = new MethodDefUser(
                ".cctor",
                MethodSig.CreateStatic(_module.CorLibTypes.Void),
                MethodImplAttributes.IL | MethodImplAttributes.Managed,
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig);
            initializer.Body = new CilBody();
            initializer.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            initializer.Body.MaxStack = 8;
            globalType.Methods.Add(initializer);
        }

        initializer.Body.Instructions.Insert(0, OpCodes.Call.ToInstruction(initialize));
    }

    /// <summary>
    /// The registration: for every holder, run the runtime checks and register the adapter, a no-op or a failure; then
    /// the continuation factories. An unexpected exception is logged and leaves the rest unregistered.
    /// </summary>
    private void EmitStaticConstructor(RegistrationBuilder builder)
    {
        var registration = builder.Registration;
        var cctor = new MethodDefUser(
            ".cctor",
            MethodSig.CreateStatic(_module.CorLibTypes.Void),
            MethodImplAttributes.IL | MethodImplAttributes.Managed,
            MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig);
        registration.Methods.Add(cctor);
        var body = cctor.Body = new CilBody();
        var instructions = body.Instructions;
        var end = OpCodes.Ret.ToInstruction();
        var registry = new ClassSig(_references.DatadogType($"{HandlersNamespace}.CallTargetAotRegistry"));
        var type = _references.Type;
        var canAssign = _references.DatadogStaticMethod(registry, "CanAssign", MethodSig.CreateStatic(_module.CorLibTypes.Boolean, type, type));
        var isSameType = _references.DatadogStaticMethod(registry, "IsSameType", MethodSig.CreateStatic(_module.CorLibTypes.Boolean, type, type));
        var categories = new ClassSig(_references.DatadogType($"{HandlersNamespace}.CallTargetAotCategories"));
        var registerIntegration = _references.DatadogStaticMethod(categories, "Register", MethodSig.CreateStaticGeneric(2, _module.CorLibTypes.Void, _module.CorLibTypes.UInt32));

        foreach (var integration in builder.Integrations.Values)
        {
            instructions.Add(Instruction.CreateLdcI4((int)integration.Categories));
            instructions.Add(OpCodes.Call.ToInstruction(new MethodSpecUser(registerIntegration, new GenericInstMethodSig(integration.Integration, integration.Target))));
        }

        foreach (var item in builder.Items)
        {
            var next = OpCodes.Nop.ToInstruction();
            var holder = _references.DatadogGeneric($"{HandlersNamespace}.CallTargetAot`2", item.Integration, item.Delegate);
            var register = _references.DatadogStaticMethod(holder, "Register", MethodSig.CreateStatic(_module.CorLibTypes.Void, new GenericVar(1), _module.CorLibTypes.Boolean));
            var registerFailure = _references.DatadogStaticMethod(holder, "RegisterFailure", MethodSig.CreateStatic(_module.CorLibTypes.Void, _module.CorLibTypes.String));
            var binding = item.Binding;
            if (binding.Status == AdapterBindingStatus.Failure)
            {
                instructions.Add(OpCodes.Ldstr.ToInstruction(binding.Message ?? "Invalid integration method."));
                instructions.Add(OpCodes.Call.ToInstruction(registerFailure));
                continue;
            }

            foreach (var check in binding.Checks)
            {
                var passed = OpCodes.Nop.ToInstruction();
                instructions.Add(OpCodes.Ldtoken.ToInstruction(TypeOperand(check.Left)));
                instructions.Add(OpCodes.Call.ToInstruction(_references.GetTypeFromHandle));
                instructions.Add(OpCodes.Ldtoken.ToInstruction(TypeOperand(check.Right)));
                instructions.Add(OpCodes.Call.ToInstruction(_references.GetTypeFromHandle));
                instructions.Add(OpCodes.Call.ToInstruction(check.SameType ? isSameType : canAssign));
                instructions.Add(OpCodes.Brtrue.ToInstruction(passed));
                instructions.Add(OpCodes.Ldstr.ToInstruction(check.Message));
                instructions.Add(OpCodes.Call.ToInstruction(registerFailure));
                instructions.Add(OpCodes.Br.ToInstruction(next));
                instructions.Add(passed);
            }

            instructions.Add(OpCodes.Ldnull.ToInstruction());
            if (item.Adapter is { } adapter)
            {
                instructions.Add(OpCodes.Ldftn.ToInstruction(SelfReference(registration, adapter)));
                instructions.Add(OpCodes.Newobj.ToInstruction(_references.Constructor(item.Delegate, _module.CorLibTypes.Object, _module.CorLibTypes.IntPtr)));
            }

            instructions.Add(Instruction.CreateLdcI4(binding.PreserveContext ? 1 : 0));
            instructions.Add(OpCodes.Call.ToInstruction(register));
            instructions.Add(next);
        }

        foreach (var factory in builder.Factories)
        {
            var holder = _references.DatadogGeneric($"{HandlersNamespace}.CallTargetAotContinuation`3", factory.Integration, factory.Target, factory.Return);
            var generatorType = _references.DatadogGeneric($"{ContinuationsNamespace}.ContinuationGenerator`2", new GenericVar(1), new GenericVar(2));
            var register = _references.DatadogStaticMethod(holder, "Register", MethodSig.CreateStatic(_module.CorLibTypes.Void, _references.FuncOf(generatorType)));
            var funcType = _references.FuncOf(_references.DatadogGeneric($"{ContinuationsNamespace}.ContinuationGenerator`2", factory.Target, factory.Return));
            instructions.Add(OpCodes.Ldnull.ToInstruction());
            instructions.Add(OpCodes.Ldftn.ToInstruction(SelfReference(registration, factory.Factory)));
            instructions.Add(OpCodes.Newobj.ToInstruction(_references.Constructor(funcType, _module.CorLibTypes.Object, _module.CorLibTypes.IntPtr)));
            instructions.Add(OpCodes.Call.ToInstruction(register));
        }

        var leave = OpCodes.Leave.ToInstruction(end);
        instructions.Add(leave);
        var handlerStart = OpCodes.Call.ToInstruction(_references.DatadogStaticMethod(registry, "LogRegistrationError", MethodSig.CreateStatic(_module.CorLibTypes.Void, _references.Exception)));
        instructions.Add(handlerStart);
        instructions.Add(OpCodes.Leave.ToInstruction(end));
        instructions.Add(end);
        body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Catch)
        {
            TryStart = instructions[0],
            TryEnd = handlerStart,
            HandlerStart = handlerStart,
            HandlerEnd = end,
            CatchType = CallTargetReferences.ToTypeDefOrRef(_references.Exception),
        });
        Finish(body);
    }

    /// <summary>
    /// A method of the registration type in its own generic context (<c>Registration&lt;!0, ...&gt;::Method</c>).
    /// </summary>
    private IMethod SelfReference(TypeDef registration, MethodDef method)
    {
        if (registration.GenericParameters.Count == 0)
        {
            return method;
        }

        var context = registration.GenericParameters.Select(p => (TypeSig)new GenericVar(p.Number, registration)).ToList();
        return new MemberRefUser(_module, method.Name, method.MethodSig, new TypeSpecUser(new GenericInstSig(new ClassSig(registration), context)));
    }

    /// <summary>
    /// The adapters replace <c>IntegrationMapper</c>'s dynamic methods, which stack traces don't show.
    /// </summary>
    private void HideFromStackTraces(TypeDef registration)
    {
        var attribute = _module.CorLibTypes.GetTypeRef("System.Diagnostics", "StackTraceHiddenAttribute");
        if (_resolveType(attribute) is null)
        {
            // Before .NET 6.
            return;
        }

        var constructor = new MemberRefUser(_module, ".ctor", MethodSig.CreateInstance(_module.CorLibTypes.Void), attribute);
        registration.CustomAttributes.Add(new CustomAttribute(constructor));
    }

    private ITypeDefOrRef TypeOperand(TypeSig type) => CallTargetReferences.ToTypeDefOrRef(_references.Import(type));

    /// <summary>
    /// Integration methods and handler types are internal to their assemblies more often than not.
    /// </summary>
    private void AddIgnoresAccessChecks(bool force)
    {
        if ((_result.Registrations == 0 && !force) || _module.Assembly is not { } assembly)
        {
            return;
        }

        const string attributeName = "System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute";
        var constructor = _references.Constructor(new ClassSig(_references.DatadogType(attributeName)), _module.CorLibTypes.String);
        foreach (var name in new[] { "Datadog.Trace" }.Concat(_accessedAssemblies).Distinct(StringComparer.Ordinal))
        {
            var present = assembly.CustomAttributes.Any(a => a.TypeFullName == attributeName && a.ConstructorArguments.Count == 1 && a.ConstructorArguments[0].Value?.ToString() == name);
            if (!present)
            {
                assembly.CustomAttributes.Add(new CustomAttribute(constructor, new[] { new CAArgument(_module.CorLibTypes.String, new UTF8String(name)) }));
            }
        }
    }

    private sealed class RegistrationBuilder
    {
        public RegistrationBuilder(TypeDef registration, string methodName)
        {
            Registration = registration;
            MethodName = methodName;
        }

        public TypeDef Registration { get; }

        public string MethodName { get; }

        public List<RegistrationItem> Items { get; } = new();

        public List<FactoryItem> Factories { get; } = new();

        /// <summary>Gets the integrations of the method by integration and target, with their instrumentation categories.</summary>
        public Dictionary<string, IntegrationItem> Integrations { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets or sets a value indicating whether this registration serves closed instantiations of a generic target.</summary>
        public bool IsInstantiation { get; set; }

        /// <summary>Gets or sets a value indicating whether a shape of the method was deferred.</summary>
        public bool HasDeferred { get; set; }
    }

    private sealed class RegistrationItem
    {
        public RegistrationItem(TypeSig integration, TypeSig @delegate, AdapterBinding binding, MethodDef? adapter)
        {
            Integration = integration;
            Delegate = @delegate;
            Binding = binding;
            Adapter = adapter;
        }

        public TypeSig Integration { get; }

        public TypeSig Delegate { get; }

        public AdapterBinding Binding { get; }

        public MethodDef? Adapter { get; }
    }

    private sealed class IntegrationItem
    {
        public IntegrationItem(TypeSig integration, TypeSig target, uint categories)
        {
            Integration = integration;
            Target = target;
            Categories = categories;
        }

        public TypeSig Integration { get; }

        public TypeSig Target { get; }

        public uint Categories { get; }
    }

    private sealed class FactoryItem
    {
        public FactoryItem(TypeSig integration, TypeSig target, TypeSig @return, MethodDef factory)
        {
            Integration = integration;
            Target = target;
            Return = @return;
            Factory = factory;
        }

        public TypeSig Integration { get; }

        public TypeSig Target { get; }

        public TypeSig Return { get; }

        public MethodDef Factory { get; }
    }
}
#endif
