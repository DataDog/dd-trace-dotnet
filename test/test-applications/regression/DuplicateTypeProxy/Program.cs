using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace DuplicateTypeProxy
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await RunAsync(typeof(HttpClient));

            for (int i = 0; i < 5; i++)
            {
                var assembly = Assembly.LoadFile(typeof(HttpClient).Assembly.Location);
                await RunAsync(assembly.GetType("System.Net.Http.HttpClient"));
            }

#if NETCOREAPP3_1 || NET5_0
            for (int i = 0; i < 5; i++)
            {
                var alc = new System.Runtime.Loader.AssemblyLoadContext($"Context: {i}");
                var assembly = alc.LoadFromAssemblyPath(typeof(HttpClient).Assembly.Location);
                await RunAsync(assembly.GetType("System.Net.Http.HttpClient"));
            }
#endif
        }

        public static async Task RunAsync(Type httpClientType)
        {
            var instance = Activator.CreateInstance(httpClientType);
            var getAsync = httpClientType.GetMethod("GetAsync", new[] { typeof(string) });

            var task = (Task)getAsync.Invoke(instance, new[] { "http://www.contoso.com" });
            await task;
        }
    }
}
