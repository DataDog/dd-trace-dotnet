using System;
using System.Collections.Generic;

namespace Datadog.Tracer.IntegrationTests
{
    public static class ObservableUtils
    {
        public static List<T> AsList<T>(this IObservable<T> observable)
        {
            var list = new List<T>();
            observable.Subscribe((x) => list.Add(x));
            return list;
        }
    }
}
