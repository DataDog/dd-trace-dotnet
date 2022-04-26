namespace PipelineMonitor
{
    public class Build
    {
        public int Id;
        public string BuildNumber { get; set; }
        public string Status { get; set; }
        public string Result { get; set; }
        public DateTime? QueueTime { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? FinishTime { get; set; }
        public string Url { get; set; }
        public Definition Definition { get; set; }
        public string SourceBranch { get; set; }
        public string SourceVersion { get; set; }
        public string Priority { get; set; }
        public string Reason { get; set; }
        public Links _Links { get; set; }

        public TriggerInfo TriggerInfo { get; set; }
    }

    public class TriggerInfo
    {
        public string PrSourceBranch { get; set; }
        public string PrSourceSha { get; set; }
        public string PrId { get; set; }
        public string PrTitle { get; set; }
        public string PrNumber { get; set; }
        public string PrIsFork { get; set; }
        public string PrDraft { get; set; }
        public string PrSenderName { get; set; }
        public string PrSenderAvatarUrl { get; set; }
        public string PrSenderIsExternal { get; set; }
        public string PrAutoCancel { get; set; }
    }

    public class Links
    {
        public Link Timeline { get; set; }
        public Link Web { get; set; }
    }

    public class Link
    {
        public string Href { get; set; }
    }
    public class Definition
    {
        public string Name { get; set; }
    }
}
