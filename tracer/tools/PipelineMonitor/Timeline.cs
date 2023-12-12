namespace PipelineMonitor
{
    public class TimelineData
    {
        public string LastChangedBy { get; set; } = null!;
        public DateTime LastChangedOn { get; set; }
        public Guid Id { get; set; }
        public int ChangeId { get; set; }
        public Uri Url { get; set; } = null!;
        public List<Record> Records { get; set; } = null!;
    }

    public class Record
    {
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }
        public string Type { get; set; } = null!;
        public string Name { get; set; } = null!;
        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }
        public int? PercentComplete { get; set; }
        public string State { get; set; } = null!;
        public string Result { get; set; } = null!;
        public string WorkerName { get; set; } = null!;
        public int ErrorCount { get; set; }
        public Log Log { get; set; } = null!;
        public int Attempt { get; set; }

        public bool IsStage
        {
            get => Type == "Stage";
        }

        public bool ShouldBeTraced
        {
            get => Result != "skipped" && Type != "Checkpoint";
        }
    }

    public class Log
    {
        public Uri Url { get; set; } = null!;
    }
}
