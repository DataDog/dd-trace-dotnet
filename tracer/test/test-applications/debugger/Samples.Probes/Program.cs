using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Samples.Probes;

public static class Program
{
    private const int millisecondsToWaitSetProbes = 1000 * 4;
    private const int millisecondsToWaitForSendSnapshots = 1000 * 10;

    public static async Task Main(string[] args)
    {
        var testClassName = args[0];
        var spinCount = args.Length > 1 ? int.Parse(args[1]) : 1;
        var type = Assembly.GetExecutingAssembly().GetType(testClassName);
        if (type == null)
        {
            throw new ArgumentException($"Type {testClassName} not found in assembly {Assembly.GetExecutingAssembly().GetName().Name}");
        }

        var instance = Activator.CreateInstance(type);

        for (var spinIndex = 0; spinIndex < spinCount; spinIndex++)
        {
            Thread.Sleep(millisecondsToWaitSetProbes);
            await RunTest(instance, testClassName);
        }

        Thread.Sleep(millisecondsToWaitForSendSnapshots);
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
                Console.Error.WriteLine($"Test class not found: {testClassName}");
                throw new Exception($"Test class not found: {testClassName}");
        }
    }
}
