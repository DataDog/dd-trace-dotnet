using System;
using System.Reflection;
using System.Threading.Tasks;
using Samples.Probes.Contracts;

namespace Samples.Probes.Shared
{
    public static class SampleHelper
    {
        private const int millisecondsToWaitSetProbes = 1000 * 4;
        private const int millisecondsToWaitForSendSnapshots = 1000 * 10;

        public static async Task RunTest(string[] args)
        {
            var testName = GetArg("--test-name", args);
            var instance = GetTestInstance(testName);

            var listenUrl = GetArg("--listen-url", args);
            if (listenUrl == null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(millisecondsToWaitSetProbes));
                await RunTest(instance, testName);
                await Task.Delay(TimeSpan.FromMilliseconds(millisecondsToWaitForSendSnapshots));
            }
            else
            {
                var listener = new SimpleHttpListener(listenUrl, () => RunTest(instance, testName));
                await listener.HandleIncomingConnections();
            }
        }

        private static object GetTestInstance(string testName)
        {
            var type = Assembly.GetExecutingAssembly().GetType(testName);
            if (type == null)
            {
                throw new ArgumentException($"Type {testName} not found in assembly {Assembly.GetExecutingAssembly().GetName().Name}");
            }

            var instance = Activator.CreateInstance(type);
            return instance;
        }

        private static string GetArg(string key, string[] args)
        {
            var index = Array.IndexOf(args, key);
            if (index == -1)
            {
                return null;
            }

            return args.Length <= index ? null : args[index + 1];
        }

        private static async Task RunTest(object instance, string testClassName)
        {
            switch (instance)
            {
                case IRun run:
                    try
                    {
                        run.Run();
                    }
                    catch
                    {
                        // Ignored
                    }

                    break;
                case IAsyncRun asyncRun:
                    try
                    {
                        await asyncRun.RunAsync();
                    }
                    catch
                    {
                        // Ignored
                    }

                    break;
                default:
                    throw new Exception($"Test class not found: {testClassName}");
            }
        }
    }
}
