using System;

namespace Samples.Probes.Unreferenced.External
{
    public class ExternalTest
    {
        private int _number;

        public string InstrumentMe(int k)
        {
            _number = new Random(15).Next() + k;
            return _number.ToString();
        }
    }
}
