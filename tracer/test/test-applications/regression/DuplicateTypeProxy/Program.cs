using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Samples;

namespace DuplicateTypeProxy
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            using var server = WebServer.Start(out var uri);

            await RunAsync(typeof(HttpClient), uri);

            for (int i = 0; i < 5; i++)
            {
                var assembly = Assembly.LoadFile(typeof(HttpClient).Assembly.Location);
                await RunAsync(assembly.GetType("System.Net.Http.HttpClient"), uri);
            }

#if NETCOREAPP3_1_OR_GREATER
            for (int i = 0; i < 5; i++)
            {
                var alc = new System.Runtime.Loader.AssemblyLoadContext($"Context: {i}");
                var assembly = alc.LoadFromAssemblyPath(typeof(HttpClient).Assembly.Location);
                await RunAsync(assembly.GetType("System.Net.Http.HttpClient"), uri);
            }
#endif
            Console.WriteLine("App completed successfully");
        }

        public static async Task RunAsync(Type httpClientType, string uri)
        {
            var instance = Activator.CreateInstance(httpClientType);
            var getAsync = httpClientType.GetMethod("GetAsync", new[] { typeof(string) });

            var task = (Task)getAsync.Invoke(instance, new[] { uri });
            await task;
        }
    }
}
