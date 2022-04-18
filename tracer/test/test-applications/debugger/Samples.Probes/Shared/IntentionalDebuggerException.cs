using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Probes.Shared
{
    internal class IntentionalDebuggerException : Exception
    {
        public IntentionalDebuggerException(string message)
            : base(message)
        {
        }
    }
}
