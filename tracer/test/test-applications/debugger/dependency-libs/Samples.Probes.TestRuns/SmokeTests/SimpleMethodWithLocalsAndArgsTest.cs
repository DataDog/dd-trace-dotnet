using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    public class SimpleMethodWithLocalsAndArgsTest : IRun
    {
        private string _privateField = nameof(IRun);

        public void Run()
        {
            Method(nameof(Run));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public string Method(string lastName)
        {
            var biggerName = lastName;

            for (var i = 0; i < lastName.Length * 5; i++)
            {
                biggerName += i.ToString();
            }

            return biggerName + $"_" + lastName + $"_" + _privateField;
        }
    }
}
