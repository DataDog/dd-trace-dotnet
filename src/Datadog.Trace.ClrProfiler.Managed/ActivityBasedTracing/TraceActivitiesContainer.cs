using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Datadog.Trace.ClrProfiler
{
    internal struct TraceActivitiesContainer
    {
        private readonly List<Activity> _activities;
        private readonly Activity _root;
        private readonly ulong _key;

        public TraceActivitiesContainer(ulong key, Activity rootActivity)
        {
            Validate.NotNull(rootActivity, nameof(rootActivity));

            _root = rootActivity;
            _key = key;

            _activities = new List<Activity>();
            _activities.Add(rootActivity);
        }

        public IReadOnlyCollection<Activity> Activities
        {
            get { return _activities; }
        }

        public Activity Root
        {
            get { return _root; }
        }

        public ulong Key
        {
            get { return _key; }
        }

        public void Add(Activity activity)
        {
            Validate.NotNull(activity, nameof(activity));
            _activities.Add(activity);
        }
    }
}
