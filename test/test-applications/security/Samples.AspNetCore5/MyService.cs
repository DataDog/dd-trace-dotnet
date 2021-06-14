using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Samples.AspNetCore5
{
    public class MyService : IDisposable
    {
        public void Dispose()
        {
            Debug.Write("disposing service");
        }
    }
}
