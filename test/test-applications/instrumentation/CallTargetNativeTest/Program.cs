using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CallTargetNativeTest
{
    class Program
    {
        private static MemoryStream mStream = new MemoryStream();
        private static StreamWriter sWriter = new StreamWriter(mStream);

        static void Main(string[] args)
        {
            if (args?.Length == 0)
            {
                ShowTypeInfo(typeof(With0Arguments));
                ShowTypeInfo(typeof(With0ArgumentsGeneric<string>));
                ShowTypeInfo(typeof(With0ArgumentsInherits));
                ShowTypeInfo(typeof(With0ArgumentsInheritsGeneric));
                ShowTypeInfo(typeof(With0ArgumentsStruct));
                ShowTypeInfo(typeof(With0ArgumentsStatic));
                return;
            }

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
                default:
                    Console.WriteLine("Run with the profiler and use a number from 0-7 as an argument.");
                    break;
            }

            Console.WriteLine("Press ENTER to exit.");
            Console.ReadLine();
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
        }

        private static void Argument6()
        {
            var w6 = new With6Arguments();
            Console.WriteLine($"{typeof(With6Arguments).FullName}.VoidMethod");
            RunMethod(() => w6.VoidMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6Arguments).FullName}.ReturnValueMethod");
            RunMethod(() => w6.ReturnValueMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987));
            Console.WriteLine($"{typeof(With6Arguments).FullName}.ReturnReferenceMethod");
            RunMethod(() => w6.ReturnReferenceMethod("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987).Wait());
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
            Console.SetCursorPosition(0, Console.CursorTop);
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

    // *** With0Arguments
    class With0Arguments
    {
        public void VoidMethod() {}
        public int ReturnValueMethod() => 42;
        public string ReturnReferenceMethod() => "Hello World";
        public T ReturnGenericMethod<T>() => default;
    }
    class With0ArgumentsGeneric<T>
    {
        public void VoidMethod() { }
        public int ReturnValueMethod() => 42;
        public string ReturnReferenceMethod() => "Hello World";
        public T ReturnGenericMethod() => default;
    }
    class With0ArgumentsInherits : With0Arguments { }
    class With0ArgumentsInheritsGeneric : With0ArgumentsGeneric<int> { }
    struct With0ArgumentsStruct
    {
        public void VoidMethod() { }
        public int ReturnValueMethod() => 42;
        public string ReturnReferenceMethod() => "Hello World";
        public T ReturnGenericMethod<T>() => default;
    }
    static class With0ArgumentsStatic
    {
        public static void VoidMethod() { }
        public static int ReturnValueMethod() => 42;
        public static string ReturnReferenceMethod() => "Hello World";
        public static T ReturnGenericMethod<T>() => default;
    }

    // *** With1Arguments
    class With1Arguments
    {
        public void VoidMethod(string arg1) { }
        public int ReturnValueMethod(string arg1) => 42;
        public string ReturnReferenceMethod(string arg1) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1) => default;
    }
    class With1ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1) { }
        public int ReturnValueMethod(string arg1) => 42;
        public string ReturnReferenceMethod(string arg1) => "Hello World";
        public T ReturnGenericMethod<TArg1>(TArg1 arg1) => default;
    }
    class With1ArgumentsInherits : With1Arguments { }
    class With1ArgumentsInheritsGeneric : With1ArgumentsGeneric<int> { }
    struct With1ArgumentsStruct
    {
        public void VoidMethod(string arg1) { }
        public int ReturnValueMethod(string arg1) => 42;
        public string ReturnReferenceMethod(string arg1) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1) => default;
    }
    static class With1ArgumentsStatic
    {
        public static void VoidMethod(string arg1) { }
        public static int ReturnValueMethod(string arg1) => 42;
        public static string ReturnReferenceMethod(string arg1) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1>(TArg1 arg1) => default;
    }

    // *** With2Arguments
    class With2Arguments
    {
        public void VoidMethod(string arg1, int arg2) { }
        public int ReturnValueMethod(string arg1, int arg2) => 42;
        public string ReturnReferenceMethod(string arg, int arg21) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1, int arg2) => default;
    }
    class With2ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2) { }
        public int ReturnValueMethod(string arg1, int arg2) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2) => "Hello World";
        public T ReturnGenericMethod<TArg1>(TArg1 arg1, int arg2) => default;
    }
    class With2ArgumentsInherits : With2Arguments { }
    class With2ArgumentsInheritsGeneric : With2ArgumentsGeneric<int> { }
    struct With2ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2) { }
        public int ReturnValueMethod(string arg1, int arg2) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2) => "Hello World";
        public T ReturnGenericMethod<T, TArg1>(TArg1 arg1, int arg2) => default;
    }
    static class With2ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2) { }
        public static int ReturnValueMethod(string arg1, int arg2) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1>(TArg1 arg1, int arg2) => default;
    }

    // *** With3Arguments
    class With3Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    class With3ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    class With3ArgumentsInherits : With3Arguments { }
    class With3ArgumentsInheritsGeneric : With3ArgumentsGeneric<int> { }
    struct With3ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }
    static class With3ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3) => default;
    }

    // *** With4Arguments
    class With4Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4) => default;
    }
    class With4ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4) => default;
    }
    class With4ArgumentsInherits : With4Arguments { }
    class With4ArgumentsInheritsGeneric : With4ArgumentsGeneric<int> { }
    struct With4ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4) => default;
    }
    static class With4ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4) => default;
    }

    // *** With5Arguments
    class With5Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
    }
    class With5ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
    }
    class With5ArgumentsInherits : With5Arguments { }
    class With5ArgumentsInheritsGeneric : With5ArgumentsGeneric<int> { }
    struct With5ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
    }
    static class With5ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5) => default;
    }

    // *** With6Arguments
    class With6Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
        public async Task<string> ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5, ulong arg6)
        {
            await Task.Delay(4000);
            return "Hello World";
        }
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
    }
    class With6ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
    }
    class With6ArgumentsInherits : With6Arguments { }
    class With6ArgumentsInheritsGeneric : With6ArgumentsGeneric<int> { }
    struct With6ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
    }
    static class With6ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6) => default;
    }

    // *** With7Arguments
    class With7Arguments
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => 42;
        public string ReturnReferenceMethod(string arg, int arg21, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7) => default;
    }
    class With7ArgumentsGeneric<T>
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => "Hello World";
        public T ReturnGenericMethod<TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7) => default;
    }
    class With7ArgumentsInherits : With7Arguments { }
    class With7ArgumentsInheritsGeneric : With7ArgumentsGeneric<int> { }
    struct With7ArgumentsStruct
    {
        public void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) { }
        public int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => 42;
        public string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => "Hello World";
        public T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7) => default;
    }
    static class With7ArgumentsStatic
    {
        public static void VoidMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) { }
        public static int ReturnValueMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => 42;
        public static string ReturnReferenceMethod(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7) => "Hello World";
        public static T ReturnGenericMethod<T, TArg1, TArg3, TArg6>(TArg1 arg1, int arg2, TArg3 arg3, Task arg4, CancellationToken arg5, TArg6 arg6, string arg7) => default;
    }
}
