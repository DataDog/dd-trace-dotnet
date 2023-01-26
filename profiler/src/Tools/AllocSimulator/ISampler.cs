using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllocSimulator
{
    internal interface ISampler
    {
        public void Allocate(string type, string aggregationKey, long size);

        public IEnumerable<(string Type, string AggregationKey, int Count, int Size)> GetAllocs();
    }
}
