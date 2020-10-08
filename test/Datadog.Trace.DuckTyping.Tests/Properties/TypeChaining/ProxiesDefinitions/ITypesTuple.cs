using System;

namespace Datadog.Trace.DuckTyping.Tests.Properties.TypeChaining.ProxiesDefinitions
{
    public interface ITypesTuple
    {
        [Duck(Kind = DuckKind.Field)]
        Type ProxyDefinitionType { get; }

        [Duck(Kind = DuckKind.Field)]
        Type TargetType { get; }
    }
}
