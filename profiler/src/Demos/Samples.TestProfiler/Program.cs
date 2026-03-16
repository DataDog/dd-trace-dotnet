// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Samples.TestProfiler
{
    class Program
    {
        // P/Invoke declarations for test profiler validation functions
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct ValidationOptions
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string? ReportPath;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct ValidationResult
        {
            public int TotalIPs;
            public int TotalFunctions;
            public int TotalCodeRanges;
            public int FailureCount;
            public int SkippedIPs;
            public int InvalidIPsTested;
            public int InvalidIPFailures;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
            public string FirstFailureMethod;
        }

        [DllImport("Datadog.TestProfiler", CallingConvention = CallingConvention.Cdecl)]
        private static extern int PrepareForValidation();

        [DllImport("Datadog.TestProfiler", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ValidateManagedCodeCache(
            ref ValidationOptions options,
            ref ValidationResult result);

        private static string ParseCommandLine(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if ("--output".Equals(args[i], StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        return args[i + 1];
                    }
                    else
                    {
                        Console.Error.WriteLine("ERROR: --output requires a path argument");
                        Environment.Exit(1);
                    }
                }
            }

            // --output is mandatory
            Console.Error.WriteLine("ERROR: --output argument is required");
            Console.Error.WriteLine("Usage: Samples.TestProfiler --output <path>");
            Environment.Exit(1);
            return null; // Never reached
        }

        static async Task Main(string[] args)
        {
            // Parse output path from arguments (mandatory)
            string outputPath = ParseCommandLine(args);

            Console.WriteLine("[TestProfiler] Starting test application");
            Console.WriteLine($"[TestProfiler] Output path: {outputPath}");
            Console.WriteLine($"[TestProfiler] Runtime: {RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"[TestProfiler] Architecture: {RuntimeInformation.ProcessArchitecture}");

            // Exercise various method types
            Console.WriteLine("\n=== Executing Regular Methods ===");
            TestRegularMethods();

            Console.WriteLine("\n=== Executing Generic Methods ===");
            TestGenericMethods();

            Console.WriteLine("\n=== Executing Async Methods ===");
            await TestAsyncMethods();

            Console.WriteLine("\n=== Executing Dynamic Methods ===");
            TestDynamicMethods();

            Console.WriteLine("\n=== Executing Lambda/Delegate Methods ===");
            TestLambdaMethods();

            // Generate many more methods to increase test coverage
            Console.WriteLine("\n=== Generating Large Method Set (200+ per category) ===");
            await GenerateLargeMethodSet();

            // Give time for all JIT compilation to complete
            Console.WriteLine("\n=== Waiting for JIT compilation ===");
            await Task.Delay(1000);

            // Prepare for validation (triggers ReJIT for a subset of functions)
            Console.WriteLine("\n=== Preparing for Validation ===");
            int prepareResult = PrepareForValidation();
            if (prepareResult != 0)
            {
                Console.WriteLine($"ERROR: PrepareForValidation failed with code {prepareResult}");
                Environment.Exit(prepareResult);
            }

            // Run validation
            Console.WriteLine("\n=== Running Validation ===");
            var options = new ValidationOptions
            {
                ReportPath = outputPath
            };
            var result = new ValidationResult();

            int validationResult = ValidateManagedCodeCache(ref options, ref result);

            // Display results
            Console.WriteLine("\n=== Validation Results ===");
            Console.WriteLine($"Total Functions:     {result.TotalFunctions}");
            Console.WriteLine($"Total Code Ranges:   {result.TotalCodeRanges}");
            Console.WriteLine($"Total IPs Validated: {result.TotalIPs}");
            Console.WriteLine($"Skipped IPs:         {result.SkippedIPs}");
            Console.WriteLine($"Invalid IPs Tested:  {result.InvalidIPsTested}");
            Console.WriteLine($"Failures:            {result.FailureCount + result.InvalidIPFailures}");

            if (result.FailureCount > 0 || result.InvalidIPFailures > 0)
            {
                Console.WriteLine($"First Failure:       {result.FirstFailureMethod}");
            }

            Console.WriteLine($"\nValidation report written to: {options.ReportPath}");
            Console.WriteLine($"\n=== FINAL RESULT: {(validationResult == 0 ? "PASSED" : "FAILED")} ===");

            Environment.Exit(validationResult);
        }

        // Regular instance and static methods
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void TestRegularMethods()
        {
            var instance = new TestClass();
            instance.InstanceMethod();
            TestClass.StaticMethod();
            instance.InstanceMethodWithParams(42, "test");

            // Nested class
            var nested = new TestClass.NestedClass();
            nested.NestedMethod();
        }

        // Generic methods
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void TestGenericMethods()
        {
            GenericClass<int>.GenericStaticMethod(123);
            GenericClass<string>.GenericStaticMethod("hello");

            var intInstance = new GenericClass<int>();
            intInstance.GenericInstanceMethod(456);

            var stringInstance = new GenericClass<string>();
            stringInstance.GenericInstanceMethod("world");

            GenericMethod<int>(789);
            GenericMethod<string>("generic");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static T GenericMethod<T>(T value)
        {
            Console.WriteLine($"  GenericMethod<{typeof(T).Name}>({value})");
            return value;
        }

        // Async methods
        [MethodImpl(MethodImplOptions.NoInlining)]
        static async Task TestAsyncMethods()
        {
            await AsyncMethod1();
            await AsyncMethod2(100);
            await AsyncMethodWithResult();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static async Task AsyncMethod1()
        {
            Console.WriteLine("  AsyncMethod1: start");
            await Task.Delay(10);
            Console.WriteLine("  AsyncMethod1: end");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static async Task AsyncMethod2(int delay)
        {
            Console.WriteLine($"  AsyncMethod2: delay {delay}ms");
            await Task.Delay(delay);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static async Task<int> AsyncMethodWithResult()
        {
            await Task.Delay(10);
            return 42;
        }

        // Dynamic methods (Reflection.Emit)
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void TestDynamicMethods()
        {
            // Create a dynamic method that returns an integer
            var dynamicMethod1 = new DynamicMethod(
                "DynamicMethod_ReturnsInt",
                typeof(int),
                Type.EmptyTypes);

            var il1 = dynamicMethod1.GetILGenerator();
            il1.Emit(OpCodes.Ldc_I4, 42);
            il1.Emit(OpCodes.Ret);

            var func1 = (Func<int>)dynamicMethod1.CreateDelegate(typeof(Func<int>));
            int result1 = func1();
            Console.WriteLine($"  DynamicMethod_ReturnsInt() = {result1}");

            // Create a dynamic method with parameters
            var dynamicMethod2 = new DynamicMethod(
                "DynamicMethod_AddInts",
                typeof(int),
                new[] { typeof(int), typeof(int) });

            var il2 = dynamicMethod2.GetILGenerator();
            il2.Emit(OpCodes.Ldarg_0);
            il2.Emit(OpCodes.Ldarg_1);
            il2.Emit(OpCodes.Add);
            il2.Emit(OpCodes.Ret);

            var func2 = (Func<int, int, int>)dynamicMethod2.CreateDelegate(typeof(Func<int, int, int>));
            int result2 = func2(10, 20);
            Console.WriteLine($"  DynamicMethod_AddInts(10, 20) = {result2}");

            // Execute multiple times to ensure they're tracked
            for (int i = 0; i < 5; i++)
            {
                func1();
                func2(i, i * 2);
            }
        }

        // Lambda and delegate methods
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void TestLambdaMethods()
        {
            // Simple lambda
            Func<int, int> square = x => x * x;
            Console.WriteLine($"  Lambda square(5) = {square(5)}");

            // Lambda with closure
            int multiplier = 10;
            Func<int, int> multiply = x => x * multiplier;
            Console.WriteLine($"  Lambda multiply(5) = {multiply(5)}");

            // Complex lambda
            Func<int, int, int> add = (a, b) =>
            {
                Console.WriteLine($"  Lambda add({a}, {b})");
                return a + b;
            };
            add(3, 7);

            // Delegate to method
            Action<string> print = Console.WriteLine;
            print("  Delegate test");
        }

        // Generate large method set for comprehensive testing
        [MethodImpl(MethodImplOptions.NoInlining)]
        static async Task GenerateLargeMethodSet()
        {
            Console.WriteLine("  Generating JIT-compiled methods via generics...");
            // Generate 200+ unique JIT-compiled methods using generic instantiations
            // More type combinations to hit 200+ methods
            var generator = new MethodGenerator();
            for (int i = 0; i < 20; i++)
            {
                // 10 types × 9 methods per type × 20 iterations = diverse coverage
                generator.Process<byte>((byte)i);
                generator.Process<sbyte>((sbyte)i);
                generator.Process<short>((short)i);
                generator.Process<ushort>((ushort)i);
                generator.Process<int>(i);
                generator.Process<uint>((uint)i);
                generator.Process<long>(i);
                generator.Process<ulong>((ulong)i);
                generator.Process<float>(i);
                generator.Process<double>(i);
                generator.Process<decimal>(i);
                generator.Process<bool>(i % 2 == 0);
                generator.Process<char>((char)('A' + (i % 26)));
                generator.Process<string>(i.ToString());
            }

            Console.WriteLine("  Generating Dynamic methods...");
            // Keep dynamic methods alive to prevent GC
            var dynamicMethods = new List<Func<int, int>>();
            for (int i = 0; i < 200; i++)
            {
                var dm = new DynamicMethod(
                    $"Generated_Dynamic_{i}",
                    typeof(int),
                    new[] { typeof(int) });

                var il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Add);
                il.Emit(OpCodes.Ret);

                var func = (Func<int, int>)dm.CreateDelegate(typeof(Func<int, int>));
                func(i); // Execute to ensure it's JIT'd
                dynamicMethods.Add(func); // Keep alive
            }

            Console.WriteLine("  Generating Async methods...");
            var tasks = new List<Task>();
            for (int i = 0; i < 50; i++)
            {
                tasks.Add(AsyncHelper(i));
            }
            await Task.WhenAll(tasks);

            Console.WriteLine("  Generating unique lambda methods...");
            // Each lambda creates a unique anonymous method/class
            var lambdas = new List<Func<int, int>>();
            for (int i = 0; i < 100; i++)
            {
                int captured = i; // Capture creates unique closure class
                Func<int, int> lambda = (x) => x + captured;
                lambdas.Add(lambda);
                _ = lambda(captured);
            }

            Console.WriteLine($"  Generated {dynamicMethods.Count} dynamic methods (kept alive)");
            Console.WriteLine($"  Generated {lambdas.Count} lambda closures");
            Console.WriteLine($"  Generated large method set successfully");
        }

        // Helper class to generate many unique methods
        class MethodGenerator
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public void Process<T>(T value)
            {
                _ = Compute1<T>(value);
                _ = Compute2<T>(value);
                _ = Compute3<T>(value);
                _ = Compute4<T>(value);

                // Use nested classes
                var nested1 = new Nested1();
                nested1.Execute<T>(value);

                var nested2 = new Nested2();
                nested2.Execute<T>(value);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            T Compute1<T>(T value) => value;

            [MethodImpl(MethodImplOptions.NoInlining)]
            T Compute2<T>(T value) => value;

            [MethodImpl(MethodImplOptions.NoInlining)]
            T Compute3<T>(T value) => value;

            [MethodImpl(MethodImplOptions.NoInlining)]
            T Compute4<T>(T value) => value;

            // Nested classes with methods
            class Nested1
            {
                [MethodImpl(MethodImplOptions.NoInlining)]
                public void Execute<T>(T value)
                {
                    _ = Method1<T>(value);
                    _ = Method2<T>(value);
                }

                [MethodImpl(MethodImplOptions.NoInlining)]
                T Method1<T>(T value) => value;

                [MethodImpl(MethodImplOptions.NoInlining)]
                T Method2<T>(T value) => value;
            }

            class Nested2
            {
                [MethodImpl(MethodImplOptions.NoInlining)]
                public void Execute<T>(T value)
                {
                    _ = Method1<T>(value);
                    _ = Method2<T>(value);
                    _ = Method3<T>(value);
                }

                [MethodImpl(MethodImplOptions.NoInlining)]
                T Method1<T>(T value) => value;

                [MethodImpl(MethodImplOptions.NoInlining)]
                T Method2<T>(T value) => value;

                [MethodImpl(MethodImplOptions.NoInlining)]
                T Method3<T>(T value) => value;
            }
        }

        // Helper methods that get JIT'd (NoInlining ensures separate methods)
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ExecuteHelper(int value)
        {
            _ = ComputeValue(value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int ComputeValue(int x) => x * 2 + 1;

        [MethodImpl(MethodImplOptions.NoInlining)]
        static async Task AsyncHelper(int value)
        {
            await Task.Yield();
            _ = value * 2;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void GenericHelper<T>(T value)
        {
            _ = value?.ToString();
        }

        // Test class with various method types
        class TestClass
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InstanceMethod()
            {
                Console.WriteLine("  TestClass.InstanceMethod()");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void StaticMethod()
            {
                Console.WriteLine("  TestClass.StaticMethod()");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void InstanceMethodWithParams(int x, string s)
            {
                Console.WriteLine($"  TestClass.InstanceMethodWithParams({x}, \"{s}\")");
            }

            public class NestedClass
            {
                [MethodImpl(MethodImplOptions.NoInlining)]
                public void NestedMethod()
                {
                    Console.WriteLine("  TestClass.NestedClass.NestedMethod()");
                }
            }
        }

        // Generic class
        class GenericClass<T>
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void GenericStaticMethod(T value)
            {
                Console.WriteLine($"  GenericClass<{typeof(T).Name}>.GenericStaticMethod({value})");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public void GenericInstanceMethod(T value)
            {
                Console.WriteLine($"  GenericClass<{typeof(T).Name}>.GenericInstanceMethod({value})");
            }
        }
    }
}
