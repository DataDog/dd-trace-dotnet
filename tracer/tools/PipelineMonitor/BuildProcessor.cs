using Datadog.Trace;
using Datadog.Trace.ExtensionMethods;

namespace PipelineMonitor;

public class BuildProcessor
{
    private Build _build;
    private TimelineData _timeline;
    private TimeSpan _offset;
    private List<Record> _rootStages = new();
    private Dictionary<Guid, List<Record>> _recordsPerParentId = new();

    public BuildProcessor(Build build, TimelineData timeline)
    {
        _build = build;
        _timeline = timeline;
    }

    public void Process()
    {
        if (_build.Status != "completed")
            return;

        if (!_build.StartTime.HasValue || !_build.FinishTime.HasValue)
            return;

        var startTime = _build.StartTime;
        var endTime = _build.FinishTime;

        if (!startTime.HasValue || !endTime.HasValue)
        {
            Console.WriteLine("only finished spans will be taken into account");
            return;
        }

        CalculateOffset(endTime.Value);

        var settings = new SpanCreationSettings() { StartTime = startTime + _offset};

        using var scope = Tracer.Instance.StartActive("ci-run", settings);
        DecorateRootSpan(scope);

        GetRootStages(_timeline);
        foreach (var stage in _rootStages)
        {
            if (stage.Result != "skipped")
                SendTraces(scope, stage);
        }

        scope.Span.Finish(endTime.Value + _offset);
    }

    // avoid any issue at intake level, so move all the times to a more recent one
    void CalculateOffset(DateTime endTime)
    {
        _offset = DateTime.UtcNow - endTime - TimeSpan.FromSeconds(30);
    }

    void DecorateRootSpan(IScope scope)
    {
        scope.Span.ServiceName = _build.Definition.Name;
        scope.Span.ResourceName = GetResourceName();
        if (_build.Result != "succeeded")
            scope.Span.Error = true;

        scope.Span.SetTag("azdo.Id", _build.Id.ToString());
        scope.Span.SetTag("azdo.BuildNumber", _build.BuildNumber);
        scope.Span.SetTag("azdo.Status", _build.Status);
        scope.Span.SetTag("azdo.Result", _build.Result);
        scope.Span.SetTag("azdo.QueueTime", _build.QueueTime.ToString());
        scope.Span.SetTag("azdo.StartTime", _build.StartTime.ToString());
        scope.Span.SetTag("azdo.FinishTime", _build.FinishTime.ToString());
        scope.Span.SetTag("azdo.url.api", _build.Url);
        scope.Span.SetTag("azdo.url.web", _build._Links.Web.Href);
        scope.Span.SetTag("azdo.Pipeline", _build.Definition.Name);
        scope.Span.SetTag("azdo.SourceBranch", _build.SourceBranch);
        scope.Span.SetTag("azdo.SourceVersion", _build.SourceVersion);
        scope.Span.SetTag("azdo.Priority", _build.Priority);
        scope.Span.SetTag("azdo.Reason", _build.Reason);

        scope.Span.SetTag("pr.title", _build.TriggerInfo.PrTitle);
        scope.Span.SetTag("pr.number", _build.TriggerInfo.PrNumber);
        scope.Span.SetTag("pr.isfork", _build.TriggerInfo.PrIsFork);
        scope.Span.SetTag("pr.draft", _build.TriggerInfo.PrDraft);
        scope.Span.SetTag("pr.sender.name", _build.TriggerInfo.PrSenderName);
        scope.Span.SetTag("pr.sender.AvatarUrl", _build.TriggerInfo.PrSenderAvatarUrl);
        scope.Span.SetTraceSamplingPriority(SamplingPriority.UserKeep);
    }

    void GetRootStages(TimelineData buildData)
    {
        foreach (var record in buildData.Records)
        {
            if (record.ParentId is null)
            {
                _rootStages.Add(record);
            }
            else
            {
                if (!_recordsPerParentId.ContainsKey(record.ParentId.Value))
                    _recordsPerParentId.Add(record.ParentId.Value, new List<Record>());

                _recordsPerParentId[record.ParentId.Value].Add(record);
            }
        }

        _rootStages.Sort((@r1, @r2) => r1.StartTime < r2.StartTime ? -1 : 1);
    }

    void SendTraces(IScope parent, Record node, string serviceName = "")
    {
        var startTime = node.StartTime + _offset;
        var endTime = node.FinishTime + _offset;

        if (!startTime.HasValue || !endTime.HasValue)
        {
            Console.WriteLine("only finished spans will be taken into account");
            return;
        }

        var settings = new SpanCreationSettings() { Parent = parent.Span.Context, StartTime = startTime };

        using var scope = Tracer.Instance.StartActive(node.Name, settings);
        if (node.IsStage)
            serviceName = node.Name;

        if (!string.IsNullOrEmpty(serviceName))
            scope.Span.ServiceName = serviceName;

        scope.Span.ResourceName = GetResourceName();

        scope.Span.SetTag("azdo.type", node.Type);
        scope.Span.SetTag("azdo.name", node.Name);
        scope.Span.SetTag("azdo.state", node.State);
        scope.Span.SetTag("azdo.percentcomplete", node.PercentComplete.ToString());
        scope.Span.SetTag("azdo.workername", node.WorkerName);
        scope.Span.SetTag("azdo.errorcount", node.ErrorCount.ToString());
        scope.Span.SetTag("azdo.logurl", node.Log?.Url?.ToString());
        scope.Span.SetTag("azdo.atempt", node.Attempt.ToString());

        if (node.Result == "failed")
            scope.Span.Error = true;

        if (_recordsPerParentId.ContainsKey(node.Id))
        {
            var children = _recordsPerParentId[node.Id];
            children.Sort((@r1, @r2) => r1.StartTime < r2.StartTime ? -1 : 1);

            foreach (var childRecord in children)
            {
                if (childRecord.ShouldBeTraced)
                    SendTraces(scope, childRecord, serviceName);
            }
        }

        scope.Span.Finish(endTime.Value);
    }

    private string GetResourceName()
    {   
        return _build.SourceBranch;
    }

}
