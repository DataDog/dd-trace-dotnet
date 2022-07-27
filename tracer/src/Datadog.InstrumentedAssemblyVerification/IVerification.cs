using System.Collections.Generic;

namespace Datadog.InstrumentedAssemblyVerification
{
    internal interface IVerification
    {
        List<string> Verify();
    }
}