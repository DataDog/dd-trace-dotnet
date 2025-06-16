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
