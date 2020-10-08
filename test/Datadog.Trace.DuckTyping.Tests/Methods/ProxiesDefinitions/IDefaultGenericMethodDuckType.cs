using System;

namespace Datadog.Trace.DuckTyping.Tests.Methods.ProxiesDefinitions
{
    public interface IDefaultGenericMethodDuckType
    {
        T GetDefault<T>();

        Tuple<T1, T2> Wrap<T1, T2>(T1 a, T2 b);
    }
}
