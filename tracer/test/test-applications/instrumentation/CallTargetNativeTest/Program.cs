using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;

namespace CallTargetNativeTest
{
    class Program
    {
        private static MemoryStream mStream = new MemoryStream();
        private static StreamWriter sWriter = new StreamWriter(mStream);

        static void Main(string[] args)
        {
            InjectCallTargetDefinitions();
            RunTests(args);
        }

        static void InjectCallTargetDefinitions()
        {
            var definitionsList = new List<NativeCallTargetDefinition>();
            definitionsList.Add(new("CallTargetNativeTest", "CallTargetNativeTest.With0ArgumentsThrowOnAsyncEnd", "Wait2Seconds", new[] { "_" }, 0, 0, 0, 1, 1, 1, "CallTargetNativeTest, Version=1.0.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb", "CallTargetNativeTest.NoOp.Noop0ArgumentsIntegration"));

            for (var i = 0; i < 10; i++)
            {
                var signaturesArray = Enumerable.Range(0, i + 1).Select(i => "_").ToArray();
                var withTypes = new string[]
                {
                    $"CallTargetNativeTest.With{i}Arguments",
                    $"CallTargetNativeTest.With{i}ArgumentsGeneric`1",
                    $"CallTargetNativeTest.With{i}ArgumentsStruct",
                    $"CallTargetNativeTest.With{i}ArgumentsStatic",
                };

                var wrapperTypeVoid = $"CallTargetNativeTest.NoOp.Noop{i}ArgumentsVoidIntegration";
                var wrapperType = $"CallTargetNativeTest.NoOp.Noop{i}ArgumentsIntegration";

                foreach (var tType in withTypes)
                {
                    definitionsList.Add(new("CallTargetNativeTest", tType, "VoidMethod", signaturesArray, 0, 0, 0, 1, 1, 1, "CallTargetNativeTest, Version=1.0.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb", wrapperTypeVoid));
                    definitionsList.Add(new("CallTargetNativeTest", tType, "ReturnValueMethod", signaturesArray, 0, 0, 0, 1, 1, 1, "CallTargetNativeTest, Version=1.0.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb", wrapperType));
                    definitionsList.Add(new("CallTargetNativeTest", tType, "ReturnReferenceMethod", signaturesArray, 0, 0, 0, 1, 1, 1, "CallTargetNativeTest, Version=1.0.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb", wrapperType));
                    definitionsList.Add(new("CallTargetNativeTest", tType, "ReturnGenericMethod", signaturesArray, 0, 0, 0, 1, 1, 1, "CallTargetNativeTest, Version=1.0.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb", wrapperType));
                }
            }

            NativeMethods.InitializeProfiler(Guid.NewGuid().ToString("N"), definitionsList.ToArray());
        }

        static void RunTests(string[] args)
        {
            switch (args[0])
            {
                case "0":
                    {
                        Argument0();
                        break;
                    }
                case "1":
                    {
                        Argument1();
                        break;
                    }
                case "2":
                    {
                        Argument2();
                        break;
                    }
                case "3":
                    {
                        Argument3();
                        break;
                    }
                case "4":
                    {
                        Argument4();
                        break;
                    }
                case "5":
                    {
                        Argument5();
                        break;
                    }
                case "6":
                    {
                        Argument6();
                        break;
                    }
                case "7":
                    {
                        Argument7();
                        break;
                    }
                case "8":
                    {
                        Argument8();
                        break;
                    }
                case "9":
                    {
                        Argument9();
                        break;
                    }
                case "all":
                    {
                        Argument0();
                        Argument1();
                        Argument2();
                        Argument3();
                        Argument4();
                        Argument5();
                        Argument6();
                        Argument7();
                        Argument8();
                        Argument9();
                        break;
                    }
                default:
                    Console.WriteLine("Run with the profiler and use a number from 0-9/all as an argument.");
                    return;
            }

#if NETCOREAPP2_1
            // Sleep to minimize the risk of segfault caused by https://github.com/dotnet/runtime/issues/11885
            Thread.Sleep(5000);
#endif
        }

        private static void Argument0()
        {
            var w0 = new With0Arguments();
            Console.WriteLine($"{typeof(With0Arguments).FullName}.VoidMethod");
            RunMethod(() => w0.VoidMethod());
            Console.WriteLine($"{typeof(With0Arguments).FullName}.ReturnValueMethod");
            RunMethod(() => w0.ReturnValueMethod());
            Console.WriteLine($"{typeof(With0Arguments).FullName}.ReturnReferenceMethod");
            RunMethod(() => w0.ReturnReferenceMethod());
            Console.WriteLine($"{typeof(With0Arguments).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w0.ReturnGenericMethod<string>());
            Console.WriteLine($"{typeof(With0Arguments).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w0.ReturnGenericMethod<int>());
            Console.WriteLine();
            //
            var w0g1 = new With0ArgumentsGeneric<string>();
            Console.WriteLine($"{typeof(With0ArgumentsGeneric<string>).FullName}.VoidMethod");
            RunMethod(() => w0g1.VoidMethod());
            Console.WriteLine($"{typeof(With0ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
            RunMethod(() => w0g1.ReturnValueMethod());
            Console.WriteLine($"{typeof(With0ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w0g1.ReturnReferenceMethod());
            Console.WriteLine($"{typeof(With0ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
            RunMethod(() => w0g1.ReturnGenericMethod());
            Console.WriteLine();
            //
            var w0g2 = new With0ArgumentsGeneric<int>();
            Console.WriteLine($"{typeof(With0ArgumentsGeneric<int>).FullName}.VoidMethod");
            RunMethod(() => w0g2.VoidMethod());
            Console.WriteLine($"{typeof(With0ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
            RunMethod(() => w0g2.ReturnValueMethod());
            Console.WriteLine($"{typeof(With0ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w0g2.ReturnReferenceMethod());
            Console.WriteLine($"{typeof(With0ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
            RunMethod(() => w0g2.ReturnGenericMethod());
            Console.WriteLine();
            //
            var w0in = new With0ArgumentsInherits();
            Console.WriteLine($"{typeof(With0ArgumentsInherits).FullName}.VoidMethod");
            RunMethod(() => w0in.VoidMethod());
            Console.WriteLine($"{typeof(With0ArgumentsInherits).FullName}.ReturnValueMethod");
            RunMethod(() => w0in.ReturnValueMethod());
            Console.WriteLine($"{typeof(With0ArgumentsInherits).FullName}.ReturnReferenceMethod");
            RunMethod(() => w0in.ReturnReferenceMethod());
            Console.WriteLine($"{typeof(With0ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w0in.ReturnGenericMethod<string>());
            Console.WriteLine($"{typeof(With0ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w0in.ReturnGenericMethod<int>());
            Console.WriteLine();
            //
            var w0inGen = new With0ArgumentsInheritsGeneric();
            Console.WriteLine($"{typeof(With0ArgumentsInheritsGeneric).FullName}.VoidMethod");
            RunMethod(() => w0inGen.VoidMethod());
            Console.WriteLine($"{typeof(With0ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
            RunMethod(() => w0inGen.ReturnValueMethod());
            Console.WriteLine($"{typeof(With0ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
            RunMethod(() => w0inGen.ReturnReferenceMethod());
            Console.WriteLine($"{typeof(With0ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
            RunMethod(() => w0inGen.ReturnGenericMethod());
            Console.WriteLine();
            //
            var w0Struct = new With0ArgumentsStruct();
            Console.WriteLine($"{typeof(With0ArgumentsStruct).FullName}.VoidMethod");
            RunMethod(() => w0Struct.VoidMethod());
            Console.WriteLine($"{typeof(With0ArgumentsStruct).FullName}.ReturnValueMethod");
            RunMethod(() => w0Struct.ReturnValueMethod());
            Console.WriteLine($"{typeof(With0ArgumentsStruct).FullName}.ReturnReferenceMethod");
            RunMethod(() => w0Struct.ReturnReferenceMethod());
            Console.WriteLine($"{typeof(With0ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w0Struct.ReturnGenericMethod<string>());
            Console.WriteLine($"{typeof(With0ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w0Struct.ReturnGenericMethod<int>());
            Console.WriteLine();
            //
            Console.WriteLine($"{typeof(With0ArgumentsStatic).FullName}.VoidMethod");
            RunMethod(() => With0ArgumentsStatic.VoidMethod());
            Console.WriteLine($"{typeof(With0ArgumentsStatic).FullName}.ReturnValueMethod");
            RunMethod(() => With0ArgumentsStatic.ReturnValueMethod());
            Console.WriteLine($"{typeof(With0ArgumentsStatic).FullName}.ReturnReferenceMethod");
            RunMethod(() => With0ArgumentsStatic.ReturnReferenceMethod());
            Console.WriteLine($"{typeof(With0ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => With0ArgumentsStatic.ReturnGenericMethod<string>());
            Console.WriteLine($"{typeof(With0ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => With0ArgumentsStatic.ReturnGenericMethod<int>());
            Console.WriteLine();
            //
            var w0TBegin = new With0ArgumentsThrowOnBegin();
            Console.WriteLine($"{typeof(With0ArgumentsThrowOnBegin).FullName}.VoidMethod");
            RunMethod(() => w0TBegin.VoidMethod());
            Console.WriteLine($"{typeof(With0ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
            RunMethod(() => w0TBegin.ReturnValueMethod());
            Console.WriteLine($"{typeof(With0ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
            RunMethod(() => w0TBegin.ReturnReferenceMethod());
            Console.WriteLine($"{typeof(With0ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w0TBegin.ReturnGenericMethod<string>());
            Console.WriteLine($"{typeof(With0ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w0TBegin.ReturnGenericMethod<int>());
            Console.WriteLine();
            //
            var w0TEnd = new With0ArgumentsThrowOnEnd();
            Console.WriteLine($"{typeof(With0ArgumentsThrowOnEnd).FullName}.VoidMethod");
            RunMethod(() => w0TEnd.VoidMethod());
            Console.WriteLine($"{typeof(With0ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
            RunMethod(() => w0TEnd.ReturnValueMethod());
            Console.WriteLine($"{typeof(With0ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
            RunMethod(() => w0TEnd.ReturnReferenceMethod());
            Console.WriteLine($"{typeof(With0ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w0TEnd.ReturnGenericMethod<string>());
            Console.WriteLine($"{typeof(With0ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w0TEnd.ReturnGenericMethod<int>());
            Console.WriteLine();
            //
            var w0TAsyncEnd = new With0ArgumentsThrowOnAsyncEnd();
            Console.WriteLine($"{typeof(With0ArgumentsThrowOnAsyncEnd).FullName}.Wait2Seconds");
            RunMethod(() => w0TAsyncEnd.Wait2Seconds().Wait());
            Console.WriteLine();
        }

        private static void Argument1()
        {
            var w1 = new With1Arguments();
            Console.WriteLine($"{typeof(With1Arguments).FullName}.VoidMethod");
            RunMethod(() => w1.VoidMethod("Hello world"));
            Console.WriteLine($"{typeof(With1Arguments).FullName}.ReturnValueMethod");
            RunMethod(() => w1.ReturnValueMethod("Hello world"));
            Console.WriteLine($"{typeof(With1Arguments).FullName}.ReturnReferenceMethod");
            RunMethod(() => w1.ReturnReferenceMethod("Hello world"));
            Console.WriteLine($"{typeof(With1Arguments).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w1.ReturnGenericMethod<string, string>("Hello world"));
            Console.WriteLine($"{typeof(With1Arguments).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w1.ReturnGenericMethod<int, int>(42));
            Console.WriteLine();
            //
            var w1g1 = new With1ArgumentsGeneric<string>();
            Console.WriteLine($"{typeof(With1ArgumentsGeneric<string>).FullName}.VoidMethod");
            RunMethod(() => w1g1.VoidMethod("Hello World"));
            Console.WriteLine($"{typeof(With1ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
            RunMethod(() => w1g1.ReturnValueMethod("Hello World"));
            Console.WriteLine($"{typeof(With1ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w1g1.ReturnReferenceMethod("Hello World"));
            Console.WriteLine($"{typeof(With1ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
            RunMethod(() => w1g1.ReturnGenericMethod<string>("Hello World"));
            Console.WriteLine();
            //
            var w1g2 = new With1ArgumentsGeneric<int>();
            Console.WriteLine($"{typeof(With1ArgumentsGeneric<int>).FullName}.VoidMethod");
            RunMethod(() => w1g2.VoidMethod("Hello world"));
            Console.WriteLine($"{typeof(With1ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
            RunMethod(() => w1g2.ReturnValueMethod("Hello world"));
            Console.WriteLine($"{typeof(With1ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w1g2.ReturnReferenceMethod("Hello world"));
            Console.WriteLine($"{typeof(With1ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
            RunMethod(() => w1g2.ReturnGenericMethod<int>(42));
            Console.WriteLine();
            //
            var w1in = new With1ArgumentsInherits();
            Console.WriteLine($"{typeof(With1ArgumentsInherits).FullName}.VoidMethod");
            RunMethod(() => w1in.VoidMethod("Hello World"));
            Console.WriteLine($"{typeof(With1ArgumentsInherits).FullName}.ReturnValueMethod");
            RunMethod(() => w1in.ReturnValueMethod("Hello World"));
            Console.WriteLine($"{typeof(With1ArgumentsInherits).FullName}.ReturnReferenceMethod");
            RunMethod(() => w1in.ReturnReferenceMethod("Hello Wolrd"));
            Console.WriteLine($"{typeof(With1ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w1in.ReturnGenericMethod<string, int>(42));
            Console.WriteLine($"{typeof(With1ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w1in.ReturnGenericMethod<int, string>("Hello World"));
            Console.WriteLine();
            //
            var w1inGen = new With1ArgumentsInheritsGeneric();
            Console.WriteLine($"{typeof(With1ArgumentsInheritsGeneric).FullName}.VoidMethod");
            RunMethod(() => w1inGen.VoidMethod("Hello World"));
            Console.WriteLine($"{typeof(With1ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
            RunMethod(() => w1inGen.ReturnValueMethod("Hello World"));
            Console.WriteLine($"{typeof(With1ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
            RunMethod(() => w1inGen.ReturnReferenceMethod("Hello World"));
            Console.WriteLine($"{typeof(With1ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
            RunMethod(() => w1inGen.ReturnGenericMethod<int>(42));
            Console.WriteLine();
            //
            var w1Struct = new With1ArgumentsStruct();
            Console.WriteLine($"{typeof(With1ArgumentsStruct).FullName}.VoidMethod");
            RunMethod(() => w1Struct.VoidMethod("Hello World"));
            Console.WriteLine($"{typeof(With1ArgumentsStruct).FullName}.ReturnValueMethod");
            RunMethod(() => w1Struct.ReturnValueMethod("Hello World"));
            Console.WriteLine($"{typeof(With1ArgumentsStruct).FullName}.ReturnReferenceMethod");
            RunMethod(() => w1Struct.ReturnReferenceMethod("Hello World"));
            Console.WriteLine($"{typeof(With1ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w1Struct.ReturnGenericMethod<string, int>(42));
            Console.WriteLine($"{typeof(With1ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w1Struct.ReturnGenericMethod<int, string>("Hello World"));
            Console.WriteLine();
            //
            Console.WriteLine($"{typeof(With1ArgumentsStatic).FullName}.VoidMethod");
            RunMethod(() => With1ArgumentsStatic.VoidMethod("Hello World"));
            Console.WriteLine($"{typeof(With1ArgumentsStatic).FullName}.ReturnValueMethod");
            RunMethod(() => With1ArgumentsStatic.ReturnValueMethod("Hello World"));
            Console.WriteLine($"{typeof(With1ArgumentsStatic).FullName}.ReturnReferenceMethod");
            RunMethod(() => With1ArgumentsStatic.ReturnReferenceMethod("Hello World"));
            Console.WriteLine($"{typeof(With1ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => With1ArgumentsStatic.ReturnGenericMethod<string, int>(42));
            Console.WriteLine($"{typeof(With1ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => With1ArgumentsStatic.ReturnGenericMethod<int, string>("Hello World"));
            Console.WriteLine();
            //
            var w1TBegin = new With1ArgumentsThrowOnBegin();
            Console.WriteLine($"{typeof(With1ArgumentsThrowOnBegin).FullName}.VoidMethod");
            RunMethod(() => w1TBegin.VoidMethod("Hello world"));
            Console.WriteLine($"{typeof(With1ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
            RunMethod(() => w1TBegin.ReturnValueMethod("Hello world"));
            Console.WriteLine($"{typeof(With1ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
            RunMethod(() => w1TBegin.ReturnReferenceMethod("Hello world"));
            Console.WriteLine($"{typeof(With1ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w1TBegin.ReturnGenericMethod<string, string>("Hello world"));
            Console.WriteLine($"{typeof(With1ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w1TBegin.ReturnGenericMethod<int, int>(42));
            Console.WriteLine();
            //
            var w1TEnd = new With1ArgumentsThrowOnEnd();
            Console.WriteLine($"{typeof(With1ArgumentsThrowOnEnd).FullName}.VoidMethod");
            RunMethod(() => w1TEnd.VoidMethod("Hello world"));
            Console.WriteLine($"{typeof(With1ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
            RunMethod(() => w1TEnd.ReturnValueMethod("Hello world"));
            Console.WriteLine($"{typeof(With1ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
            RunMethod(() => w1TEnd.ReturnReferenceMethod("Hello world"));
            Console.WriteLine($"{typeof(With1ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w1TEnd.ReturnGenericMethod<string, string>("Hello world"));
            Console.WriteLine($"{typeof(With1ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w1TEnd.ReturnGenericMethod<int, int>(42));
            Console.WriteLine();
        }

        private static void Argument2()
        {
            var w2 = new With2Arguments();
            Console.WriteLine($"{typeof(With2Arguments).FullName}.VoidMethod");
            RunMethod(() => w2.VoidMethod("Hello world", 42));
            Console.WriteLine($"{typeof(With2Arguments).FullName}.ReturnValueMethod");
            RunMethod(() => w2.ReturnValueMethod("Hello world", 42));
            Console.WriteLine($"{typeof(With2Arguments).FullName}.ReturnReferenceMethod");
            RunMethod(() => w2.ReturnReferenceMethod("Hello world", 42));
            Console.WriteLine($"{typeof(With2Arguments).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w2.ReturnGenericMethod<string, string>("Hello world", 42));
            Console.WriteLine($"{typeof(With2Arguments).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w2.ReturnGenericMethod<int, int>(42, 99));
            Console.WriteLine();
            //
            var w2g1 = new With2ArgumentsGeneric<string>();
            Console.WriteLine($"{typeof(With2ArgumentsGeneric<string>).FullName}.VoidMethod");
            RunMethod(() => w2g1.VoidMethod("Hello World", 42));
            Console.WriteLine($"{typeof(With2ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
            RunMethod(() => w2g1.ReturnValueMethod("Hello World", 42));
            Console.WriteLine($"{typeof(With2ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w2g1.ReturnReferenceMethod("Hello World", 42));
            Console.WriteLine($"{typeof(With2ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
            RunMethod(() => w2g1.ReturnGenericMethod<string>("Hello World", 42));
            Console.WriteLine();
            //
            var w2g2 = new With2ArgumentsGeneric<int>();
            Console.WriteLine($"{typeof(With2ArgumentsGeneric<int>).FullName}.VoidMethod");
            RunMethod(() => w2g2.VoidMethod("Hello world", 42));
            Console.WriteLine($"{typeof(With2ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
            RunMethod(() => w2g2.ReturnValueMethod("Hello world", 42));
            Console.WriteLine($"{typeof(With2ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w2g2.ReturnReferenceMethod("Hello world", 42));
            Console.WriteLine($"{typeof(With2ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
            RunMethod(() => w2g2.ReturnGenericMethod<int>(42, 99));
            Console.WriteLine();
            //
            var w2in = new With2ArgumentsInherits();
            Console.WriteLine($"{typeof(With2ArgumentsInherits).FullName}.VoidMethod");
            RunMethod(() => w2in.VoidMethod("Hello World", 42));
            Console.WriteLine($"{typeof(With2ArgumentsInherits).FullName}.ReturnValueMethod");
            RunMethod(() => w2in.ReturnValueMethod("Hello World", 42));
            Console.WriteLine($"{typeof(With2ArgumentsInherits).FullName}.ReturnReferenceMethod");
            RunMethod(() => w2in.ReturnReferenceMethod("Hello Wolrd", 42));
            Console.WriteLine($"{typeof(With2ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w2in.ReturnGenericMethod<string, int>(42, 99));
            Console.WriteLine($"{typeof(With2ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w2in.ReturnGenericMethod<int, string>("Hello World", 42));
            Console.WriteLine();
            //
            var w2inGen = new With2ArgumentsInheritsGeneric();
            Console.WriteLine($"{typeof(With2ArgumentsInheritsGeneric).FullName}.VoidMethod");
            RunMethod(() => w2inGen.VoidMethod("Hello World", 42));
            Console.WriteLine($"{typeof(With2ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
            RunMethod(() => w2inGen.ReturnValueMethod("Hello World", 42));
            Console.WriteLine($"{typeof(With2ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
            RunMethod(() => w2inGen.ReturnReferenceMethod("Hello World", 42));
            Console.WriteLine($"{typeof(With2ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
            RunMethod(() => w2inGen.ReturnGenericMethod<int>(42, 99));
            Console.WriteLine();
            //
            var w2Struct = new With2ArgumentsStruct();
            Console.WriteLine($"{typeof(With2ArgumentsStruct).FullName}.VoidMethod");
            RunMethod(() => w2Struct.VoidMethod("Hello World", 42));
            Console.WriteLine($"{typeof(With2ArgumentsStruct).FullName}.ReturnValueMethod");
            RunMethod(() => w2Struct.ReturnValueMethod("Hello World", 42));
            Console.WriteLine($"{typeof(With2ArgumentsStruct).FullName}.ReturnReferenceMethod");
            RunMethod(() => w2Struct.ReturnReferenceMethod("Hello World", 42));
            Console.WriteLine($"{typeof(With2ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w2Struct.ReturnGenericMethod<string, int>(42, 99));
            Console.WriteLine($"{typeof(With2ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w2Struct.ReturnGenericMethod<int, string>("Hello World", 42));
            Console.WriteLine();
            //
            Console.WriteLine($"{typeof(With2ArgumentsStatic).FullName}.VoidMethod");
            RunMethod(() => With2ArgumentsStatic.VoidMethod("Hello World", 42));
            Console.WriteLine($"{typeof(With2ArgumentsStatic).FullName}.ReturnValueMethod");
            RunMethod(() => With2ArgumentsStatic.ReturnValueMethod("Hello World", 42));
            Console.WriteLine($"{typeof(With2ArgumentsStatic).FullName}.ReturnReferenceMethod");
            RunMethod(() => With2ArgumentsStatic.ReturnReferenceMethod("Hello World", 42));
            Console.WriteLine($"{typeof(With2ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => With2ArgumentsStatic.ReturnGenericMethod<string, int>(42, 99));
            Console.WriteLine($"{typeof(With2ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => With2ArgumentsStatic.ReturnGenericMethod<int, string>("Hello World", 42));
            Console.WriteLine();
            //
            var w2TBegin = new With2ArgumentsThrowOnBegin();
            Console.WriteLine($"{typeof(With2ArgumentsThrowOnBegin).FullName}.VoidMethod");
            RunMethod(() => w2TBegin.VoidMethod("Hello world", 42));
            Console.WriteLine($"{typeof(With2ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
            RunMethod(() => w2TBegin.ReturnValueMethod("Hello world", 42));
            Console.WriteLine($"{typeof(With2ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
            RunMethod(() => w2TBegin.ReturnReferenceMethod("Hello world", 42));
            Console.WriteLine($"{typeof(With2ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w2TBegin.ReturnGenericMethod<string, string>("Hello world", 42));
            Console.WriteLine($"{typeof(With2ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w2TBegin.ReturnGenericMethod<int, int>(42, 99));
            Console.WriteLine();
            //
            var w2TEnd = new With2ArgumentsThrowOnEnd();
            Console.WriteLine($"{typeof(With2ArgumentsThrowOnEnd).FullName}.VoidMethod");
            RunMethod(() => w2TEnd.VoidMethod("Hello world", 42));
            Console.WriteLine($"{typeof(With2ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
            RunMethod(() => w2TEnd.ReturnValueMethod("Hello world", 42));
            Console.WriteLine($"{typeof(With2ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
            RunMethod(() => w2TEnd.ReturnReferenceMethod("Hello world", 42));
            Console.WriteLine($"{typeof(With2ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w2TEnd.ReturnGenericMethod<string, string>("Hello world", 42));
            Console.WriteLine($"{typeof(With2ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w2TEnd.ReturnGenericMethod<int, int>(42, 99));
            Console.WriteLine();
        }

        private static void Argument3()
        {
            var w3 = new With3Arguments();
            Console.WriteLine($"{typeof(With3Arguments).FullName}.VoidMethod");
            RunMethod(() => w3.VoidMethod("Hello world", 42, Tuple.Create(1,2)));
            Console.WriteLine($"{typeof(With3Arguments).FullName}.ReturnValueMethod");
            RunMethod(() => w3.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3Arguments).FullName}.ReturnReferenceMethod");
            RunMethod(() => w3.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3Arguments).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w3.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3Arguments).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w3.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
            Console.WriteLine();
            //
            var w3g1 = new With3ArgumentsGeneric<string>();
            Console.WriteLine($"{typeof(With3ArgumentsGeneric<string>).FullName}.VoidMethod");
            RunMethod(() => w3g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
            RunMethod(() => w3g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w3g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
            RunMethod(() => w3g1.ReturnGenericMethod<string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine();
            //
            var w3g2 = new With3ArgumentsGeneric<int>();
            Console.WriteLine($"{typeof(With3ArgumentsGeneric<int>).FullName}.VoidMethod");
            RunMethod(() => w3g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
            RunMethod(() => w3g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w3g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
            RunMethod(() => w3g2.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
            Console.WriteLine();
            //
            var w3in = new With3ArgumentsInherits();
            Console.WriteLine($"{typeof(With3ArgumentsInherits).FullName}.VoidMethod");
            RunMethod(() => w3in.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsInherits).FullName}.ReturnValueMethod");
            RunMethod(() => w3in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsInherits).FullName}.ReturnReferenceMethod");
            RunMethod(() => w3in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w3in.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w3in.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine();
            //
            var w3inGen = new With3ArgumentsInheritsGeneric();
            Console.WriteLine($"{typeof(With3ArgumentsInheritsGeneric).FullName}.VoidMethod");
            RunMethod(() => w3inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
            RunMethod(() => w3inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
            RunMethod(() => w3inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
            RunMethod(() => w3inGen.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
            Console.WriteLine();
            //
            var w3Struct = new With3ArgumentsStruct();
            Console.WriteLine($"{typeof(With3ArgumentsStruct).FullName}.VoidMethod");
            RunMethod(() => w3Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsStruct).FullName}.ReturnValueMethod");
            RunMethod(() => w3Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsStruct).FullName}.ReturnReferenceMethod");
            RunMethod(() => w3Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w3Struct.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w3Struct.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine();
            //
            Console.WriteLine($"{typeof(With3ArgumentsStatic).FullName}.VoidMethod");
            RunMethod(() => With3ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsStatic).FullName}.ReturnValueMethod");
            RunMethod(() => With3ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsStatic).FullName}.ReturnReferenceMethod");
            RunMethod(() => With3ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => With3ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => With3ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2)));
            Console.WriteLine();
            //
            var w3TBegin = new With3ArgumentsThrowOnBegin();
            Console.WriteLine($"{typeof(With3ArgumentsThrowOnBegin).FullName}.VoidMethod");
            RunMethod(() => w3TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
            RunMethod(() => w3TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
            RunMethod(() => w3TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w3TBegin.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w3TBegin.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
            Console.WriteLine();
            //
            var w3TEnd = new With3ArgumentsThrowOnEnd();
            Console.WriteLine($"{typeof(With3ArgumentsThrowOnEnd).FullName}.VoidMethod");
            RunMethod(() => w3TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
            RunMethod(() => w3TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
            RunMethod(() => w3TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w3TEnd.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2)));
            Console.WriteLine($"{typeof(With3ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w3TEnd.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2)));
            Console.WriteLine();
        }

        private static void Argument4()
        {
            var w4 = new With4Arguments();
            Console.WriteLine($"{typeof(With4Arguments).FullName}.VoidMethod");
            RunMethod(() => w4.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4Arguments).FullName}.ReturnValueMethod");
            RunMethod(() => w4.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4Arguments).FullName}.ReturnReferenceMethod");
            RunMethod(() => w4.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4Arguments).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w4.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4Arguments).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w4.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine();
            //
            var w4g1 = new With4ArgumentsGeneric<string>();
            Console.WriteLine($"{typeof(With3ArgumentsGeneric<string>).FullName}.VoidMethod");
            RunMethod(() => w4g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With3ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
            RunMethod(() => w4g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With3ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w4g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With3ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
            RunMethod(() => w4g1.ReturnGenericMethod<string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine();
            //
            var w4g2 = new With4ArgumentsGeneric<int>();
            Console.WriteLine($"{typeof(With4ArgumentsGeneric<int>).FullName}.VoidMethod");
            RunMethod(() => w4g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
            RunMethod(() => w4g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w4g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
            RunMethod(() => w4g2.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine();
            //
            var w4in = new With4ArgumentsInherits();
            Console.WriteLine($"{typeof(With4ArgumentsInherits).FullName}.VoidMethod");
            RunMethod(() => w4in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsInherits).FullName}.ReturnValueMethod");
            RunMethod(() => w4in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsInherits).FullName}.ReturnReferenceMethod");
            RunMethod(() => w4in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w4in.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w4in.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine();
            //
            var w4inGen = new With4ArgumentsInheritsGeneric();
            Console.WriteLine($"{typeof(With4ArgumentsInheritsGeneric).FullName}.VoidMethod");
            RunMethod(() => w4inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
            RunMethod(() => w4inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
            RunMethod(() => w4inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
            RunMethod(() => w4inGen.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine();
            //
            var w4Struct = new With4ArgumentsStruct();
            Console.WriteLine($"{typeof(With4ArgumentsStruct).FullName}.VoidMethod");
            RunMethod(() => w4Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsStruct).FullName}.ReturnValueMethod");
            RunMethod(() => w4Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsStruct).FullName}.ReturnReferenceMethod");
            RunMethod(() => w4Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w4Struct.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w4Struct.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine();
            //
            Console.WriteLine($"{typeof(With4ArgumentsStatic).FullName}.VoidMethod");
            RunMethod(() => With4ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsStatic).FullName}.ReturnValueMethod");
            RunMethod(() => With4ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsStatic).FullName}.ReturnReferenceMethod");
            RunMethod(() => With4ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => With4ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => With4ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine();
            //
            var w4TBegin = new With4ArgumentsThrowOnBegin();
            Console.WriteLine($"{typeof(With4ArgumentsThrowOnBegin).FullName}.VoidMethod");
            RunMethod(() => w4TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
            RunMethod(() => w4TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
            RunMethod(() => w4TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w4TBegin.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w4TBegin.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine();
            //
            var w4TEnd = new With4ArgumentsThrowOnEnd();
            Console.WriteLine($"{typeof(With4ArgumentsThrowOnEnd).FullName}.VoidMethod");
            RunMethod(() => w4TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
            RunMethod(() => w4TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
            RunMethod(() => w4TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w4TEnd.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine($"{typeof(With4ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w4TEnd.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask));
            Console.WriteLine();
        }

        private static void Argument5()
        {
            var w5 = new With5Arguments();
            Console.WriteLine($"{typeof(With5Arguments).FullName}.VoidMethod");
            RunMethod(() => w5.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5Arguments).FullName}.ReturnValueMethod");
            RunMethod(() => w5.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5Arguments).FullName}.ReturnReferenceMethod");
            RunMethod(() => w5.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5Arguments).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w5.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5Arguments).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w5.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine();
            //
            var w5g1 = new With5ArgumentsGeneric<string>();
            Console.WriteLine($"{typeof(With5ArgumentsGeneric<string>).FullName}.VoidMethod");
            RunMethod(() => w5g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
            RunMethod(() => w5g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w5g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
            RunMethod(() => w5g1.ReturnGenericMethod<string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine();
            //
            var w5g2 = new With5ArgumentsGeneric<int>();
            Console.WriteLine($"{typeof(With5ArgumentsGeneric<int>).FullName}.VoidMethod");
            RunMethod(() => w5g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
            RunMethod(() => w5g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w5g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
            RunMethod(() => w5g2.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine();
            //
            var w5in = new With5ArgumentsInherits();
            Console.WriteLine($"{typeof(With5ArgumentsInherits).FullName}.VoidMethod");
            RunMethod(() => w5in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsInherits).FullName}.ReturnValueMethod");
            RunMethod(() => w5in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsInherits).FullName}.ReturnReferenceMethod");
            RunMethod(() => w5in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w5in.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w5in.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine();
            //
            var w5inGen = new With5ArgumentsInheritsGeneric();
            Console.WriteLine($"{typeof(With5ArgumentsInheritsGeneric).FullName}.VoidMethod");
            RunMethod(() => w5inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
            RunMethod(() => w5inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
            RunMethod(() => w5inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
            RunMethod(() => w5inGen.ReturnGenericMethod<int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine();
            //
            var w5Struct = new With5ArgumentsStruct();
            Console.WriteLine($"{typeof(With5ArgumentsStruct).FullName}.VoidMethod");
            RunMethod(() => w5Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsStruct).FullName}.ReturnValueMethod");
            RunMethod(() => w5Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsStruct).FullName}.ReturnReferenceMethod");
            RunMethod(() => w5Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w5Struct.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w5Struct.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine();
            //
            Console.WriteLine($"{typeof(With5ArgumentsStatic).FullName}.VoidMethod");
            RunMethod(() => With5ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsStatic).FullName}.ReturnValueMethod");
            RunMethod(() => With5ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsStatic).FullName}.ReturnReferenceMethod");
            RunMethod(() => With5ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => With5ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => With5ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine();
            //
            var w5TBegin = new With5ArgumentsThrowOnBegin();
            Console.WriteLine($"{typeof(With5ArgumentsThrowOnBegin).FullName}.VoidMethod");
            RunMethod(() => w5TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
            RunMethod(() => w5TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
            RunMethod(() => w5TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w5TBegin.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w5TBegin.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine();
            //
            var w5TEnd = new With5ArgumentsThrowOnEnd();
            Console.WriteLine($"{typeof(With5ArgumentsThrowOnEnd).FullName}.VoidMethod");
            RunMethod(() => w5TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
            RunMethod(() => w5TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
            RunMethod(() => w5TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w5TEnd.ReturnGenericMethod<string, string, Tuple<int, int>>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine($"{typeof(With5ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w5TEnd.ReturnGenericMethod<int, int, Tuple<int, int>>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None));
            Console.WriteLine();
        }

        private static void Argument6()
        {
            var w6 = new With6Arguments();
            Console.WriteLine($"{typeof(With6Arguments).FullName}.VoidMethod");
            RunMethod(() => w6.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6Arguments).FullName}.ReturnValueMethod");
            RunMethod(() => w6.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6Arguments).FullName}.ReturnReferenceMethod");
            RunMethod(() => w6.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6Arguments).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w6.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6Arguments).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w6.ReturnGenericMethod<Task<int>, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine();
            //
            var w6g1 = new With6ArgumentsGeneric<string>();
            Console.WriteLine($"{typeof(With6ArgumentsGeneric<string>).FullName}.VoidMethod");
            RunMethod(() => w6g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
            RunMethod(() => w6g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w6g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
            RunMethod(() => w6g1.ReturnGenericMethod<string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine();
            //
            var w6g2 = new With6ArgumentsGeneric<int>();
            Console.WriteLine($"{typeof(With6ArgumentsGeneric<int>).FullName}.VoidMethod");
            RunMethod(() => w6g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
            RunMethod(() => w6g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w6g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
            RunMethod(() => w6g2.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine();
            //
            var w6in = new With6ArgumentsInherits();
            Console.WriteLine($"{typeof(With6ArgumentsInherits).FullName}.VoidMethod");
            RunMethod(() => w6in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsInherits).FullName}.ReturnValueMethod");
            RunMethod(() => w6in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsInherits).FullName}.ReturnReferenceMethod");
            RunMethod(() => w6in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w6in.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w6in.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine();
            //
            var w6inGen = new With6ArgumentsInheritsGeneric();
            Console.WriteLine($"{typeof(With6ArgumentsInheritsGeneric).FullName}.VoidMethod");
            RunMethod(() => w6inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
            RunMethod(() => w6inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
            RunMethod(() => w6inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
            RunMethod(() => w6inGen.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine();
            //
            var w6Struct = new With6ArgumentsStruct();
            Console.WriteLine($"{typeof(With6ArgumentsStruct).FullName}.VoidMethod");
            RunMethod(() => w6Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsStruct).FullName}.ReturnValueMethod");
            RunMethod(() => w6Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsStruct).FullName}.ReturnReferenceMethod");
            RunMethod(() => w6Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w6Struct.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w6Struct.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine();
            //
            Console.WriteLine($"{typeof(With6ArgumentsStatic).FullName}.VoidMethod");
            RunMethod(() => With6ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsStatic).FullName}.ReturnValueMethod");
            RunMethod(() => With6ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsStatic).FullName}.ReturnReferenceMethod");
            RunMethod(() => With6ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => With6ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => With6ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine();
            //
            var w6TBegin = new With6ArgumentsThrowOnBegin();
            Console.WriteLine($"{typeof(With6ArgumentsThrowOnBegin).FullName}.VoidMethod");
            RunMethod(() => w6TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
            RunMethod(() => w6TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
            RunMethod(() => w6TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w6TBegin.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w6TBegin.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine();
            //
            var w6TEnd = new With6ArgumentsThrowOnEnd();
            Console.WriteLine($"{typeof(With6ArgumentsThrowOnEnd).FullName}.VoidMethod");
            RunMethod(() => w6TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
            RunMethod(() => w6TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
            RunMethod(() => w6TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w6TEnd.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w6TEnd.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine();
        }

        private static void Argument7()
        {
            var w7 = new With7Arguments();
            Console.WriteLine($"{typeof(With7Arguments).FullName}.VoidMethod");
            RunMethod(() => w7.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7Arguments).FullName}.ReturnValueMethod");
            RunMethod(() => w7.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7Arguments).FullName}.ReturnReferenceMethod");
            RunMethod(() => w7.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7Arguments).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w7.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7Arguments).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w7.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine();
            //
            var w7g1 = new With7ArgumentsGeneric<string>();
            Console.WriteLine($"{typeof(With7ArgumentsGeneric<string>).FullName}.VoidMethod");
            RunMethod(() => w7g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
            RunMethod(() => w7g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w7g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
            RunMethod(() => w7g1.ReturnGenericMethod<string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine();
            //
            var w7g2 = new With7ArgumentsGeneric<int>();
            Console.WriteLine($"{typeof(With7ArgumentsGeneric<int>).FullName}.VoidMethod");
            RunMethod(() => w7g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
            RunMethod(() => w7g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w7g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
            RunMethod(() => w7g2.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine();
            //
            var w7in = new With7ArgumentsInherits();
            Console.WriteLine($"{typeof(With7ArgumentsInherits).FullName}.VoidMethod");
            RunMethod(() => w7in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsInherits).FullName}.ReturnValueMethod");
            RunMethod(() => w7in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsInherits).FullName}.ReturnReferenceMethod");
            RunMethod(() => w7in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w7in.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w7in.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine();
            //
            var w7inGen = new With7ArgumentsInheritsGeneric();
            Console.WriteLine($"{typeof(With7ArgumentsInheritsGeneric).FullName}.VoidMethod");
            RunMethod(() => w7inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
            RunMethod(() => w7inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
            RunMethod(() => w7inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
            RunMethod(() => w7inGen.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine();
            //
            var w7Struct = new With7ArgumentsStruct();
            Console.WriteLine($"{typeof(With7ArgumentsStruct).FullName}.VoidMethod");
            RunMethod(() => w7Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsStruct).FullName}.ReturnValueMethod");
            RunMethod(() => w7Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsStruct).FullName}.ReturnReferenceMethod");
            RunMethod(() => w7Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w7Struct.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w7Struct.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine();
            //
            Console.WriteLine($"{typeof(With7ArgumentsStatic).FullName}.VoidMethod");
            RunMethod(() => With7ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsStatic).FullName}.ReturnValueMethod");
            RunMethod(() => With7ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsStatic).FullName}.ReturnReferenceMethod");
            RunMethod(() => With7ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => With7ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => With7ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine();
            //
            var w7TBegin = new With7ArgumentsThrowOnBegin();
            Console.WriteLine($"{typeof(With7ArgumentsThrowOnBegin).FullName}.VoidMethod");
            RunMethod(() => w7TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
            RunMethod(() => w7TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
            RunMethod(() => w7TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w7TBegin.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w7TBegin.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine();
            //
            var w7TEnd = new With7ArgumentsThrowOnEnd();
            Console.WriteLine($"{typeof(With7ArgumentsThrowOnEnd).FullName}.VoidMethod");
            RunMethod(() => w7TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
            RunMethod(() => w7TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
            RunMethod(() => w7TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w7TEnd.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine($"{typeof(With7ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w7TEnd.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value"));
            Console.WriteLine();
        }

        private static void Argument8()
        {
            var w8 = new With8Arguments();
            Console.WriteLine($"{typeof(With8Arguments).FullName}.VoidMethod");
            RunMethod(() => w8.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8Arguments).FullName}.ReturnValueMethod");
            RunMethod(() => w8.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8Arguments).FullName}.ReturnReferenceMethod");
            RunMethod(() => w8.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8Arguments).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w8.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8Arguments).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w8.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine();
            //
            var w8g1 = new With8ArgumentsGeneric<string>();
            Console.WriteLine($"{typeof(With8ArgumentsGeneric<string>).FullName}.VoidMethod", Assembly.GetExecutingAssembly());
            RunMethod(() => w8g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
            RunMethod(() => w8g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w8g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
            RunMethod(() => w8g1.ReturnGenericMethod<string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine();
            //
            var w8g2 = new With8ArgumentsGeneric<int>();
            Console.WriteLine($"{typeof(With8ArgumentsGeneric<int>).FullName}.VoidMethod");
            RunMethod(() => w8g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
            RunMethod(() => w8g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w8g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
            RunMethod(() => w8g2.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine();
            //
            var w8in = new With8ArgumentsInherits();
            Console.WriteLine($"{typeof(With8ArgumentsInherits).FullName}.VoidMethod");
            RunMethod(() => w8in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsInherits).FullName}.ReturnValueMethod");
            RunMethod(() => w8in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsInherits).FullName}.ReturnReferenceMethod");
            RunMethod(() => w8in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w8in.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w8in.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine();
            //
            var w8inGen = new With8ArgumentsInheritsGeneric();
            Console.WriteLine($"{typeof(With8ArgumentsInheritsGeneric).FullName}.VoidMethod");
            RunMethod(() => w8inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
            RunMethod(() => w8inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
            RunMethod(() => w8inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
            RunMethod(() => w8inGen.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine();
            //
            var w8Struct = new With8ArgumentsStruct();
            Console.WriteLine($"{typeof(With8ArgumentsStruct).FullName}.VoidMethod");
            RunMethod(() => w8Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsStruct).FullName}.ReturnValueMethod");
            RunMethod(() => w8Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsStruct).FullName}.ReturnReferenceMethod");
            RunMethod(() => w8Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w8Struct.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w8Struct.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine();
            //
            Console.WriteLine($"{typeof(With8ArgumentsStatic).FullName}.VoidMethod");
            RunMethod(() => With8ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsStatic).FullName}.ReturnValueMethod");
            RunMethod(() => With8ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsStatic).FullName}.ReturnReferenceMethod");
            RunMethod(() => With8ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => With8ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => With8ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine();
            //
            var w8TBegin = new With8ArgumentsThrowOnBegin();
            Console.WriteLine($"{typeof(With8ArgumentsThrowOnBegin).FullName}.VoidMethod");
            RunMethod(() => w8TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
            RunMethod(() => w8TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
            RunMethod(() => w8TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w8TBegin.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w8TBegin.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine();
            //
            var w8TEnd = new With8ArgumentsThrowOnEnd();
            Console.WriteLine($"{typeof(With8ArgumentsThrowOnEnd).FullName}.VoidMethod");
            RunMethod(() => w8TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
            RunMethod(() => w8TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
            RunMethod(() => w8TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w8TEnd.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine($"{typeof(With8ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w8TEnd.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly()));
            Console.WriteLine();
        }

        private static void Argument9()
        {
            var w9 = new With9Arguments();
            Console.WriteLine($"{typeof(With9Arguments).FullName}.VoidMethod");
            RunMethod(() => w9.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9Arguments).FullName}.ReturnValueMethod");
            RunMethod(() => w9.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9Arguments).FullName}.ReturnReferenceMethod");
            RunMethod(() => w9.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9Arguments).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w9.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9Arguments).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w9.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine();
            //
            var w9g1 = new With9ArgumentsGeneric<string>();
            Console.WriteLine($"{typeof(With9ArgumentsGeneric<string>).FullName}.VoidMethod", Assembly.GetExecutingAssembly(), null);
            RunMethod(() => w9g1.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsGeneric<string>).FullName}.ReturnValueMethod");
            RunMethod(() => w9g1.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsGeneric<string>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w9g1.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsGeneric<string>).FullName}.ReturnGenericMethod");
            RunMethod(() => w9g1.ReturnGenericMethod<string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine();
            //
            var w9g2 = new With9ArgumentsGeneric<int>();
            Console.WriteLine($"{typeof(With9ArgumentsGeneric<int>).FullName}.VoidMethod");
            RunMethod(() => w9g2.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsGeneric<int>).FullName}.ReturnValueMethod");
            RunMethod(() => w9g2.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsGeneric<int>).FullName}.ReturnReferenceMethod");
            RunMethod(() => w9g2.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsGeneric<int>).FullName}.ReturnGenericMethod");
            RunMethod(() => w9g2.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine();
            //
            var w9in = new With9ArgumentsInherits();
            Console.WriteLine($"{typeof(With9ArgumentsInherits).FullName}.VoidMethod");
            RunMethod(() => w9in.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsInherits).FullName}.ReturnValueMethod");
            RunMethod(() => w9in.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsInherits).FullName}.ReturnReferenceMethod");
            RunMethod(() => w9in.ReturnReferenceMethod("Hello Wolrd", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsInherits).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w9in.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsInherits).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w9in.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine();
            //
            var w9inGen = new With9ArgumentsInheritsGeneric();
            Console.WriteLine($"{typeof(With9ArgumentsInheritsGeneric).FullName}.VoidMethod");
            RunMethod(() => w9inGen.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsInheritsGeneric).FullName}.ReturnValueMethod");
            RunMethod(() => w9inGen.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsInheritsGeneric).FullName}.ReturnReferenceMethod");
            RunMethod(() => w9inGen.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsInheritsGeneric).FullName}.ReturnGenericMethod");
            RunMethod(() => w9inGen.ReturnGenericMethod<int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine();
            //
            var w9Struct = new With9ArgumentsStruct();
            Console.WriteLine($"{typeof(With9ArgumentsStruct).FullName}.VoidMethod");
            RunMethod(() => w9Struct.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsStruct).FullName}.ReturnValueMethod");
            RunMethod(() => w9Struct.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsStruct).FullName}.ReturnReferenceMethod");
            RunMethod(() => w9Struct.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsStruct).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w9Struct.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsStruct).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w9Struct.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine();
            //
            Console.WriteLine($"{typeof(With9ArgumentsStatic).FullName}.VoidMethod");
            RunMethod(() => With9ArgumentsStatic.VoidMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsStatic).FullName}.ReturnValueMethod");
            RunMethod(() => With9ArgumentsStatic.ReturnValueMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsStatic).FullName}.ReturnReferenceMethod");
            RunMethod(() => With9ArgumentsStatic.ReturnReferenceMethod("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsStatic).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => With9ArgumentsStatic.ReturnGenericMethod<string, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsStatic).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => With9ArgumentsStatic.ReturnGenericMethod<int, string, Tuple<int, int>, ulong>("Hello World", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine();
            //
            var w9TBegin = new With9ArgumentsThrowOnBegin();
            Console.WriteLine($"{typeof(With9ArgumentsThrowOnBegin).FullName}.VoidMethod");
            RunMethod(() => w9TBegin.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsThrowOnBegin).FullName}.ReturnValueMethod");
            RunMethod(() => w9TBegin.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsThrowOnBegin).FullName}.ReturnReferenceMethod");
            RunMethod(() => w9TBegin.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w9TBegin.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsThrowOnBegin).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w9TBegin.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine();
            //
            var w9TEnd = new With9ArgumentsThrowOnEnd();
            Console.WriteLine($"{typeof(With9ArgumentsThrowOnEnd).FullName}.VoidMethod");
            RunMethod(() => w9TEnd.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsThrowOnEnd).FullName}.ReturnValueMethod");
            RunMethod(() => w9TEnd.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsThrowOnEnd).FullName}.ReturnReferenceMethod");
            RunMethod(() => w9TEnd.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<string>");
            RunMethod(() => w9TEnd.ReturnGenericMethod<string, string, Tuple<int, int>, ulong>("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine($"{typeof(With9ArgumentsThrowOnEnd).FullName}.ReturnGenericMethod<int>");
            RunMethod(() => w9TEnd.ReturnGenericMethod<int, int, Tuple<int, int>, ulong>(42, 99, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
            Console.WriteLine();
        }


        private static void RunMethod(Action action)
        {
            var cOut = Console.Out;
            Console.SetOut(sWriter);
            action();
            sWriter.Flush();
            var str = Encoding.UTF8.GetString(mStream.GetBuffer(), 0, (int)mStream.Length);
            mStream.SetLength(0);
            if (string.IsNullOrEmpty(str))
            {
                throw new Exception("The profiler is not connected or is not compiled as DEBUG with the DD_CTARGET_TESTMODE=True environment variable.");
            }
            if (!str.Contains("ProfilerOK: BeginMethod") || !str.Contains("ProfilerOK: EndMethod"))
            {
                throw new Exception("Profiler didn't return a valid ProfilerOK: BeginMethod string.");
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
