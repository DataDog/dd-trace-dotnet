using Quartz;

namespace QuartzSampleApp.Infrastructure;

public static class SchedulerHolder
{
    public static IScheduler Scheduler { get; set; } = default!;
}
