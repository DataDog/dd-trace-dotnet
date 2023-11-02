using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Samples.Probes.TestRuns.SmokeTests
{
#if NET462
    /// <summary>
    /// Important: This test has two phases. Upon first execution, it will create a new appdomain, execute some code inside of it and then destroy it & load the assembly `Samples.Probes.Unreferenced.External.dll`
    /// and upon second execution it will execute code from inside that assembly (namely Samples.Probes.Unreferenced.External.ExternalTest.InstrumentMe).
    /// Refer to the test `ModuleUnloadInNetFramework462Test` for more information.
    /// </summary>
    public class ModuleUnloadTest : IRun
    {
        private static AppDomain _newAppDomain;

        public void Run()
        {
            const string loadedOnDemandAssemblyName = "Samples.Probes.Unreferenced.External";
            var unreferencedAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(asm => asm.FullName.Contains(loadedOnDemandAssemblyName));

            if (unreferencedAssembly != null)
            {
                ExecuteCodeInCurrentAppDomain(unreferencedAssembly);
            }
            else
            {
                _newAppDomain = AppDomain.CreateDomain("NewAppDomain");

                _newAppDomain.DoCallBack(LoadAndExecuteInNewAppDomain);

                AppDomain.Unload(_newAppDomain);

                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var executingAssemblyLocation = Assembly.GetExecutingAssembly().Location;
                var assemblyPath = Path.Combine(baseDirectory, "Samples.Probes.Unreferenced.External.dll");
                Assembly.LoadFrom(assemblyPath);
            }

        }

        public static void ExecuteCodeInCurrentAppDomain(Assembly assembly)
        {
            Type externalTestClassType = assembly.GetType("Samples.Probes.Unreferenced.External.ExternalTest");
            object externalTestClassInstance = Activator.CreateInstance(externalTestClassType);
            MethodInfo instrumentMeMethod = externalTestClassType.GetMethod("InstrumentMe");
            instrumentMeMethod.Invoke(externalTestClassInstance, new object[] { 5 });
        }

        public static void LoadAndExecuteInNewAppDomain()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var assemblyPath = Path.Combine(baseDirectory, "Samples.Probes.Unreferenced.External.dll");
            var loadedAssembly = Assembly.Load(File.ReadAllBytes(assemblyPath));

            var externalTestClassType = loadedAssembly.GetType("Samples.Probes.Unreferenced.External.ExternalTest");
            var externalTestClassInstance = Activator.CreateInstance(externalTestClassType);

            var instrumentMeMethod = externalTestClassType.GetMethod("InstrumentMe");
            instrumentMeMethod.Invoke(externalTestClassInstance, new object[] { 5 });
        }
    }

#endif
}
