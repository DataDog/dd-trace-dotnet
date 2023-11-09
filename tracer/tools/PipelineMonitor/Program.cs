// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using System.Text.Json.Serialization;
using Datadog.Trace;
using Datadog.Trace.Configuration;
using PipelineMonitor;

const string azDoApi = "https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds/";
var _cli = new HttpClient();

var tracerSettings = new TracerSettings {Environment = "apm-dotnet-pipeline-monitoring", GlobalSamplingRate = 100};
Tracer.Configure(tracerSettings);

if (args?.Length == 0)
{
    Console.WriteLine("please provide a buildid");
    return;
}

var buildId = args[0];
Console.WriteLine("Processing pipeline: " + buildId);
Build? buildData;
TimelineData? timeline;

var jsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
};

try
{
    var json = await _cli.GetStringAsync(azDoApi + buildId);
    buildData = JsonSerializer.Deserialize<Build>(json, jsonOpts);

    if (buildData is null || string.IsNullOrEmpty(buildData._Links.Timeline.Href))
    {
        Console.WriteLine("Timeline url not found");
        return;
    }
}
catch (Exception ex)
{
    Console.WriteLine("An error occured getting build data. Build Url: {0}", azDoApi + buildId);
    Console.WriteLine("Exception: {0}", ex);
    return;
}

try
{
    var jsonTimeline = await _cli.GetStringAsync(buildData._Links.Timeline.Href);
    timeline = JsonSerializer.Deserialize<TimelineData>(jsonTimeline, jsonOpts);

    if (timeline is null)
    {
        Console.WriteLine("Timeline data hasn't been deserialized");
        return;
    }
}
catch (Exception ex)
{
    Console.WriteLine("An error occured getting timeline data. Timeline Url: {0}", buildData._Links.Timeline.Href);
    Console.WriteLine("Exception: {0}", ex);
    return;
}

try
{
    var processor = new BuildProcessor(buildData, timeline);
    processor.Process();
    Console.WriteLine("Build " + buildId + " has been processed successfully. It should be visible at: https://ddstaging.datadoghq.com/apm/services/consolidated-pipeline/operations/ci_run/resources");
    await Tracer.Instance.ForceFlushAsync();
}
catch (Exception ex)
{
    Console.WriteLine("an error occured while processing the pipeline. Ex: {0}", ex);
}
