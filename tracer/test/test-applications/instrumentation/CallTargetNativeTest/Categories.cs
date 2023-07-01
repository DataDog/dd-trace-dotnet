using System;
using System.Text;
using CallTargetNativeTest.NoOp;
using Datadog.Trace.ClrProfiler;

namespace CallTargetNativeTest;

partial class Program
{
    static void RegisterCategorizedCallTargets()
    {
        var categoriesDefs = new NativeCallTargetDefinition2[]
        {
            new(TargetAssembly, typeof(CategoriesTests).FullName, "Cat1Method", new[] { "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop0ArgumentsIntegration).FullName, (byte)CallTargetKind.Default, 1),
            new(TargetAssembly, typeof(CategoriesTests).FullName, "Cat2Method", new[] { "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop0ArgumentsIntegration).FullName, (byte)CallTargetKind.Default, 2),
            new(TargetAssembly, typeof(CategoriesTests).FullName, "Cat4Method", new[] { "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop0ArgumentsIntegration).FullName, (byte)CallTargetKind.Default, 4),
            new(TargetAssembly, typeof(CategoriesTests).FullName, "Cat8Method", new[] { "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop0ArgumentsIntegration).FullName, (byte)CallTargetKind.Default, 8),
            new(TargetAssembly, typeof(CategoriesTests).FullName, "Cat1_2Method", new[] { "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop0ArgumentsIntegration).FullName, (byte)CallTargetKind.Default, 1|2),
            new(TargetAssembly, typeof(CategoriesTests).FullName, "Cat2_4Method", new[] { "_" }, 0,0,0,1,1,1, integrationAssembly, typeof(Noop0ArgumentsIntegration).FullName, (byte)CallTargetKind.Default, 2|4),
        };

        NativeMethods.RegisterCallTargetDefinitions("CategoriesTest", categoriesDefs, 0x1 | 0x2 | 0x4);
    }

    private static void CategoriesTest()
    {
        RegisterCategorizedCallTargets();

        var tests = new CategoriesTests();
        Console.WriteLine($"Categories Tests");

        Console.WriteLine("\n---");
        Console.WriteLine($"Registering with enabled categories 1|2|4 ");
        CategoriesTests.RunMethod("Cat1  ", () => tests.Cat1Method(), true);
        CategoriesTests.RunMethod("Cat2  ", () => tests.Cat2Method(), true);
        CategoriesTests.RunMethod("Cat4  ", () => tests.Cat4Method(), true);
        CategoriesTests.RunMethod("Cat8  ", () => tests.Cat8Method(), false);
        CategoriesTests.RunMethod("Cat1_2", () => tests.Cat1_2Method(), true);
        CategoriesTests.RunMethod("Cat2_4", () => tests.Cat2_4Method(), true);

        Console.WriteLine("\n---");
        Console.WriteLine($"Disabling category 2 (enabled 1|4) ");
        NativeMethods.DisableCallTargetDefinitions(0x2);
        CategoriesTests.RunMethod("Cat1  ", () => tests.Cat1Method(), true);
        CategoriesTests.RunMethod("Cat2  ", () => tests.Cat2Method(), false);
        CategoriesTests.RunMethod("Cat4  ", () => tests.Cat4Method(), true);
        CategoriesTests.RunMethod("Cat8  ", () => tests.Cat8Method(), false);
        CategoriesTests.RunMethod("Cat1_2", () => tests.Cat1_2Method(), true);
        CategoriesTests.RunMethod("Cat2_4", () => tests.Cat2_4Method(), true);

        Console.WriteLine("\n---");
        Console.WriteLine($"Disabling category 4 (enabled 1) ");
        NativeMethods.DisableCallTargetDefinitions(0x4);
        CategoriesTests.RunMethod("Cat1  ", () => tests.Cat1Method(), true);
        CategoriesTests.RunMethod("Cat2  ", () => tests.Cat2Method(), false);
        CategoriesTests.RunMethod("Cat4  ", () => tests.Cat4Method(), false);
        CategoriesTests.RunMethod("Cat8  ", () => tests.Cat8Method(), false);
        CategoriesTests.RunMethod("Cat1_2", () => tests.Cat1_2Method(), true);
        CategoriesTests.RunMethod("Cat2_4", () => tests.Cat2_4Method(), false);

        Console.WriteLine("\n---");
        Console.WriteLine($"Enabling category 2 (enabled 1|2) ");
        NativeMethods.EnableCallTargetDefinitions(0x2);
        CategoriesTests.RunMethod("Cat1  ", () => tests.Cat1Method(), true);
        CategoriesTests.RunMethod("Cat2  ", () => tests.Cat2Method(), true);
        CategoriesTests.RunMethod("Cat4  ", () => tests.Cat4Method(), false);
        CategoriesTests.RunMethod("Cat8  ", () => tests.Cat8Method(), false);
        CategoriesTests.RunMethod("Cat1_2", () => tests.Cat1_2Method(), true);
        CategoriesTests.RunMethod("Cat2_4", () => tests.Cat2_4Method(), true);

        Console.WriteLine("\n---");
        Console.WriteLine($"Enabling category 8 (enabled 1|2|8) ");
        NativeMethods.EnableCallTargetDefinitions(0x8);
        CategoriesTests.RunMethod("Cat1  ", () => tests.Cat1Method(), true);
        CategoriesTests.RunMethod("Cat2  ", () => tests.Cat2Method(), true);
        CategoriesTests.RunMethod("Cat4  ", () => tests.Cat4Method(), false);
        CategoriesTests.RunMethod("Cat8  ", () => tests.Cat8Method(), true);
        CategoriesTests.RunMethod("Cat1_2", () => tests.Cat1_2Method(), true);
        CategoriesTests.RunMethod("Cat2_4", () => tests.Cat2_4Method(), true);
    }

    public class CategoriesTests
    {
        internal static void RunMethod(string funcName, Action action, bool checkInstrumented = true)
        {
            var txt = checkInstrumented ? "I" : "N";
            Console.Write($"{funcName} - {txt} -> ");
            Program.RunMethod(action, checkInstrumented);
        }

        public string Cat1Method()
        {
            return "Cat1";
        }
        public string Cat2Method()
        {
            return "Cat2";
        }
        public string Cat4Method()
        {
            return "Cat4";
        }
        public string Cat8Method()
        {
            return "Cat8";
        }
        public string Cat1_2Method()
        {
            return "Cat1_2";
        }
        public string Cat2_4Method()
        {
            return "Cat2_4";
        }
    }
}
