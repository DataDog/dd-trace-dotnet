using System;
using System.Collections;
using System.Linq;

namespace Samples.ServiceStackRedis
{
    class RedisClientWrapper
    {
        readonly object _client;

        readonly Func<string, int, bool> _set;
        readonly Func<bool> _ping;
        readonly CustomDelegate _custom;
        readonly Func<string, string> _echo;
        readonly Func<int?, IEnumerable> _getSlowlog;
        readonly Func<string, long> _incr;
        readonly Func<string, double, double> _incrByFloat;
        readonly Func<DateTime> _getServerTime;
        readonly Action<long> _changeDb;

        // Use a new Delegate type so we can use `params object[] cmdWithArgs`
        delegate object CustomDelegate(params object[] cmdWithArgs);

        public RedisClientWrapper(object client)
        {
            _client = client;

            // Populate delegates
            var type = client.GetType();
            _set = (Func<string, int, bool>)type.GetMethods().Where(m => m.Name == "Set" && m.IsGenericMethod && m.GetParameters().Length == 2).First().MakeGenericMethod(typeof(int)).CreateDelegate(typeof(Func<string, int, bool>), _client);
            _ping = (Func<bool>)type.GetMethod("Ping").CreateDelegate(typeof(Func<bool>), _client);
            _custom = (CustomDelegate)type.GetMethod("Custom").CreateDelegate(typeof(CustomDelegate), _client);
            _echo = (Func<string, string>)type.GetMethod("Echo").CreateDelegate(typeof(Func<string, string>), _client);
            _getSlowlog = (Func<int?, IEnumerable>)type.GetMethod("GetSlowlog").CreateDelegate(typeof(Func<int?, IEnumerable>), _client);
            _incr = (Func<string, long>)type.GetMethod("Incr").CreateDelegate(typeof(Func<string, long>), _client);
            _incrByFloat = (Func<string, double, double>)type.GetMethod("IncrByFloat").CreateDelegate(typeof(Func<string, double, double>), _client);
            _getServerTime = (Func<DateTime>)type.GetMethod("GetServerTime").CreateDelegate(typeof(Func<DateTime>), _client);
            _changeDb = (Action<long>)type.GetMethod("ChangeDb").CreateDelegate(typeof(Action<long>), _client);
        }

        public bool Set(string key, int value) => _set(key, value);

        public bool Ping() => _ping();

        public object Custom(params object[] cmdWithArgs) => _custom(cmdWithArgs);

        public string Echo(string text) => _echo(text);

        public IEnumerable GetSlowlog(int? numberOfRecords) => _getSlowlog(numberOfRecords);

        public long Incr(string key) => _incr(key);

        public double IncrByFloat(string key, double incrBy) => _incrByFloat(key, incrBy);

        public DateTime GetServerTime() => _getServerTime();

        public void ChangeDb(long db) => _changeDb(db);
    }
}
