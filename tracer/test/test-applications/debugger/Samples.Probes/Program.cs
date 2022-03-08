using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Samples.Probes;
using Samples.Probes.Shared;

public static class Program
{
    private const int millisecondsToWaitSetProbes = 1000 * 2;
    private const int millisecondsToWaitForSendSnapshots = 1000 * 1;

    public static async Task Main(string[] args)
    {
        var testClassName = args[0];
        var type = Assembly.GetExecutingAssembly().GetType(testClassName);
        if (type == null)
        {
            throw new ArgumentException($"Type {testClassName} not found in assembly {Assembly.GetExecutingAssembly().GetName().Name}");
        }

        object instance;
        if (type.IsGenericType)
        {
            var constructedClass = type.MakeGenericType(typeof(IGeneric));
            instance = Activator.CreateInstance(constructedClass);
        }
        else
        {
            instance = Activator.CreateInstance(type);
        }

        await RunTest(instance, testClassName);
        Thread.Sleep(millisecondsToWaitForSendSnapshots);
    }

    private static async Task RunTest(object instance, string testClassName)
    {
        Thread.Sleep(millisecondsToWaitSetProbes);
        switch (instance)
        {
            case IRun run:
                run.Run();
                break;
            case IAsyncRun asyncRun:
                await asyncRun.RunAsync();
                break;
            default:
                Console.Error.WriteLine($"Test class not found: {testClassName}");
                throw new Exception($"Test class not found: {testClassName}");
        }
    }
}
