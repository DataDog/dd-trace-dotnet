using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Samples.Probes.TestRuns.SmokeTests
{
    /// <summary>
    /// Important: This test has two phases. Upon first execution, it will load the assembly `Samples.Probes.Unreferenced.External.dll`
    /// and upon second execution it will execute code from inside that assembly (namely Samples.Probes.Unreferenced.External.ExternalTest.InstrumentMe).
    /// Refer to the test `LineProbeUnboundProbeBecomesBoundTest` for more information.
    /// </summary>
    public class UnboundProbeBecomesBoundTest : IRun
    {
        public void Run()
        {
            const string loadedOnDemandAssemblyName = "Samples.Probes.Unreferenced.External";

            var unreferencedAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(asm => asm.FullName.Contains(loadedOnDemandAssemblyName));
            if (unreferencedAssembly == null)
            {
                // The assembly is not loaded. Loading it and returning.
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var executingAssemblyLocation = Assembly.GetExecutingAssembly().Location;
                var assemblyPath = Path.Combine(baseDirectory, "Samples.Probes.Unreferenced.External.dll");
                Assembly.LoadFrom(assemblyPath);
                return;
            }

            // Get the "ExternalTest" class from the external assembly
            Type externalTestClassType = unreferencedAssembly.GetType("Samples.Probes.Unreferenced.External.ExternalTest");
            object externalTestClassInstance = Activator.CreateInstance(externalTestClassType);

            // Get the "InstrumentMe" method
            MethodInfo instrumentMeMethod = externalTestClassType.GetMethod("InstrumentMe");

            // Call the "InstrumentMe" method
            instrumentMeMethod.Invoke(externalTestClassInstance, new object[] { 5 });
        }
    }
}
