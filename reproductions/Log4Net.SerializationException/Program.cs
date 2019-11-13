
using System;
using System.IO;
using System.Reflection;
using System.Security.Policy;

namespace Log4Net.SerializationException
{
    public class Program
    {
        public static int Main(string[] args)
        {
            // The library we want to run was built and copied to the ApplicationFiles subdirectory
            // Create an AppDomain with that directory as the appBasePath
            var entryDirectory = Directory.GetParent(Assembly.GetEntryAssembly().Location);
            var applicationFilesDirectory = Path.Combine(entryDirectory.FullName, "ApplicationFiles");
            var applicationAppDomain = AppDomain.CreateDomain("ApplicationAppDomain", null, applicationFilesDirectory, applicationFilesDirectory, false);
            var objectHandle = Activator.CreateInstance(applicationAppDomain, "ApplicationWithLog4Net", "ApplicationWithLog4Net.Program");

            // Get the program type so we can call into it from this AppDomain
            var programObject = objectHandle.Unwrap();

            try
            {
                // Test that when transition back to this AppDomain, there are no serialization problems
                // This would happen if any values were stored in data slots
                Console.WriteLine("Calling the ApplicationWithLog4Net.Program in a separate AppDomain");
                programObject.ToString();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }

            return (int)ExitCode.Success;
        }

        enum ExitCode : int
        {
            Success = 0,
            UnknownError = -10
        }
    }
}
