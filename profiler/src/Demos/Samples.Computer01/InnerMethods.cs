using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Samples.Computer01
{
    public class InnerMethods : ScenarioBase
    {
        public override void OnRun()
        {
            CallInnerMethods();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CallInnerMethods()
        {
            EnclosingType.Run(1000);
        }

        public class EnclosingType
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void Run(int number)
            {
                bool NamedInnerMethod(IEnumerable<int> seq)
                {
                    int count = 0;  // capture for the first lambda
                    var found = seq.Any(v =>
                    {
                        var anotherCount = 0;  // inner capture for the second lambda
                        count++;

                        var greater = Enumerable.Range(10, 100).Any(ov =>
                        {
                            anotherCount++;
                            return ov == count + anotherCount;
                        });

                        return greater ? false : greater;
                    });

                    return found;
                }

                var sequence = Enumerable.Range(0, number);
                var above = NamedInnerMethod(sequence);

                Thread.Sleep(0);
            }
        }
    }
}
