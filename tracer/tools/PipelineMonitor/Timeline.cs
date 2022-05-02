namespace PipelineMonitor
{
    public class TimelineData
    {
        public string LastChangedBy { get; set; }
        public DateTime LastChangedOn { get; set; }
        public Guid Id { get; set; }
        public int ChangeId { get; set; }
        public Uri Url { get; set; }
        public List<Record> Records { get; set; }
    }

    public class Record
    {
        private object PreviousAttempts { get; set; }
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }
        public string CurrentOperation { get; set; }
        public int? PercentComplete { get; set; }
        public string State { get; set; }
        public string Result { get; set; }
        public string ResultCode { get; set; }
        public int ChangeId { get; set; }
        public DateTime? LastModified { get; set; }
        public string WorkerName { get; set; }
        public string Details { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public string Url { get; set; }
        public Log Log { get; set; }
        public Task Task { get; set; }
        public int Attempt { get; set; }
        public string Identifier { get; set; }

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
        public int Id { get; set; }
        public string Type { get; set; }
        public Uri Url { get; set; }
    }

    public class Task
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
    }

}
