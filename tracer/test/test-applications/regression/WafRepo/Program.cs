using System;
using System.IO;
using System.Reflection;
using System.Security;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("Starting");

        var entries = Directory.EnumerateFileSystemEntries("/opt/datadog/");

        foreach (string entry in entries)
        {
            Console.WriteLine(entry);
        }

        entries = Directory.EnumerateFileSystemEntries("/opt/datadog/linux-arm64/");

        foreach (string entry in entries)
        {
            Console.WriteLine(entry);
        }

        if (!NativeLibrary.TryLoad("/opt/datadog/linux-arm64/Datadog.Profiler.Native.so", out var cpHandler))
        {
            Console.WriteLine("failed to load CP");
            return 1;
        }

        if (!NativeLibrary.CloseLibrary(cpHandler))
        {
            Console.WriteLine("failed to unload CP");
            return 1;
        }

        if (!NativeLibrary.TryLoad("/opt/datadog/libddwaf.so", out var libraryHandle))
        {
            Console.WriteLine("failed to load WAF");
            return 1;
        }

        var invoker = new WafLibraryInvoker(libraryHandle);

        var version = invoker.GetVersion();
        Console.WriteLine(version);

        DdwafConfigStruct configStruct = default;
        var wafBuilderHandle = invoker.InitBuilder(ref configStruct);
        Console.WriteLine("wafBuilderHandle: " + wafBuilderHandle);

        var encoder = new EncoderLegacy(invoker);

        var stream = File.OpenRead("rule-set.json");
        using var reader = new StreamReader(stream);
        using var jsonReader = new JsonTextReader(reader);
        var root = JToken.ReadFrom(jsonReader);
        var config = encoder.Encode(root, applySafetyLimits: false);
        DdwafObjectStruct diag = default;

        var res = invoker.BuilderAddOrUpdateConfig(wafBuilderHandle, "fake-path", ref config, ref diag);
        Console.WriteLine("BuilderAddOrUpdateConfig res: " + res);

        var wafHandle = invoker.BuilderBuildInstance(wafBuilderHandle);
        Console.WriteLine("wafHandle: " + wafHandle);

        var result = invoker.InitContext(wafHandle);
        Console.WriteLine("result: " + result);

        return 0;
    }
}
