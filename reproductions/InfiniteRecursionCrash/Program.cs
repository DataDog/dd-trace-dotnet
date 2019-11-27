using System;
using System.Globalization;
using System.IO;
using System.IO.IsolatedStorage;
using System.Net.Http;
using System.Threading;

namespace InfiniteRecursionCrash
{
    internal class Program
    {
        public static int Main()
        {
            var ci = new CultureInfo("fr-FR");
            Thread.CurrentThread.CurrentUICulture = ci;

            using (IsolatedStorageFile isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Domain | IsolatedStorageScope.Assembly, null, null))
            {
                try
                {
                    using (var outputStream = isoStore.OpenFile("Folder/" + "filename", FileMode.Create, FileAccess.Write)) // <- here is the problem (I tried with backslash (\\) and also doesnt work.
                    {
                        // Nothing to do in here. The openFile call will trigger a FileNotFoundException which will cause an mscorlib resources load.
                    }
                }
                catch (System.IO.IsolatedStorage.IsolatedStorageException)
                {
                    Console.WriteLine("The AssemblyResolve event didn't cause infinite recusion. The original System.IO.IsolatedStorage.IsolatedStorageException exception was correctly thrown and caught.");
                    return (int)ExitCode.Success;
                }
                catch (System.IO.DirectoryNotFoundException)
                {
                    Console.WriteLine("The AssemblyResolve event didn't cause infinite recusion. The original System.IO.DirectoryNotFoundException exception was correctly thrown and caught.");
                    return (int)ExitCode.Success;
                }
            }

            return (int)ExitCode.UnknownError;
        }

        // This class will not be used, but making sure we have an assembly reference to System.Net.Http
        // will make sure this module is considered for JITCompilationStarted replacement, triggering
        // the eager loading logic and registration of the AssemblyResolve event
        public class DerivedHandler : HttpClientHandler
        {
        }
    }

    enum ExitCode : int
    {
        Success = 0,
        UnknownError = -10
    }
}
