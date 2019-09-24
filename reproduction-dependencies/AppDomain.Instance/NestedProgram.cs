using System;

namespace AppDomain.Instance
{
    public abstract class NestedProgram : MarshalByRefObject
    {
        public string AppDomainName { get; set; }
        public int AppDomainIndex { get; set; }
        public abstract void Run();
    }
}
