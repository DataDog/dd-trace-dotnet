using System.Diagnostics;

namespace ActivitySampleHelper
{
    public class ActivitySourceHelper
    {
        public ActivitySourceHelper(string sampleName)
        {
            ActivitySource = new ActivitySource(sampleName);
            var activityListener = new ActivityListener
            {
                ActivityStarted = activity => Console.WriteLine($"{activity.DisplayName}:{activity.Id} - Started"),
                ActivityStopped = activity => Console.WriteLine($"{activity.DisplayName}:{activity.Id} - Stopped"),
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
            };

            ActivitySource.AddActivityListener(activityListener);
        }

        public ActivitySource ActivitySource { get; }

        public IDisposable CreateScope(string operationName)
        {
            var activity = ActivitySource.StartActivity(operationName);
            if (activity == null)
            {
                throw new Exception($"Failed to start Activity for {operationName}");
            }
            return activity;
        }

        public void TrySetTag(IDisposable scope, string key, string value)
        {
            if(scope is Activity activity)
            {
                activity.SetTag(key, value);
            }
        }

        public ActivityTraceId GetTraceId(IDisposable scope)
        {
            if (scope is Activity activity)
            {
                return activity.TraceId;
            }

            throw new Exception("scope wasn't an Activity, can't get TraceId");
        }

        public ActivitySpanId GetSpanId(IDisposable scope)
        {
            if (scope is Activity activity)
            {
                return activity.SpanId;
            }

            throw new Exception("scope wasn't an Activity, can't get SpanId");
        }
    }
}
