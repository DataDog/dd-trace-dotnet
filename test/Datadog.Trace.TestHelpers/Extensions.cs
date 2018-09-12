using System;
using System.Collections;

namespace Datadog.Trace.TestHelpers
{
    public static class Extensions
    {
        public static T Get<T>(this IDictionary obj, string key)
        {
            if (obj.Contains(key))
            {
                object value = obj[key];
                if (value is IConvertible)
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }

                if (value is T)
                {
                    return (T)value;
                }

                if (typeof(IDictionary).IsAssignableFrom(typeof(T)))
                {
                    if (value is IDictionary)
                    {
                        var from = value as IDictionary;
                        var to = (IDictionary)typeof(T).GetConstructor(new Type[] { }).Invoke(new object[] { });
                        foreach (var subkey in from.Keys)
                        {
                            to.Add(subkey, from[subkey]);
                        }

                        return (T)to;
                    }
                }
            }

            return default(T);
        }
    }
}
