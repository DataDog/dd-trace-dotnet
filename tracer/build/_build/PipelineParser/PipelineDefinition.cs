using System;

class PipelineDefinition
{
    public TriggerDefinition Trigger { get; set; }
    public TriggerDefinition Pr { get; set; }
    public StageDefinition[] Stages { get; set; } = Array.Empty<StageDefinition>();

    public class TriggerDefinition
    {
        public PathDefinition Paths { get; set; }
    }

    public class PathDefinition
    {
        public string[] Exclude { get; set; } = Array.Empty<string>();
    }

    public class StageDefinition
    {
        public string Stage { get; set; }
        public string[] DependsOn { get; set; }
    }
}
