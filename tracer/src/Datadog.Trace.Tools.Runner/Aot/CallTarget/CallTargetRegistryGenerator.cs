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
    private readonly CallTargetRegistryResult _result = new();
    private readonly SortedSet<string> _accessedAssemblies = new(StringComparer.Ordinal);

    private CallTargetRegistryGenerator(ModuleDef module, CallTargetReferences references, Func<ITypeDefOrRef, TypeDef?> resolveType, Func<TypeSig, bool> derivesFromTask)
    {
        _module = module;
        _references = references;
        _resolveType = resolveType;
        _derivesFromTask = derivesFromTask;
    }

    /// <param name="module">The instrumented module, with the rewritten bodies already set.</param>
    /// <param name="rewrittenMethods">The methods the native rewriter instrumented.</param>
    /// <param name="datadogTrace">The <c>Datadog.Trace</c> module the application ships.</param>
    /// <param name="resolveType">Resolves a type reference across the loaded modules.</param>
    public static CallTargetRegistryResult Generate(ModuleDef module, IEnumerable<MethodDef> rewrittenMethods, ModuleDef datadogTrace, Func<ITypeDefOrRef, TypeDef?> resolveType)
    {
        var scanned = rewrittenMethods.Where(m => m.Body is not null)
                                      .Select(m => (Method: m, Invocations: CallTargetInvocationScanner.Scan(m.Body)))
                                      .Where(s => s.Invocations.Count > 0)
                                      .ToList();
        if (scanned.Count == 0 || FindDatadogTraceScope(scanned[0].Method.Body) is not { } scope)
        {
            return new CallTargetRegistryResult();
        }

        var references = new CallTargetReferences(module, datadogTrace, scope);
        var generator = new CallTargetRegistryGenerator(module, references, resolveType, type => DerivesFromTask(type, resolveType));
        foreach (var (method, invocations) in scanned)
        {
            generator.AddRegistration(method, invocations);
        }

        generator.AddIgnoresAccessChecks();
        return generator._result;
    }

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
        var name = $"{RegistrationTypePrefix}{_result.Registrations}" + (arity > 0 ? $"`{arity}" : string.Empty);

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
                _result.Record(AdapterBindingStatus.Deferred, method.FullName, invocation, ex.Message);
                continue;
            }

            AddInvocation(builder, remapped);
        }

        if (builder.Items.Count == 0 && builder.Factories.Count == 0)
        {
            return;
        }

        _module.Types.Add(registration);
        _result.Registrations++;
        HideFromStackTraces(registration);
        EmitStaticConstructor(builder);
        var ensure = new MethodDefUser("Ensure", MethodSig.CreateStatic(_module.CorLibTypes.Void), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig);
        ensure.Body = new CilBody();
        ensure.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
        registration.Methods.Add(ensure);

        IMethod ensureReference = ensure;
        if (arity > 0)
        {
            var context = new List<TypeSig>();
            context.AddRange(typeParameters.Select(p => (TypeSig)new GenericVar(p.Number, method.DeclaringType)));
            context.AddRange(methodParameters.Select(p => (TypeSig)new GenericMVar(p.Number, method)));
            ensureReference = new MemberRefUser(_module, ensure.Name, ensure.MethodSig, new TypeSpecUser(new GenericInstSig(new ClassSig(registration), context)));
        }

        // First instruction: branches and protected regions keep pointing at the original first instruction.
        method.Body.Instructions.Insert(0, OpCodes.Call.ToInstruction(ensureReference));
    }

    private void AddInvocation(RegistrationBuilder builder, CallTargetInvocation invocation)
    {
        var integrationType = invocation.Integration;
        var integration = integrationType is TypeDefOrRefSig { TypeDefOrRef: { } reference } ? _resolveType(reference) : null;
        if (integration is null)
        {
            _result.Record(AdapterBindingStatus.Deferred, builder.MethodName, invocation, integrationType is GenericInstSig ? "generic integration type" : "the integration type can't be resolved");
            return;
        }

        if (integration.Module != _module && integration.Module?.Assembly?.Name is { } assemblyName)
        {
            _accessedAssemblies.Add(assemblyName);
        }

        var target = invocation.Target;
        var state = new ByRefSig(_references.CallTargetState);
        switch (invocation.Kind)
        {
            case CallTargetInvocationKind.Begin:
            {
                var arguments = invocation.Arguments;
                var handler = $"{HandlersNamespace}.BeginMethodHandler`{arguments.Count + 2}/InvokeDelegate";
                var delegateType = _references.DatadogGeneric(handler, new[] { integrationType, target }.Concat(arguments).ToArray());
                var binding = IntegrationBinder.BindBegin(integration, target, arguments);
                var parameters = new[] { target }.Concat(arguments.Select(a => (TypeSig)new ByRefSig(a))).ToArray();
                AddItem(builder, invocation, integrationType, delegateType, binding, "Begin", _references.CallTargetState, parameters);
                break;
            }

            case CallTargetInvocationKind.BeginSlow:
            {
                var delegateType = _references.DatadogGeneric($"{HandlersNamespace}.BeginMethodSlowHandler`2/InvokeDelegate", integrationType, target);
                var binding = IntegrationBinder.BindSlowBegin(integration, target);
                AddItem(builder, invocation, integrationType, delegateType, binding, "BeginSlow", _references.CallTargetState, target, new SZArraySig(_module.CorLibTypes.Object));
                break;
            }

            case CallTargetInvocationKind.EndVoid:
            {
                var delegateType = _references.DatadogGeneric($"{HandlersNamespace}.EndMethodHandler`2/InvokeDelegate", integrationType, target);
                var binding = IntegrationBinder.BindEndVoid(integration, target);
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
                var endBinding = IntegrationBinder.BindEndReturn(integration, target, declaredReturn);
                var isVoid = invocation.Kind == CallTargetInvocationKind.EndRuntimeAsyncVoid;
                var result = isVoid ? _module.CorLibTypes.Object : invocation.ReturnType!;
                var asyncBinding = IntegrationBinder.BindAsyncEnd(integration, target, result);
                if (endBinding.Status == AdapterBindingStatus.Deferred || asyncBinding.Status == AdapterBindingStatus.Deferred)
                {
                    _result.Record(AdapterBindingStatus.Deferred, builder.MethodName, invocation, endBinding.Message ?? asyncBinding.Message);
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

    private void AddEndReturn(RegistrationBuilder builder, CallTargetInvocation invocation, TypeDef integration, TypeSig integrationType, TypeSig returnType, AdapterBinding? binding = null)
    {
        var target = invocation.Target;
        var delegateType = _references.DatadogGeneric($"{HandlersNamespace}.EndMethodHandler`3/InvokeDelegate", integrationType, target, returnType);
        binding ??= IntegrationBinder.BindEndReturn(integration, target, returnType);
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
                IntegrationBinder.BindAsyncEnd(integration, target, objectType),
                target,
                objectType,
                _references.DatadogGeneric($"{ContinuationsNamespace}.ContinuationGenerator`2/ObjectContinuationMethodDelegate", target, returnType),
                _references.DatadogGeneric($"{ContinuationsNamespace}.ContinuationGenerator`2/AsyncObjectContinuationMethodDelegate", target, returnType));
            return;
        }

        if (returnType is GenericVar)
        {
            _result.Record(AdapterBindingStatus.Deferred, builder.MethodName, invocation, "generic return type: a Task<T> or ValueTask<T> instantiation needs MakeGenericType for its continuation");
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
                    _result.Record(AdapterBindingStatus.Deferred, builder.MethodName, invocation, $"continuation of {returnType.FullName}: result type with several generic arguments");
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
            IntegrationBinder.BindAsyncEnd(integration, target, result),
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
        _result.Record(binding.Status, builder.MethodName, invocation, binding.Message);
        if (binding.Status == AdapterBindingStatus.Deferred)
        {
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
        }

        var method = (IMethodDefOrRef)_references.Import(binding.Method!);
        var instantiation = new GenericInstMethodSig(binding.GenericArguments.Select(_references.Import).ToList());
        instructions.Add(OpCodes.Call.ToInstruction(new MethodSpecUser(method, instantiation)));
        instructions.Add(OpCodes.Ret.ToInstruction());
        Finish(body);
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
    private void AddIgnoresAccessChecks()
    {
        if (_result.Registrations == 0 || _module.Assembly is not { } assembly)
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
