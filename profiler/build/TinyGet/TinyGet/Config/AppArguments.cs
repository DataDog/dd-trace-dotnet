using System;
using System.Collections.Specialized;
using System.Net.Http;

namespace TinyGet.Config
{
    internal class AppArguments : IAppArguments
    {
        public int Loop { get; private set; }

        public int Threads { get; private set; }

        public String Srv { get; private set; }

        public String Uri { get; private set; }

        public int Port { get; private set; }

        public int Status { get; private set; }

        public HttpMethod Method { get; private set; }

        public bool IsInfinite { get; private set; }

        private AppArguments()
        {

        }

        public static AppArguments Parse(NameValueCollection collection)
        {
            AppArguments result = new AppArguments();

            result.Loop = GetValue(collection, "-loop", 1);
            result.Threads = GetValue(collection, "-threads", 1);
            result.Srv = GetMandatoryValue<String>(collection, "-srv");
            result.Uri = GetValue(collection, "-uri", "/");
            result.Port = GetValue(collection, "-port", 80);
            result.Status = GetValue(collection, "-status", 200);
            result.Method = GetValue(collection, "-method", HttpMethod.Get, (m) => new HttpMethod(m));
            result.IsInfinite = GetValue(collection, "-infinite", false, x => true);
            return result;
        }

        public string GetUrl()
        {
            return string.Format("http://{0}:{1}/{2}", Srv, Port, Uri);
        }

        private static TValue GetValue<TValue>(NameValueCollection collection, string key, TValue defaultValue, Func<String, TValue> parser = null)
        {
            string rawValue = collection[key];
            if (String.IsNullOrEmpty(rawValue))
            {
                return defaultValue;
            }

            try
            {
                if (null == parser)
                {
                    Type type = typeof(TValue);
                    return (TValue)Convert.ChangeType(rawValue, type);
                }
                else
                {
                    return parser(rawValue);
                }

            }
            catch (Exception)
            {
                throw new ApplicationException("Can't parse value for argument " + key);
            }
        }

        private static TValue GetMandatoryValue<TValue>(NameValueCollection collection, string key)
        {
            string rawValue = collection[key];
            if (String.IsNullOrEmpty(rawValue))
            {
                throw new ApplicationException("Can't find argument " + key);
            }
            return GetValue(collection, key, default(TValue));
        }
    }
}
