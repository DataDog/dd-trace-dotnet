using System;

namespace Datadog.Trace.DuckTyping.Tests.Methods.ProxiesDefinitions
{
    public class VirtualGenericsWithAttribute
    {
        [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.Int32" })]
        public virtual int GetDefaultInt() => 100;

        [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.String" })]
        public virtual string GetDefaultString() => string.Empty;

        [Duck(Name = "Wrap", GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
        public virtual Tuple<int, string> WrapIntString(int a, string b) => null;
    }
}
