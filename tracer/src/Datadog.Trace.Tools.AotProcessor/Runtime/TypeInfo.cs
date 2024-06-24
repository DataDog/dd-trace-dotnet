using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.Tools.AotProcessor.Runtime
{
    internal class TypeInfo(ModuleInfo module)
    {
        public ModuleInfo Module { get; } = module;
    }
}
