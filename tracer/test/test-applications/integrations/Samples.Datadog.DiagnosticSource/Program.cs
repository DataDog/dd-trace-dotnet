using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Datadog.DiagnosticSource;

namespace Samples.Datadog.DiagnosticSource // Note: actual namespace depends on the project name.
{
    public static class Program
    {
        private static ActivitySource _source;

        public static async Task Main(string[] args)
        {
            _source = new ActivitySource("Samples.Datadog.DiagnosticSource");

            var listener = new ActivityListener
            {
                ActivityStopped = activity => PrintActivityStoppedInfo(activity),
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
            };

            ActivitySource.AddActivityListener(listener);

            // run through the extension methods

            // Set the Trace Sampling Priority (rejit needed to get access to TraceContext sampling priority)

            using (var rootActivity = _source.StartActivity("RootActivity"))
            {
                using (var childActivity = _source.StartActivity("ChildActivity"))
                {
                    // this should set the trace sampling priority on rootActivity
                    childActivity.SetTraceSamplingPriority(SamplingPriority.UserKeep);
                }
            }

            // Set the user details on the Root Activity (rejit needed to get access to TraceContext.Tags if there is a TraceContext)
            using (var setUserActivity = _source.StartActivity("SetUserActivity"))
            {
                setUserActivity.SetUser("12345");
            }



            // Set an exception on the Activity (no-rejit needed)
            using (var exceptionActivity = _source.StartActivity("ExceptionActivity"))
            {
                exceptionActivity.SetException(new ArgumentNullException("parameterName"));
            }


            // Get the currently active Span as an Activity - needs rejit to convert Span to Activity
            var activeSpan = ActivityExtensions.GetCurrentlyActiveActivity();

        }

        private static void PrintActivityStoppedInfo(Activity activity)
        {
            if (activity is null)
            {
                Console.WriteLine("ERROR: activity was null");
                return;
            }

            Console.Write("\n*****\n");
            Console.WriteLine($"Activity.DisplayName: {activity.DisplayName} Stopped");
            Console.WriteLine($"Activity.Id: {activity.Id}");
            Console.WriteLine($"Activity.SpanId: {activity.SpanId}");
            Console.WriteLine($"Activity.ParentId: {activity.ParentId}");
            Console.WriteLine($"Activity.Kind: {activity.Kind}");
            Console.WriteLine($"Activity.Status: {activity.Status}");
            Console.WriteLine($"Activity.StatusDescription: {activity.StatusDescription}");
            Console.WriteLine($"Activity.TraceStateString: {activity.TraceStateString}");
            Console.WriteLine($"Activity.Source.Name: {activity.Source.Name}");
            Console.WriteLine("Tags:");
            foreach (var tag in activity.TagObjects)
            {
                Console.WriteLine($"{tag.Key}: {tag.Value}");
            }
        }
    }
}
