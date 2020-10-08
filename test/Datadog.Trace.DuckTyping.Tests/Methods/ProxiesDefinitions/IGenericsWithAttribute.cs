using System;

namespace Datadog.Trace.DuckTyping.Tests.Methods.ProxiesDefinitions
{
    public interface IGenericsWithAttribute
    {
        [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.Int32" })]
        int GetDefaultInt();

        [Duck(Name = "GetDefault", GenericParameterTypeNames = new[] { "System.String" })]
        string GetDefaultString();

        [Duck(Name = "Wrap", GenericParameterTypeNames = new[] { "System.Int32", "System.String" })]
        Tuple<int, string> WrapIntString(int a, string b);
    }
}
