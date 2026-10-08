// <copyright file="CallTargetRegistryGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tools.Runner.Aot.CallTarget;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using FluentAssertions;
using Xunit;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace Datadog.Trace.Tools.Runner.Tests;

/// <summary>
/// Builds modules whose methods call <c>CallTargetInvoker</c> like the native rewriter leaves them, generates their
/// registrations, and runs them under the JIT: the handlers must use the generated adapters (not
/// <c>IntegrationMapper</c>) and behave as with the dynamic path.
/// </summary>
public class CallTargetRegistryGeneratorTests
{
    private const string RegistrationPrefix = CallTargetRegistryGenerator.RegistrationTypePrefix;

    public CallTargetRegistryGeneratorTests()
    {
        // What Instrumentation.InitializeAot does: the probe integrations are of the default category.
        CallTargetAotCategories.Enable(InstrumentationCategory.Tracing);
    }

    [Theory]
    [InlineData("Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient.HttpClientHandler.HttpClientHandlerIntegration", 1u)] // Tracing
    [InlineData("Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.MvcOptionsIntegration", 2u)] // AppSec
    [InlineData("Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore.DefaultModelBindingContext_SetResult_Integration", 6u)] // AppSec | Iast
    [InlineData("Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.CommandExecuteReaderIntegration", 1u)] // Tracing
    [InlineData("Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.ReaderReadIntegration", 4u)] // Iast
    public void IntegrationsHaveTheCategoriesOfTheirDefinitions(string integration, uint categories)
    {
        var datadogTrace = ModuleDefMD.Load(typeof(Tracer).Assembly.Location);
        var type = datadogTrace.Find(integration, isReflectionName: false);
        type.Should().NotBeNull();
        IntegrationCategories.Get(datadogTrace, type!).Should().Be(categories);
    }

    [Fact]
    public void IntegrationFollowsItsCategory()
    {
        var probe = new ProbeModule(nameof(IntegrationFollowsItsCategory));
        var target = probe.AddType("Target");
        var run = probe.AddEchoMethod(target, "Run", typeof(RefArgumentIntegration), probe.Module.CorLibTypes.String);

        var (assembly, _) = probe.Generate(run);
        var targetType = assembly.GetType("Probe.Target")!;
        var instance = Activator.CreateInstance(targetType);
        try
        {
            CallTargetAotCategories.Disable(InstrumentationCategory.Tracing);
            targetType.GetMethod("Run")!.Invoke(instance, new object[] { "hello" }).Should().Be("hello", "the registration registers the integration before its first call");

            CallTargetAotCategories.Enable(InstrumentationCategory.Tracing);
            targetType.GetMethod("Run")!.Invoke(instance, new object[] { "hello" }).Should().Be("begin:hello|end");
        }
        finally
        {
            CallTargetAotCategories.Enable(InstrumentationCategory.Tracing);
        }
    }

    [Fact]
    public void ClosedTargetUsesTheGeneratedAdapters()
    {
        var probe = new ProbeModule(nameof(ClosedTargetUsesTheGeneratedAdapters));
        var target = probe.AddType("Target");
        var run = probe.AddEchoMethod(target, "Run", typeof(RefArgumentIntegration), probe.Module.CorLibTypes.String);

        var (assembly, result) = probe.Generate(run);
        result.Registrations.Should().Be(1);
        result.Bound.Should().Be(2);

        var targetType = assembly.GetType("Probe.Target")!;
        var output = targetType.GetMethod("Run")!.Invoke(Activator.CreateInstance(targetType), new object[] { "hello" });

        // OnMethodBegin replaced the by-ref argument and OnMethodEnd the return value.
        output.Should().Be("begin:hello|end");
        AssertAdapter(typeof(BeginMethodHandler<,,>).MakeGenericType(typeof(RefArgumentIntegration), targetType, typeof(string)));
        AssertAdapter(typeof(EndMethodHandler<,,>).MakeGenericType(typeof(RefArgumentIntegration), targetType, typeof(string)));
    }

    [Fact]
    public void GenericTargetsBindThroughTheirGenericContext()
    {
        var probe = new ProbeModule(nameof(GenericTargetsBindThroughTheirGenericContext));
        var genericType = probe.AddType("GenericTarget`1", genericParameters: 1);
        probe.AddEchoMethod(genericType, "Echo", typeof(RecordingIntegration), new GenericVar(0, genericType));
        var target = probe.AddType("Target");
        var genericMethod = probe.AddEchoMethod(target, "EchoGeneric", typeof(RecordingIntegration), null);

        var (assembly, result) = probe.Generate(genericType.Methods.Single(m => m.Name == "Echo"), genericMethod);
        result.Registrations.Should().Be(2);
        result.Bound.Should().Be(4);

        foreach (var value in new object[] { 42, "text" })
        {
            var closedType = assembly.GetType("Probe.GenericTarget`1")!.MakeGenericType(value.GetType());
            closedType.GetMethod("Echo")!.Invoke(Activator.CreateInstance(closedType), new[] { value }).Should().Be(value);
            AssertAdapter(typeof(BeginMethodHandler<,,>).MakeGenericType(typeof(RecordingIntegration), closedType, value.GetType()));

            var targetType = assembly.GetType("Probe.Target")!;
            var method = targetType.GetMethod("EchoGeneric")!.MakeGenericMethod(value.GetType());
            method.Invoke(Activator.CreateInstance(targetType), new[] { value }).Should().Be(value);
            AssertAdapter(typeof(EndMethodHandler<,,>).MakeGenericType(typeof(RecordingIntegration), targetType, value.GetType()));
            RecordingIntegration.Calls.Should().Contain($"begin {closedType.Name} {value}").And.Contain($"begin Target {value}");
        }
    }

    [Fact]
    public async Task TaskOfTContinuationComesFromTheRegistration()
    {
        var probe = new ProbeModule(nameof(TaskOfTContinuationComesFromTheRegistration));
        var target = probe.AddType("Target");
        var method = probe.AddTaskMethod(target, "RunAsync", typeof(AsyncIntegration));

        var (assembly, result) = probe.Generate(method);
        result.ContinuationFactories.Should().Be(1);
        result.Bound.Should().Be(1, "OnAsyncMethodEnd");
        result.NoMethod.Should().Be(2, "no OnMethodBegin nor OnMethodEnd");

        var targetType = assembly.GetType("Probe.Target")!;
        var task = (Task<int>)targetType.GetMethod("RunAsync")!.Invoke(Activator.CreateInstance(targetType), new object[] { 41 })!;
        (await task).Should().Be(42);

        var holder = typeof(CallTargetAotContinuation<,,>).MakeGenericType(typeof(AsyncIntegration), targetType, typeof(Task<int>));
        var arguments = new object?[] { null };
        ((bool)holder.GetMethod("TryGet", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments)!).Should().BeTrue();
        ((Delegate)arguments[0]!).Method.DeclaringType!.Name.Should().StartWith(RegistrationPrefix);
    }

    [Fact]
    public void InvalidIntegrationFailsLikeIntegrationMapper()
    {
        var probe = new ProbeModule(nameof(InvalidIntegrationFailsLikeIntegrationMapper));
        var target = probe.AddType("Target");
        var run = probe.AddEchoMethod(target, "Run", typeof(AmbiguousIntegration), probe.Module.CorLibTypes.String);

        var (assembly, result) = probe.Generate(run);
        result.Failures.Should().Be(1);
        result.NoMethod.Should().Be(1);

        var targetType = assembly.GetType("Probe.Target")!;
        Action invoke = () => targetType.GetMethod("Run")!.Invoke(Activator.CreateInstance(targetType), new object[] { "hello" });
        var exception = invoke.Should().Throw<TargetInvocationException>().Which.InnerException;
        exception.Should().BeOfType<TypeInitializationException>();
        exception!.InnerException.Should().BeOfType<CallTargetInvokerException>();
        exception.InnerException!.InnerException!.Message.Should().Contain("Ambiguous match found");
    }

    [Fact]
    public void DuckTypingShapesFallBackToIntegrationMapper()
    {
        var probe = new ProbeModule(nameof(DuckTypingShapesFallBackToIntegrationMapper));
        var target = probe.AddType("Target");
        var run = probe.AddEchoMethod(target, "Run", typeof(DuckIntegration), probe.Module.CorLibTypes.String);

        var (assembly, result) = probe.Generate(run);
        result.Deferred.Should().Be(2);
        result.Registrations.Should().Be(1, "the registration still registers the integration with its categories");

        var targetType = assembly.GetType("Probe.Target")!;
        targetType.GetMethod("Run")!.Invoke(Activator.CreateInstance(targetType), new object[] { "hello" }).Should().Be("hello");
        DuckIntegration.Calls.Should().Be(1);
        var handler = typeof(BeginMethodHandler<,,>).MakeGenericType(typeof(DuckIntegration), targetType, typeof(string));
        InvokeDelegateOf(handler).Method.DeclaringType.Should().BeNull("IntegrationMapper creates a dynamic method");
    }

    private static Delegate InvokeDelegateOf(Type handler)
        => (Delegate)handler.GetField("_invokeDelegate", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    private static void AssertAdapter(Type handler)
        => InvokeDelegateOf(handler).Method.DeclaringType!.Name.Should().StartWith(RegistrationPrefix, $"{handler} must use the generated adapter");

    // Public, like the real integrations.
    public sealed class DuckIntegration
    {
        public interface ITarget
        {
        }

        public static int Calls { get; private set; }

        internal static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, TArg1 value)
            where TTarget : ITarget, IDuckType
        {
            Calls++;
            return CallTargetState.GetDefault();
        }

        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
            where TTarget : ITarget, IDuckType
            => new(returnValue);
    }

    internal sealed class RefArgumentIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, ref TArg1 value)
        {
            value = (TArg1)(object)$"begin:{value}";
            return CallTargetState.GetDefault();
        }

        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
            => new((TReturn)(object)$"{returnValue}|end");
    }

    internal sealed class RecordingIntegration
    {
        public static ConcurrentBag<string> Calls { get; } = new();

        internal static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, TArg1 value)
        {
            Calls.Add($"begin {typeof(TTarget).Name} {value}");
            return CallTargetState.GetDefault();
        }

        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
            => new(returnValue);
    }

    internal sealed class AsyncIntegration
    {
        [PreserveContext]
        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
            => (TReturn)(object)((int)(object)returnValue! + 1);
    }

    internal sealed class AmbiguousIntegration
    {
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance) => CallTargetState.GetDefault();

        internal static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, TArg1 value) => CallTargetState.GetDefault();
    }

    /// <summary>
    /// A module with methods rewritten the way the native tracer does it, minus the exception handling.
    /// </summary>
    private sealed class ProbeModule
    {
        private readonly Importer _importer;

        public ProbeModule(string name)
        {
            Module = new ModuleDefUser(name + ".dll", Guid.NewGuid(), new AssemblyRefUser(typeof(object).Assembly.GetName())) { Kind = ModuleKind.Dll };
            new AssemblyDefUser(name, new Version(1, 0, 0, 0)).Modules.Add(Module);
            _importer = new Importer(Module);
        }

        public ModuleDefUser Module { get; }

        public TypeDef AddType(string name, int genericParameters = 0)
        {
            var type = new TypeDefUser("Probe", name, Module.CorLibTypes.Object.TypeDefOrRef) { Attributes = TypeAttributes.Public | TypeAttributes.Class };
            for (var i = 0; i < genericParameters; i++)
            {
                type.GenericParameters.Add(new GenericParamUser((ushort)i, GenericParamAttributes.NonVariant, $"T{i}"));
            }

            var constructor = new MethodDefUser(".ctor", MethodSig.CreateInstance(Module.CorLibTypes.Void), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
            constructor.Body = new CilBody();
            constructor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            constructor.Body.Instructions.Add(OpCodes.Call.ToInstruction(_importer.Import(typeof(object).GetConstructor(Type.EmptyTypes))));
            constructor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            type.Methods.Add(constructor);
            Module.Types.Add(type);
            return type;
        }

        /// <summary>
        /// <c>T Method(T value)</c> (generic over <c>!!0</c> when <paramref name="valueType"/> is null) calling
        /// <c>BeginMethod&lt;I, TTarget, T&gt;(instance, ref value)</c> and <c>EndMethod&lt;I, TTarget, T&gt;(instance, value, null, in state)</c>.
        /// </summary>
        public MethodDef AddEchoMethod(TypeDef type, string name, Type integration, TypeSig? valueType)
        {
            var isGeneric = valueType is null;
            valueType ??= new GenericMVar(0);
            var signature = isGeneric ? MethodSig.CreateInstanceGeneric(1, valueType, valueType) : MethodSig.CreateInstance(valueType, valueType);
            var method = new MethodDefUser(name, signature, MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.HideBySig);
            if (isGeneric)
            {
                method.GenericParameters.Add(new GenericParamUser(0, GenericParamAttributes.NonVariant, "TM"));
            }

            type.Methods.Add(method);
            var targetType = TargetSig(type);
            var integrationType = _importer.ImportAsTypeSig(integration);
            var body = method.Body = new CilBody { InitLocals = true };
            var state = new Local(_importer.ImportAsTypeSig(typeof(CallTargetState)));
            var returned = new Local(CallTargetReturnOf(valueType));
            body.Variables.Add(state);
            body.Variables.Add(returned);

            var begin = Invoker("BeginMethod", m => m.GetGenericArguments().Length == 3 && m.GetParameters()[1].ParameterType.IsByRef, integrationType, targetType, valueType);
            var end = Invoker("EndMethod", m => m.GetGenericArguments().Length == 3 && m.GetParameters()[3].ParameterType.IsByRef, integrationType, targetType, valueType);
            body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            body.Instructions.Add(OpCodes.Ldarga.ToInstruction(method.Parameters[1]));
            body.Instructions.Add(OpCodes.Call.ToInstruction(begin));
            body.Instructions.Add(OpCodes.Stloc.ToInstruction(state));
            body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
            body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
            body.Instructions.Add(OpCodes.Ldloca.ToInstruction(state));
            body.Instructions.Add(OpCodes.Call.ToInstruction(end));
            body.Instructions.Add(OpCodes.Stloc.ToInstruction(returned));
            body.Instructions.Add(OpCodes.Ldloca.ToInstruction(returned));
            body.Instructions.Add(OpCodes.Call.ToInstruction(GetReturnValue(valueType)));
            body.Instructions.Add(OpCodes.Ret.ToInstruction());
            return method;
        }

        /// <summary>
        /// <c>Task&lt;int&gt; Method(int value)</c> returning <c>Task.FromResult(value)</c> through <c>EndMethod&lt;I, TTarget, Task&lt;int&gt;&gt;</c>.
        /// </summary>
        public MethodDef AddTaskMethod(TypeDef type, string name, Type integration)
        {
            var taskType = _importer.ImportAsTypeSig(typeof(Task<int>));
            var method = new MethodDefUser(name, MethodSig.CreateInstance(taskType, Module.CorLibTypes.Int32), MethodImplAttributes.IL | MethodImplAttributes.Managed, MethodAttributes.Public | MethodAttributes.HideBySig);
            type.Methods.Add(method);
            var targetType = TargetSig(type);
            var integrationType = _importer.ImportAsTypeSig(integration);
            var body = method.Body = new CilBody { InitLocals = true };
            var state = new Local(_importer.ImportAsTypeSig(typeof(CallTargetState)));
            var returned = new Local(CallTargetReturnOf(taskType));
            body.Variables.Add(state);
            body.Variables.Add(returned);

            var begin = Invoker("BeginMethod", m => m.GetGenericArguments().Length == 2 && m.GetParameters().Length == 1, integrationType, targetType);
            var end = Invoker("EndMethod", m => m.GetGenericArguments().Length == 3 && m.GetParameters()[3].ParameterType.IsByRef, integrationType, targetType, taskType);
            body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            body.Instructions.Add(OpCodes.Call.ToInstruction(begin));
            body.Instructions.Add(OpCodes.Stloc.ToInstruction(state));
            body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
            body.Instructions.Add(OpCodes.Call.ToInstruction(_importer.Import(typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(typeof(int)))));
            body.Instructions.Add(OpCodes.Ldnull.ToInstruction());
            body.Instructions.Add(OpCodes.Ldloca.ToInstruction(state));
            body.Instructions.Add(OpCodes.Call.ToInstruction(end));
            body.Instructions.Add(OpCodes.Stloc.ToInstruction(returned));
            body.Instructions.Add(OpCodes.Ldloca.ToInstruction(returned));
            body.Instructions.Add(OpCodes.Call.ToInstruction(GetReturnValue(taskType)));
            body.Instructions.Add(OpCodes.Ret.ToInstruction());
            return method;
        }

        public (Assembly Assembly, CallTargetRegistryResult Result) Generate(params MethodDef[] rewritten)
        {
            var datadogTrace = ModuleDefMD.Load(typeof(Tracer).Assembly.Location);
            var modules = new ModuleDef[]
            {
                Module,
                datadogTrace,
                ModuleDefMD.Load(typeof(CallTargetRegistryGeneratorTests).Assembly.Location),
                ModuleDefMD.Load(typeof(object).Assembly.Location),
                ModuleDefMD.Load(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll")),
            };
            var resolver = new LoadedModulesTypeResolver(modules);
            var result = CallTargetRegistryGenerator.Generate(Module, rewritten, datadogTrace, resolver.Resolve);
            using var stream = new MemoryStream();
            Module.Write(stream);
            return (Assembly.Load(stream.ToArray()), result);
        }

        private static TypeSig TargetSig(TypeDef type)
            => type.GenericParameters.Count == 0
                   ? new ClassSig(type)
                   : new GenericInstSig(new ClassSig(type), type.GenericParameters.Select(p => (TypeSig)new GenericVar(p.Number, type)).ToList());

        private GenericInstSig CallTargetReturnOf(TypeSig type)
            => new(new ValueTypeSig(_importer.Import(typeof(CallTargetReturn<>))), type);

        private MemberRef GetReturnValue(TypeSig type)
            => new MemberRefUser(Module, "GetReturnValue", MethodSig.CreateInstance(new GenericVar(0)), new TypeSpecUser(CallTargetReturnOf(type)));

        private MethodSpec Invoker(string name, Func<MethodInfo, bool> overload, params TypeSig[] instantiation)
        {
            var definition = typeof(CallTargetInvoker).GetMethods().Single(m => m.Name == name && overload(m));
            return new MethodSpecUser((IMethodDefOrRef)_importer.Import(definition), new GenericInstMethodSig(instantiation));
        }
    }
}
#endif
