using System;

namespace Datadog.Trace.DuckTyping.Tests.Methods.ProxiesDefinitions
{
    public abstract class AbstractGenericsWithAttribute
    {
        [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.Int32" })]
        public abstract int GetDefaultInt();

        [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.String" })]
        public abstract string GetDefaultString();

        [Duck(Name = "Wrap", GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
        public abstract Tuple<int, string> WrapIntString(int a, string b);
    }
}
