#nullable enable
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Samples.Probes.TestRuns.SmokeTests
{
    internal class BaseLocalWithConcreteTypeInAsyncMethod : IAsyncRun
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task RunAsync()
        {
            await Pii(2);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [LogMethodProbeTestData]
        public async Task<string> Pii(int arg)
        {
            PiiBase? pii;

            switch (arg)
            {
                case 1:
                    pii = await Task.FromResult<PiiBase>(new Pii1());
                    break;
                case 2:
                    pii = await Task.FromResult<PiiBase>(new Pii2());
                    break;
                case 3:
                    pii = await Task.FromResult<PiiBase>(new Pii3());
                    break;
                default:
                    pii = await Task.FromResult<PiiBase?>(null);
                    break;
            }

            var value = pii?.TestValue;
            return $"PII {value}";
        }
    }

    internal class PiiBase
    {
        public string TestValue { get; set; } = "PiiBase";
    }

    internal class Pii1 : PiiBase
    {
        public string Pii1Value { get; set; } = "Pii1";
    }

    internal class Pii2 : PiiBase
    {
        public string Pii2Value { get; set; } = "Pii2";
    }

    internal class Pii3 : PiiBase
    {
        public string Pii3Value { get; set; } = "Pii3";
    }
}
