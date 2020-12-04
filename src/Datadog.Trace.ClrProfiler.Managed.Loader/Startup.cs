using System;
using System.IO;
using System.Reflection;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    /// <summary>
    /// This is the only publi class in this assembly.
    /// The entire assembly is compiled into the native profiler DLL as a resource.
    /// This happens both, for the profiler part of the Tracer, and for the actual Profiler.
    /// The native code then uses this class to call arbitrary managed code:
    /// It uses IL rewriting to inject a call to <c>Startup.Run(..)</c> and passes a list of assemblies.
    /// Then, this class loads all of the specified assemblies and calls a well known etry point in those assemblies.
    /// (See <c>TargetLibraryEntrypointXxx</c> constants in this class.)
    /// If the specified assemblies do not contain such an entry point, they will be loaded, but nothing will be executed.
    ///
    /// This class sets up a basic AppDomain.AssemblyResolve handler to look for the assemblies in a framwork-specific
    /// subdirectory of DD_DOTNET_TRACER_HOME in addition to the normal probing paths.
    /// If also allows for some SxS loading using costom Assembly Load Context.
    ///
    /// If a target assembly needs additional AssemblyResolve to satisfy its dependencies of for any other reasons,
    /// it should set up its own as the first thing after its entry point is called.
    ///
    /// ! Do not make the AppDomain.AssemblyResolve handler in here more complex !
    /// If anything, it should be simplified and any special logic should be moved into the respective assemblies
    /// requested for loading.
    ///
    /// ! Also, remember that this assembly is shared between the Tracer's profiler component
    /// and the Profiler's profiler component. DO not put specialized code here !
    /// </summary>
    public partial class Startup
    {
        /// <summary>
        /// The constants <c>TargetLibraryEntrypointMethod</c>, <c>...Type</c> and <c>...Namespace</c> specify
        /// which entrypoint to call in the specified assemblies. The respective assembly is expected to have
        /// exactly this entry point or nothing will be invoked.
        /// The method must be static, the return type of the method must be <c>void</c> and it must have no parameters.
        /// Before doing anything else, the target assemblies must set up AppDomain AssemblyResolve events that
        /// make sure that their respective dependencies can be loaded.
        /// </summary>
        public const string TargetLibraryEntrypointMethod = "Run";

        /// <summary> <see cref="Startup.TargetLibraryEntrypointMethod" /> </summary>
        public const string TargetLibraryEntrypointType = "DllMail";

        /// <summary> <see cref="Startup.TargetLibraryEntrypointMethod" /> </summary>
        public const string TargetLibraryEntrypointNamespace = "Datadog.AutoInstrumentation";

        private const string TargetLibraryEntrypointFullTypeName = TargetLibraryEntrypointNamespace
                                                                 + "."
                                                                 + TargetLibraryEntrypointType;

#pragma warning disable SA1308 // Variable names must not be prefixed (if not this, what is the static prefix?)
        private static string s_managedProfilerDirectory = null;
#pragma warning restore SA1308 // Variable names must not be prefixed

        private readonly string[] _assemblyNames;

        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        /// <param name="assemblyNames">List of assemblies to load ans start.</param>
        public Startup(string[] assemblyNames)
        {
            _assemblyNames = assemblyNames;
        }

        /// <summary>
        /// Instntites a <c>Startup</c> instance with the specified assemblies and executes it.
        /// </summary>
        /// <param name="assemblyNames">List of assemblies to load ans start.</param>
        public static void Run(string[] assemblyNames)
        {
            var startup = new Startup(assemblyNames);
            startup.Execute();
        }

        /// <summary>
        /// Loads the assemblied specified for this <c>Startup</c> instance and executes their entry point.
        /// </summary>
        public void Execute()
        {
            if (_assemblyNames == null)
            {
                StartupLogger.Log($"Not loading any assemblies ({nameof(_assemblyNames)} is null). ");
                return;
            }

            if (_assemblyNames.Length == 0)
            {
                StartupLogger.Log($"Not loading any assemblies ({nameof(_assemblyNames)}.{nameof(_assemblyNames.Length)} is 0). ");
                return;
            }

            ResolveManagedProfilerDirectory();
            StartupLogger.Log($"Will try to load {_assemblyNames.Length} assemblies. Directory: \"{s_managedProfilerDirectory}\".");

            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolveEventHandler;
            }
            catch (Exception ex)
            {
                StartupLogger.Log(ex, "Unable to register a callback to the CurrentDomain.AssemblyResolve event.");
            }

            for (int i = 0; i < _assemblyNames.Length; i++)
            {
                string assemblyName = _assemblyNames[i];

                try
                {
                    LoadAndStartAssembly(assemblyName);
                }
                catch (Exception ex)
                {
                    StartupLogger.Log(ex, $"Error loading or starting a managed assembly (\"{assemblyName}\").");
                }
            }
        }

        private static void LoadAndStartAssembly(string assemblyName)
        {
            if (assemblyName == null)
            {
                StartupLogger.Log($"Skipping loading assembly because the specified {nameof(assemblyName)} is null.");
                return;
            }

            assemblyName = assemblyName.Trim();

            if (assemblyName.Length == 0)
            {
                StartupLogger.Log($"Skipping loading assembly because the specified {nameof(assemblyName)} is \"\".");
                return;
            }

            StartupLogger.Log($"Loading managed assembly \"{assemblyName}\""
                            + $" and invoking method \"{TargetLibraryEntrypointMethod}\""
                            + $" in type \"{TargetLibraryEntrypointFullTypeName}\".");

            Assembly assembly = Assembly.Load(assemblyName);
            if (assembly == null)
            {
                StartupLogger.Log($"Could not load managed assembly \"{assemblyName}\".");
                return;
            }

            Type entryPointType = assembly.GetType(TargetLibraryEntrypointFullTypeName, throwOnError: false);
            if (entryPointType == null)
            {
                StartupLogger.Log($"Could not obtain type \"{TargetLibraryEntrypointFullTypeName}\""
                                + $" from managed assembly \"{assemblyName}\".");
                return;
            }

            MethodInfo entryPointMethod = entryPointType.GetRuntimeMethod(TargetLibraryEntrypointMethod, parameters: new Type[0]);
            if (entryPointMethod == null)
            {
                StartupLogger.Log($"Could not obtain method \"{TargetLibraryEntrypointMethod}\""
                                + " in type \"{TargetLibraryEntrypointFullTypeName}\""
                                + $" from managed assembly \"{assemblyName}\".");
                return;
            }

            entryPointMethod.Invoke(obj: null, parameters: null);
        }

        private static AssemblyName ParseAssemblyName(string fullAssemblyName)
        {
            if (string.IsNullOrEmpty(fullAssemblyName))
            {
                return null;
            }

            try
            {
                var assemblyName = new AssemblyName(fullAssemblyName);
                return assemblyName;
            }
            catch
            {
                return null;
            }
        }

        private static string ReadEnvironmentVariable(string key)
        {
            try
            {
                return Environment.GetEnvironmentVariable(key);
            }
            catch (Exception ex)
            {
                StartupLogger.Log(ex, $"Error while loading environment variable \"{key}\".");
            }

            return null;
        }

        private void ResolveManagedProfilerDirectory()
        {
            Assembly objectAssembly = (new object()).GetType().Assembly;
            bool isCoreFx = (objectAssembly?.FullName?.StartsWith("System.Private.CoreLib") == true);

            string frameworkBasedSubdir;
            if (isCoreFx)
            {
                frameworkBasedSubdir = "netstandard2.0";

                // Old versions of .NET core report a major version of 4
                Version clrVersion = Environment.Version;
                if ((clrVersion.Major == 3 && clrVersion.Minor >= 1) || clrVersion.Major >= 5)
                {
                    frameworkBasedSubdir = "netcoreapp3.1";
                }
            }
            else
            {
                // We currently build two assemblies targeting .NET Framework.
                // If we're running on the .NET Framework, load the highest-compatible assembly
                string corlibFileVersionString = ((AssemblyFileVersionAttribute)objectAssembly.GetCustomAttribute(typeof(AssemblyFileVersionAttribute))).Version;
                string corlib461FileVersionString = "4.6.1055.0";

                // This will throw an exception if the version number does not match the expected 2-4 part version number of non-negative int32 numbers,
                // but mscorlib should be versioned correctly
                var corlibVersion = new Version(corlibFileVersionString);
                var corlib461Version = new Version(corlib461FileVersionString);
                frameworkBasedSubdir = corlibVersion < corlib461Version ? "net45" : "net461";
            }

            string tracerHomeDirectory = ReadEnvironmentVariable("DD_DOTNET_TRACER_HOME") ?? string.Empty;
            s_managedProfilerDirectory = Path.Combine(tracerHomeDirectory, frameworkBasedSubdir);
        }

        private bool TryFindAssemblyInProfilerDirectory(AssemblyName assemblyName, out string fullPath)
        {
            fullPath = Path.Combine(s_managedProfilerDirectory, $"{assemblyName.Name}.dll");
            return File.Exists(fullPath);
        }

        private bool ShouldLoadAssemblyFromProfilerDirectory(AssemblyName assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName?.Name) || string.IsNullOrEmpty(assemblyName?.FullName))
            {
                return false;
            }

            // The AssemblyResolveEventHandler will handle all assemblies that have these prefixes:
            // If the could not be found, we will look for them in the TRACER_HOME:

            bool shouldHandle = (assemblyName.Name.StartsWith("Datadog.Trace", StringComparison.OrdinalIgnoreCase) == true)
                    || (assemblyName.Name.StartsWith("Datadog.AutoInstrumentation", StringComparison.OrdinalIgnoreCase) == true);

            // If an assembly does not have the abpve prefix, but it has been specified as the actual startup assembly,
            // we will also look for it in TRACER_HOME:

            for (int i = 0; !shouldHandle && i < _assemblyNames.Length; i++)
            {
                shouldHandle = assemblyName.FullName.Equals(_assemblyNames[i], StringComparison.OrdinalIgnoreCase);
            }

            return shouldHandle;
        }
    }
}
