using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

void CreateTimeTravelJson(string targetDirectory)
{
    if (Directory.Exists(targetDirectory) == false)
    {
        Console.WriteLine("Invalid directory");
        return;
    }

    var files = Directory.GetFiles(targetDirectory, "*.json", SearchOption.AllDirectories);
    var ximoFrames = files.Where(f => f.Contains(" line ")).Select(ConvertToTimeTravelFormat).OrderBy(f => f.timestamp).ToList();

    var root = CreateRootJson(ximoFrames);
    var jsonString = root.ToString(Formatting.Indented);
    Console.WriteLine(jsonString);
    File.WriteAllText(@"C:\dev\time-travel.json", jsonString);
    var url = "vscode://datadog.dd-tt/dev/time-travel.json";
    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}

(string snapshotId, string methodId, ulong timestamp, JObject frame) ConvertToTimeTravelFormat(string file)
{
    var json = File.ReadAllText(file);
    var snapshot = JObject.Parse(json);
    string snapshotId = snapshot.SelectToken("debugger.snapshot.id").ToString();
    string methodId = snapshot.SelectToken("debugger.snapshot.parent_id")?.ToString() ?? "";
    ulong timestamp= ulong.Parse(snapshot.SelectToken("debugger.snapshot.timestamp").ToString());
    var path = snapshot.SelectToken("debugger.snapshot.probe.location.file").ToString();
    var line = ((JArray)snapshot.SelectToken("debugger.snapshot.probe.location.lines")).First();
    
    var locals = (JObject)snapshot.SelectToken("debugger.snapshot.captures.lines.*.locals");

    return (snapshotId, methodId, timestamp, new JObject
    {
        ["id"] = 0,
        ["name"] = "fff",
        ["source"] = new JObject
        {
            ["name"] = Path.GetFileName(path),
            ["path"] = path
        },
        ["line"] = line,
        ["column"] = 0,
        ["childFrameID"] = "B",
        ["vars"] = new JObject
        {
            ["1"] = ConvertLocals(locals)
        }
    });

    JToken? ConvertLocals(JObject snapshotLocals)
    {
        if (snapshotLocals == null) return null;
        var ximoLocals = new List<JObject>();
        foreach (var snapshotLocal in snapshotLocals)
            ximoLocals.Add(
                new JObject
                {
                    ["name"] = snapshotLocal.Key,
                    ["value"] = snapshotLocal.Value["value"],
                    ["type"] = snapshotLocal.Value["type"],
                    ["variablesReference"] = 0
                });
        return new JArray(ximoLocals);
    }
}

JObject CreateRootJson(List<(string snapshotId, string methodId, ulong timestamp, JObject frame)> frames)
{
    var o = new JObject
    {
        ["initial"] = frames.First().snapshotId,
        ["scopes"] = new JArray
        {
            new JObject
            {
                ["name"] = "Local",
                ["variablesReference"] = 1
            }
        },
        ["frames"] = new JObject()
    };

    var methodExecutions = frames.GroupBy(f => f.methodId).ToList();
    foreach (var methodExecution in methodExecutions)
    {
        SetUpSequentialSteps(methodExecution);
    }

    for (var i = 0; i < frames.Count; i++)
    {
        if (i + 1 < frames.Count)
        {
            if (frames[i].methodId != frames[i + 1].methodId)
            {
                frames[i].frame["childFrameID"] = frames[i + 1].snapshotId;
            }
    
            // foreach (var ff in frames)
            // {
            //     if (ff.methodId == frames[i + 1].methodId)
            //     {
            //         ff.frame["parentFrameID"] = frames[i].snapshotId;
            //     }
            // }
        }
    }

    for (var index = 0; index < frames.Count; index++)
    {
        var frame = frames[index];
        o["frames"][frame.snapshotId] = frame.frame;
        if (index + 1 < frames.Count && frame.frame["nextFrameID"] == null)
        {
            frame.frame["nextFrameID"] = frames[index + 1].snapshotId;
        }
    }

    return o;
}

CreateTimeTravelJson(@"C:\Users\Omer Raviv\AppData\Local\Temp\snapshots\15792");

void SetUpSequentialSteps(IGrouping<string, (string snapshotId, string methodId, ulong timestamp, JObject frame)> valueTuples)
{
    var linesWithinMethod = valueTuples.OrderBy(l => l.timestamp).ToList();
    for (var index = 0; index < linesWithinMethod.Count; index++)
    {
        var frame = linesWithinMethod[index];
        if (index + 1 < linesWithinMethod.Count)
        {
            frame.frame["nextFrameID"] = linesWithinMethod[index + 1].snapshotId;
        }

        if (index - 1 >= 0)
        {
            frame.frame["prevFrameID"] = linesWithinMethod[index - 1].snapshotId;
        }
    }
}