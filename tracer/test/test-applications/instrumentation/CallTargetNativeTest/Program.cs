using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using CallTargetNativeTest.NoOp;
using Datadog.Trace.ClrProfiler;

namespace CallTargetNativeTest
{
    partial class Program
    {
        private static MemoryStream mStream = new();
        private static StreamWriter sWriter = new(mStream);
        private static NativeCallTargetDefinition[] definitions;
        private static string definitionsId;

        static void Main(string[] args)
        {
            InjectCallTargetDefinitions();
            RunTests(args);
        }

        static void InjectCallTargetDefinitions()
        {
            const string TargetAssembly = "CallTargetNativeTest";
            string integrationAssembly = typeof(NoOp.Noop0ArgumentsIntegration).Assembly.FullName;

            var definitionsList = new List<NativeCallTargetDefinition>();
            definitionsList.Add(new(TargetAssembly, typeof(With0ArgumentsThrowOnAsyncEnd).FullName, "Wait2Seconds", new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsParentType.With0ArgumentsThrowOnAsyncEnd).FullName, "Wait2Seconds", new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsStructParentType.With0ArgumentsThrowOnAsyncEnd).FullName, "Wait2Seconds", new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));
            definitionsList.Add(new(TargetAssembly, typeof(ArgumentsGenericParentType<>.With0ArgumentsThrowOnAsyncEnd).FullName, "Wait2Seconds", new[] { "_" }, 0, 0, 0, 1, 1, 1, integrationAssembly, "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));

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
                case "abstract":
                    {
                        AbstractMethod();
                        // *** Derived instrumentation is not yet supported for nested types.
                        // ParentAbstractMethod();
                        // StructParentAbstractMethod();
                        // GenericParentAbstractMethod();
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
                        // *** Derived instrumentation is not yet supported for nested types.
                        // ParentAbstractMethod();
                        // StructParentAbstractMethod();
                        // GenericParentAbstractMethod();
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
                default:
                    Console.WriteLine("Run with the profiler and use a number from 0-9/withref/without/abstract/all as an argument.");
                    return;
            }

#if NETCOREAPP2_1
            // Sleep to minimize the risk of segfault caused by https://github.com/dotnet/runtime/issues/11885
            Thread.Sleep(5000);
#endif
        }

        private static void RunMethod(Action action, bool checkInstrumented = true)
        {
            var cOut = Console.Out;
            Console.SetOut(sWriter);
            action();
            sWriter.Flush();
            var str = Encoding.UTF8.GetString(mStream.GetBuffer(), 0, (int)mStream.Length);
            mStream.SetLength(0);
            if (checkInstrumented)
            {
                if (string.IsNullOrEmpty(str))
                {
                    throw new Exception("The profiler is not connected or is not compiled as DEBUG with the DD_CTARGET_TESTMODE=True environment variable.");
                }
                if (!str.Contains("ProfilerOK: BeginMethod") || !str.Contains("ProfilerOK: EndMethod"))
                {
                    throw new Exception("Profiler didn't return a valid ProfilerOK: BeginMethod string.");
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
