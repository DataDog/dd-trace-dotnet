using System.Runtime.CompilerServices;
using Samples.Probes.Shared;

namespace Samples.Probes.SmokeTests
{
    internal class GenericMethodWithArguments : IRun
    {
        public string Prop { get; } = nameof(GenericMethodWithArguments);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Run()
        {
            var p = new Person("Alfred Hitchcock", 30, new Address { HomeType = BuildingType.Duplex, Number = 5, Street = "Elsewhere" }, System.Guid.NewGuid(), null);
            Method(p);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        [MethodProbeTestData("System.String", new[] { "!!0" })]
        public string Method<T>(T genericParam)
        {
            return genericParam.ToString();
        }
    }
}
