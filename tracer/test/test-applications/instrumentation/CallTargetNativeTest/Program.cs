using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using CallTargetNativeTest.NoOp;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest
{
    partial class Program
    {
        private static MemoryStream mStream = new();
        private static StreamWriter sWriter = new(mStream);
        private static NativeCallTargetDefinition[] definitions;
        private static string definitionsId;

        const string TargetAssembly = "CallTargetNativeTest";
        static string integrationAssembly = typeof(NoOp.Noop0ArgumentsIntegration).Assembly.FullName;

        static void Main(string[] args)
        {
            InjectCallTargetDefinitions();
            RunTests(args);
        }

        static void InjectCallTargetDefinitions()
        {
            var definitionsList = new List<NativeCallTargetDefinition>();
            definitionsList.Add(new(TargetAssembly, typeof(With0ArgumentsThrowOnAsyncEnd).FullName, "Wait2Seconds", new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsParentType.With0ArgumentsThrowOnAsyncEnd).FullName, "Wait2Seconds", new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsStructParentType.With0ArgumentsThrowOnAsyncEnd).FullName, "Wait2Seconds", new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsGenericParentType<>.With0ArgumentsThrowOnAsyncEnd).FullName, "Wait2Seconds", new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));

            // task definitions
            definitionsList.Add(new(TargetAssembly, typeof(With0ArgumentsTasks).FullName, nameof(With0ArgumentsTasks.ReturnTaskSync), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));
            definitionsList.Add(new(TargetAssembly, typeof(With0ArgumentsTasks).FullName, nameof(With0ArgumentsTasks.ReturnTaskAsync), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));
            definitionsList.Add(new(TargetAssembly, typeof(With0ArgumentsTasks).FullName, nameof(With0ArgumentsTasks.ReturnValueTaskSync), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));
            definitionsList.Add(new(TargetAssembly, typeof(With0ArgumentsTasks).FullName, nameof(With0ArgumentsTasks.ReturnValueTaskAsync), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));

            // generic task definitions
            definitionsList.Add(new(TargetAssembly, typeof(With0ArgumentsTasksGeneric<>).FullName, nameof(With0ArgumentsTasksGeneric<int>.ReturnTaskSync), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));
            definitionsList.Add(new(TargetAssembly, typeof(With0ArgumentsTasksGeneric<>).FullName, nameof(With0ArgumentsTasksGeneric<int>.ReturnTaskAsync), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));
            definitionsList.Add(new(TargetAssembly, typeof(With0ArgumentsTasksGeneric<>).FullName, nameof(With0ArgumentsTasksGeneric<int>.ReturnValueTaskSync), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));
            definitionsList.Add(new(TargetAssembly, typeof(With0ArgumentsTasksGeneric<>).FullName, nameof(With0ArgumentsTasksGeneric<int>.ReturnValueTaskAsync), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));

            for (var i = 0; i < 10; i++)
            {
                var signaturesArray = Enumerable.Range(0, i + 1).Select(i => "_").ToArray();
                var withTypes = new[]
                {
                    $"CallTargetNativeTest.With{i}Arguments",
                    $"CallTargetNativeTest.With{i}ArgumentsGeneric`1",
                    $"CallTargetNativeTest.With{i}ArgumentsStruct",
                    $"CallTargetNativeTest.With{i}ArgumentsStatic",

                    $"CallTargetNativeTest.ArgumentsParentType+With{i}Arguments",
                    $"CallTargetNativeTest.ArgumentsParentType+With{i}ArgumentsGeneric`1",
                    $"CallTargetNativeTest.ArgumentsParentType+With{i}ArgumentsStruct",
                    $"CallTargetNativeTest.ArgumentsParentType+With{i}ArgumentsStatic",

                    $"CallTargetNativeTest.ArgumentsStructParentType+With{i}Arguments",
                    $"CallTargetNativeTest.ArgumentsStructParentType+With{i}ArgumentsGeneric`1",
                    $"CallTargetNativeTest.ArgumentsStructParentType+With{i}ArgumentsStruct",
                    $"CallTargetNativeTest.ArgumentsStructParentType+With{i}ArgumentsStatic",

                    $"CallTargetNativeTest.ArgumentsGenericParentType`1+With{i}Arguments",
                    $"CallTargetNativeTest.ArgumentsGenericParentType`1+With{i}ArgumentsGeneric`1",
                    $"CallTargetNativeTest.ArgumentsGenericParentType`1+With{i}ArgumentsStruct",
                    $"CallTargetNativeTest.ArgumentsGenericParentType`1+With{i}ArgumentsStatic",
                };

                var wrapperTypeVoid = $"CallTargetNativeTest.NoOp.Noop{i}ArgumentsVoidIntegration";
                var wrapperType = $"CallTargetNativeTest.NoOp.Noop{i}ArgumentsIntegration";

                foreach (var tType in withTypes)
                {
                    definitionsList.Add(new(TargetAssembly, tType, "VoidMethod", signaturesArray, 0, 0, 0, 1, 1, 1, integrationAssembly, wrapperTypeVoid));
                    definitionsList.Add(new(TargetAssembly, tType, "ReturnValueMethod", signaturesArray, 0, 0, 0, 1, 1, 1, integrationAssembly, wrapperType));
                    definitionsList.Add(new(TargetAssembly, tType, "ReturnReferenceMethod", signaturesArray, 0, 0, 0, 1, 1, 1, integrationAssembly, wrapperType));
                    definitionsList.Add(new(TargetAssembly, tType, "ReturnGenericMethod", signaturesArray, 0, 0, 0, 1, 1, 1, integrationAssembly, wrapperType));
                }
            }

            // Add By Ref integrations
            definitionsList.Add(new(TargetAssembly, typeof(WithRefArguments).FullName, "VoidMethod", new[] { "_", "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(StringAndIntRefModificationVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(WithRefArguments).FullName, "VoidRefMethod", new[] { "_", "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(StringAndIntRefModificationVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(WithRefArguments).FullName, "VoidMethod", new[] { "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(GenericRefModificationVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(WithRefArguments).FullName, "VoidRefMethod", new[] { "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(GenericRefModificationVoidIntegration).FullName));

            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsParentType.WithRefArguments).FullName, "VoidMethod", new[] { "_", "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(StringAndIntRefModificationVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsParentType.WithRefArguments).FullName, "VoidRefMethod", new[] { "_", "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(StringAndIntRefModificationVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsParentType.WithRefArguments).FullName, "VoidMethod", new[] { "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(GenericRefModificationVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsParentType.WithRefArguments).FullName, "VoidRefMethod", new[] { "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(GenericRefModificationVoidIntegration).FullName));

            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsStructParentType.WithRefArguments).FullName, "VoidMethod", new[] { "_", "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(StringAndIntRefModificationVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsStructParentType.WithRefArguments).FullName, "VoidRefMethod", new[] { "_", "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(StringAndIntRefModificationVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsStructParentType.WithRefArguments).FullName, "VoidMethod", new[] { "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(GenericRefModificationVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsStructParentType.WithRefArguments).FullName, "VoidRefMethod", new[] { "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(GenericRefModificationVoidIntegration).FullName));

            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsGenericParentType<>.WithRefArguments).FullName, "VoidMethod", new[] { "_", "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(StringAndIntRefModificationVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsGenericParentType<>.WithRefArguments).FullName, "VoidRefMethod", new[] { "_", "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(StringAndIntRefModificationVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsGenericParentType<>.WithRefArguments).FullName, "VoidMethod", new[] { "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(GenericRefModificationVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsGenericParentType<>.WithRefArguments).FullName, "VoidRefMethod", new[] { "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(GenericRefModificationVoidIntegration).FullName));

            // Add Out integrations
            definitionsList.Add(new(TargetAssembly, typeof(WithOutArguments).FullName, "VoidMethod", new[] { "_", "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(StringAndIntOutVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(WithOutArguments).FullName, "VoidMethod", new[] { "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(GenericOutModificationVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsParentType.WithOutArguments).FullName, "VoidMethod", new[] { "_", "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(StringAndIntOutVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsParentType.WithOutArguments).FullName, "VoidMethod", new[] { "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(GenericOutModificationVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsStructParentType.WithOutArguments).FullName, "VoidMethod", new[] { "_", "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(StringAndIntOutVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsStructParentType.WithOutArguments).FullName, "VoidMethod", new[] { "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(GenericOutModificationVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsGenericParentType<>.WithOutArguments).FullName, "VoidMethod", new[] { "_", "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(StringAndIntOutVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsGenericParentType<>.WithOutArguments).FullName, "VoidMethod", new[] { "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(GenericOutModificationVoidIntegration).FullName));

            // Add extra integrations
            definitionsList.Add(new(TargetAssembly, typeof(Extras).FullName, nameof(CallTargetNativeTest.Extras.NonVoidWithBranchToLastReturn), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(Noop0ArgumentsIntegration).FullName));
            
            // call target bubble up exception
            definitionsList.Add(new(TargetAssembly, typeof(CallTargetBubbleUpExceptionsThrowBubbleUpOnBegin).FullName, nameof(CallTargetNativeTest.CallTargetBubbleUpExceptionsThrowBubbleUpOnBegin.DoSomething), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(CallTargetBubbleUpExceptionsIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnBegin).FullName, nameof(CallTargetNativeTest.CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnBegin.DoSomething), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(CallTargetBubbleUpExceptionsIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(CallTargetBubbleUpExceptionsThrowBubbleUpOnEnd).FullName, nameof(CallTargetNativeTest.CallTargetBubbleUpExceptionsThrowBubbleUpOnEnd.DoSomething), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(CallTargetBubbleUpExceptionsIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnEnd).FullName, nameof(CallTargetNativeTest.CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnEnd.DoSomething), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(CallTargetBubbleUpExceptionsIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(CallTargetBubbleUpExceptionsThrowBubbleUpOnAsyncEnd).FullName, nameof(CallTargetNativeTest.CallTargetBubbleUpExceptionsThrowBubbleUpOnAsyncEnd.DoSomething), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(CallTargetBubbleUpExceptionsIntegrationAsync).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnAsyncEnd).FullName, nameof(CallTargetNativeTest.CallTargetBubbleUpExceptionsThrowNestedBubbleUpOnAsyncEnd.DoSomething), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(CallTargetBubbleUpExceptionsIntegrationAsync).FullName));

            // instrumnentation exceptions
            definitionsList.Add(new(TargetAssembly, typeof(InstrumentationExceptionDuckTypeExceptionThrowOnBegin).FullName, nameof(CallTargetNativeTest.InstrumentationExceptionDuckTypeExceptionThrowOnBegin.DoSomething), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(InstrumentationExceptionsIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(InstrumentationExceptionMissingMethodExceptionThrowOnBegin).FullName, nameof(CallTargetNativeTest.InstrumentationExceptionMissingMethodExceptionThrowOnBegin.DoSomething), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(InstrumentationExceptionsIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(InstrumentationExceptionCallTargetInvokerExceptionThrowOnBegin).FullName, nameof(CallTargetNativeTest.InstrumentationExceptionCallTargetInvokerExceptionThrowOnBegin.DoSomething), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(InstrumentationExceptionsIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(InstrumentationExceptionDuckTypeExceptionThrowOnEnd).FullName, nameof(CallTargetNativeTest.InstrumentationExceptionDuckTypeExceptionThrowOnEnd.DoSomething), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(InstrumentationExceptionsIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(InstrumentationExceptionDuckTypeExceptionThrowOnAsyncEnd).FullName, nameof(CallTargetNativeTest.InstrumentationExceptionDuckTypeExceptionThrowOnAsyncEnd.DoSomething), new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(InstrumentationExceptionsIntegrationAsync).FullName));

            // Add Ref Struct integrations
            definitionsList.Add(new(TargetAssembly, typeof(WithRefStructArguments).FullName, "VoidReadOnlySpanMethod", new[] { "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(RefStructOneParametersVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(WithRefStructArguments).FullName, "VoidSpanMethod", new[] { "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(RefStructOneParametersVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(WithRefStructArguments).FullName, "VoidReadOnlyRefStructMethod", new[] { "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(RefStructOneParametersVoidIntegration).FullName));

            definitionsList.Add(new(TargetAssembly, typeof(WithRefStructArguments).FullName, "Void2ReadOnlySpanMethod", new[] { "_", "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(RefStructTwoParametersVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(WithRefStructArguments).FullName, "Void2SpanMethod", new[] { "_", "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(RefStructTwoParametersVoidIntegration).FullName));
            definitionsList.Add(new(TargetAssembly, typeof(WithRefStructArguments).FullName, "Void2ReadOnlyRefStructMethod", new[] { "_", "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(RefStructTwoParametersVoidIntegration).FullName));

            definitionsList.Add(new(TargetAssembly, typeof(WithRefStructArguments).FullName, "VoidMixedMethod", new[] { "_", "_", "_", "_", "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, typeof(RefStructFourParametersVoidIntegration).FullName));
            
            definitionsId = Guid.NewGuid().ToString("N");
            definitions = definitionsList.ToArray();
            EnableDefinitions();

            NativeMethods.AddDerivedInstrumentations(Guid.NewGuid().ToString("N"), new NativeCallTargetDefinition[]
            {
                new(TargetAssembly, typeof(AbstractClass).FullName, "VoidMethod", new[] { "_", "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop1ArgumentsVoidIntegration).FullName),
                new(TargetAssembly, typeof(AbstractClass).FullName, "OtherMethod", new[] { "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop0ArgumentsVoidIntegration).FullName),
                new(TargetAssembly, typeof(NonAbstractClass).FullName, "VoidMethod", new[] { "_", "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop1ArgumentsVoidIntegration).FullName),

                new(TargetAssembly, typeof(ArgumentsParentType.AbstractClass).FullName, "VoidMethod", new[] { "_", "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop1ArgumentsVoidIntegration).FullName),
                new(TargetAssembly, typeof(ArgumentsParentType.AbstractClass).FullName, "OtherMethod", new[] { "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop0ArgumentsVoidIntegration).FullName),
                new(TargetAssembly, typeof(ArgumentsParentType.NonAbstractClass).FullName, "VoidMethod", new[] { "_", "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop1ArgumentsVoidIntegration).FullName),

                new(TargetAssembly, typeof(ArgumentsStructParentType.AbstractClass).FullName, "VoidMethod", new[] { "_", "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop1ArgumentsVoidIntegration).FullName),
                new(TargetAssembly, typeof(ArgumentsStructParentType.AbstractClass).FullName, "OtherMethod", new[] { "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop0ArgumentsVoidIntegration).FullName),
                new(TargetAssembly, typeof(ArgumentsStructParentType.NonAbstractClass).FullName, "VoidMethod", new[] { "_", "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop1ArgumentsVoidIntegration).FullName),

                new(TargetAssembly, typeof(ArgumentsGenericParentType<>.AbstractClass).FullName, "VoidMethod", new[] { "_", "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop1ArgumentsVoidIntegration).FullName),
                new(TargetAssembly, typeof(ArgumentsGenericParentType<>.AbstractClass).FullName, "OtherMethod", new[] { "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop0ArgumentsVoidIntegration).FullName),
                new(TargetAssembly, typeof(ArgumentsGenericParentType<>.NonAbstractClass).FullName, "VoidMethod", new[] { "_", "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop1ArgumentsVoidIntegration).FullName),
            });

            NativeMethods.AddInterfaceInstrumentations(Guid.NewGuid().ToString("N"), new NativeCallTargetDefinition[]
            {
                new(TargetAssembly, typeof(InterfaceType).FullName, "VoidMethod", new[] { "_", "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop1ArgumentsVoidIntegration).FullName),
                new(TargetAssembly, typeof(IExplicitOverNormal).FullName, "ReturnValueMethod", new[] { "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(ExplicitOverNormalIntegration).FullName),
            });
        }

        static void EnableDefinitions()
        {
            NativeMethods.InitializeProfiler(definitionsId, definitions);
        }
        static void DisableDefinitions()
        {
            NativeMethods.RemoveCallTargetDefinitions(definitionsId, definitions);
        }

        static void RunTests(string[] args)
        {
            switch (args[0])
            {
                case "0":
                    {
                        Argument0();
                        ParentArgument0();
                        StructParentArgument0();
                        GenericParentArgument0();
                        break;
                    }
                case "1":
                    {
                        Argument1();
                        ParentArgument1();
                        StructParentArgument1();
                        GenericParentArgument1();
                        break;
                    }
                case "2":
                    {
                        Argument2();
                        ParentArgument2();
                        StructParentArgument2();
                        GenericParentArgument2();
                        break;
                    }
                case "3":
                    {
                        Argument3();
                        ParentArgument3();
                        StructParentArgument3();
                        GenericParentArgument3();
                        break;
                    }
                case "4":
                    {
                        Argument4();
                        ParentArgument4();
                        StructParentArgument4();
                        GenericParentArgument4();
                        break;
                    }
                case "5":
                    {
                        Argument5();
                        ParentArgument5();
                        StructParentArgument5();
                        GenericParentArgument5();
                        break;
                    }
                case "6":
                    {
                        Argument6();
                        ParentArgument6();
                        StructParentArgument6();
                        GenericParentArgument6();
                        break;
                    }
                case "7":
                    {
                        Argument7();
                        ParentArgument7();
                        StructParentArgument7();
                        GenericParentArgument7();
                        break;
                    }
                case "8":
                    {
                        Argument8();
                        ParentArgument8();
                        StructParentArgument8();
                        GenericParentArgument8();
                        break;
                    }
                case "9":
                    {
                        Argument9();
                        ParentArgument9();
                        StructParentArgument9();
                        GenericParentArgument9();
                        break;
                    }
                case "withref":
                    {
                        WithRefArguments();
                        ParentWithRefArguments();
                        StructParentWithRefArguments();
                        GenericParentWithRefArguments();
                        break;
                    }
                case "without":
                    {
                        WithOutArguments();
                        ParentWithOutArguments();
                        StructParentWithOutArguments();
                        GenericParentWithOutArguments();
                        break;
                    }
                case "withrefstruct":
                    {
                        WithRefStructArguments();
                        break;
                    }
                case "abstract":
                    {
                        AbstractMethod();
                        // *** Derived instrumentation is not yet supported for nested types.
                        // ParentAbstractMethod();
                        // StructParentAbstractMethod();
                        // GenericParentAbstractMethod();
                        break;
                    }
                case "interface":
                    {
                        InterfaceMethod();
                        break;
                    }
                case "remove":
                    {
                        WithOutArguments();
                        DisableDefinitions();
                        WithOutArguments(false);
                        EnableDefinitions();
                        WithOutArguments();
                        break;
                    }
                case "extras":
                    {
                        Extras();
                        break;
                    }
                case "categories":
                    {
                        CategoriesTest();
                        break;
                    }
                case "callsite":
                    {
                        CallSite();
                        break;
                    }
                case "calltargetbubbleupexceptions":
                {
                    CallTargetBubbleUpExceptions();
                    break;
                }
                case "instrumentationexceptions":
                {
                    InstrumentationExceptions();
                    break;
                }
                case "all":
                    {
                        Argument0();
                        ParentArgument0();
                        StructParentArgument0();
                        GenericParentArgument0();
                        // .
                        Argument1();
                        ParentArgument1();
                        StructParentArgument1();
                        GenericParentArgument1();
                        // .
                        Argument2();
                        ParentArgument2();
                        StructParentArgument2();
                        GenericParentArgument2();
                        // .
                        Argument3();
                        ParentArgument3();
                        StructParentArgument3();
                        GenericParentArgument3();
                        // .
                        Argument4();
                        ParentArgument4();
                        StructParentArgument4();
                        GenericParentArgument4();
                        // .
                        Argument5();
                        ParentArgument5();
                        StructParentArgument5();
                        GenericParentArgument5();
                        // .
                        Argument6();
                        ParentArgument6();
                        StructParentArgument6();
                        GenericParentArgument6();
                        // .
                        Argument7();
                        ParentArgument7();
                        StructParentArgument7();
                        GenericParentArgument7();
                        // .
                        Argument8();
                        ParentArgument8();
                        StructParentArgument8();
                        GenericParentArgument8();
                        // .
                        Argument9();
                        ParentArgument9();
                        StructParentArgument9();
                        GenericParentArgument9();
                        // .
                        WithRefArguments();
                        ParentWithRefArguments();
                        StructParentWithRefArguments();
                        GenericParentWithRefArguments();
                        // .
                        WithOutArguments();
                        ParentWithOutArguments();
                        StructParentWithOutArguments();
                        GenericParentWithOutArguments();
                        // .
                        AbstractMethod();
                        // .
                        InterfaceMethod();
                        //.
                        WithOutArguments();
                        DisableDefinitions();
                        WithOutArguments(false);
                        EnableDefinitions();
                        WithOutArguments();
                        // *** Derived instrumentation is not yet supported for nested types.
                        // ParentAbstractMethod();
                        // StructParentAbstractMethod();
                        // GenericParentAbstractMethod();
                        Extras();
                        //.
                        CallTargetBubbleUpExceptions();
                        //.
                        InstrumentationExceptions();
                        //.
                        CallSite();
                        // .
                        WithRefStructArguments();
                        break;
                    }
                default:
                    Console.WriteLine("Run with the profiler and use a number from 0-9/withref/without/withrefstruct/abstract/interface/remove/all as an argument.");
                    return;
            }

#if NETCOREAPP2_1
            // Sleep to minimize the risk of segfault caused by https://github.com/dotnet/runtime/issues/11885
            Thread.Sleep(5000);
#endif
        }

        private static void RunMethod(Action action, bool checkInstrumented = true, bool bubblingUpException = false, bool asyncMethod = false, bool expectEndMethodExecution = true)
        {
            var cOut = Console.Out;
            Console.SetOut(sWriter);

            if (bubblingUpException)
            {
                try
                {
                    action();
                    throw new Exception("No exception bubbled up when it was expected to, check that the native code filter is working properly or that your instrumentation is throwing a CallTargetBubbleUpException (or a nested one)");
                }
                catch (Exception e) when (CallTargetBubbleUpException.IsCallTargetBubbleUpException(e))
                {
                    // this is normal and expected if not, throw after action is executed
                }
            }
            else
            {
                action();
            }

            sWriter.Flush();
            var str = Encoding.UTF8.GetString(mStream.GetBuffer(), 0, (int)mStream.Length);
            mStream.SetLength(0);
            if (checkInstrumented)
            {
                if (string.IsNullOrEmpty(str))
                {
                    throw new Exception("The profiler is not connected or is not compiled as DEBUG with the DD_CTARGET_TESTMODE=True environment variable.");
                }
                if (!str.Contains("ProfilerOK: BeginMethod"))
                {
                    throw new Exception("Profiler didn't return a valid ProfilerOK: BeginMethod string.");
                }

                if (expectEndMethodExecution)
                {
                    var endMethodString = asyncMethod ? "ProfilerOK: EndMethodAsync(" : "ProfilerOK: EndMethod(";
                    if (!str.Contains(endMethodString))
                    {
                        throw new Exception($"Profiler didn't return a valid {endMethodString} string.");
                    }
                }
            }
            else 
            {
                if (!string.IsNullOrEmpty(str))
                {
                    throw new Exception("Profiler instrumented disabled function.");
                }
                str = "OK: Not instrumented";
            }
            if (!string.IsNullOrEmpty(str))
            {
                cOut.Write("     " + string.Join("\n     ", str.Split('\n')));
            }
            Console.SetOut(cOut);
            Console.WriteLine();
        }

        static void ShowTypeInfo(Type type)
        {
            Console.WriteLine($"Assembly: {type.Assembly.GetName().Name}");
            Console.WriteLine($"  Type: {type.FullName}");
            foreach (var methodInfo in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                if (methodInfo.Name == "GetType" || methodInfo.Name == "GetHashCode" || methodInfo.Name == "ToString" || methodInfo.Name == "Equals")
                {
                    continue;
                }
                Console.WriteLine($"   Method: {methodInfo.Name}");
            }
            Console.WriteLine();
        }
    }
}
