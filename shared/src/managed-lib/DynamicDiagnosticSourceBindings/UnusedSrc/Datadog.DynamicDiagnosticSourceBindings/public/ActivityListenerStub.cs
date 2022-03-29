using System;
using Datadog.Util;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    public class ActivityListenerStub : IDisposable
    {
        /// <summary>
        /// When we decide to support sampling before an Activity is creted, the ActivityListenerStub.CreateAndStart(..)
        /// method will need to accept delegates for the underlying SampleXxx(..) methods of the ActivityListener
        /// (such delegates will need to accep respective stubs).
        /// Similar for ShouldListenTo.
        /// For now we will use hard-coded handlers that accept all activities.
        /// </summary>
        /// <param name="activityStartedHandler"></param>
        /// <param name="activityStoppedHandler"></param>
        /// <param name=""></param>
        public static ActivityListenerStub CreateAndStart(Action<ActivityStub> activityStartedHandler, Action<ActivityStub> activityStoppedHandler) 
        {
            var listener = new ActivityListenerStub(activityStartedHandler, activityStoppedHandler);

            //ActivitySourceStub.AddActivityListener(listener);

            return listener;
        }

        private ActivityListenerStub(Action<ActivityStub> activityStartedHandler, Action<ActivityStub> activityStoppedHandler)
        {
            this.ActivityStarted = activityStartedHandler;
            this.ActivityStopped = activityStoppedHandler;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }


        public Action<ActivityStub> ActivityStarted { get; }
        public Action<ActivityStub> ActivityStopped { get; }
    }
}
