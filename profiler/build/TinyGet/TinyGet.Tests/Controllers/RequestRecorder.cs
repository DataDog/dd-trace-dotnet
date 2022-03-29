using System;
using System.Collections.Concurrent;
using System.Linq;

namespace TinyGet.Tests.Controllers
{
    internal static class RequestRecorder
    {
        private static ConcurrentDictionary<String, int> _requests = new ConcurrentDictionary<string, int>();

        public static void Reset()
        {
            _requests = new ConcurrentDictionary<string, int>();
        }

        public static void Increment(string key)
        {
            _requests.AddOrUpdate(key, 1, (k, v) => v+1);
        }

        public static int GetTotal()
        {
            return _requests.Sum(pair => pair.Value);
        }

        public static int Get(string key)
        {
            _requests.TryAdd(key, 0);
            return _requests[key];
        }
    }
}
