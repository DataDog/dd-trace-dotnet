using System;
using System.Runtime.CompilerServices;
using Samples.Probes.TestRuns.Shared;

namespace Samples.Probes.TestRuns.SmokeTests
{
    [LogLineProbeTestData(lineNumber: 36)]
    internal class InterfaceProperties : IRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            var implementGenericInterface = new Class<string> { Value = "T Value" };
            var implementInterface = new Class { DoNotShowMe = "bla bla", ShowMe = "Show Me!" };
            Method(implementGenericInterface, implementInterface);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData]
        public T Method<T>(IGenericInterface<T> parameter1, IInterface parameter2) where T : class
        {
            Console.WriteLine($"{parameter1.Value}");
            Console.WriteLine($"{parameter2.ShowMe}, {parameter2.DoNotShowMe}");

            IInterface iInterface = new Class { ShowMe = string.Empty};
            IGenericInterface<T> iGenericInterface = (IGenericInterface<T>)new Class<string> { GenericValue = "", Value = "Value"};

            Console.WriteLine($"{iInterface.ShowMe}");
            Console.WriteLine($"{iGenericInterface.Value}");

            if (Check(iInterface) && Check(iGenericInterface))
            {
                return parameter1.Value;
            }

            return ((Class<T>)parameter1).GenericValue;
        }

        private bool Check(IInterface iInterface)
        {
            return iInterface.ShowMe.Length == ToString()!.Length;
        }

        private bool Check<T>(IGenericInterface<T> iInterface)
        {
            return iInterface.Value.ToString()?.Length == ToString()!.Length;
        }
    }
}
