using System;

namespace EnumerateAssemblyReferences
{
    public class Program
    {
        public static void Main()
        {
            // We define a reference to mscorlib when injecting the startup hook
            // If the reference is incorrect, then the following code will throw
            var referencedAssemblies = typeof(Program).Assembly.GetReferencedAssemblies();

            var names = string.Empty;

            foreach (var assembly in referencedAssemblies)
            {
                names += assembly.FullName + Environment.NewLine;
            }

            Console.WriteLine(names);
            Console.WriteLine("App completed successfully");
        }
    }
}
