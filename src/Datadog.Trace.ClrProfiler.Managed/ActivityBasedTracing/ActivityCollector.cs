using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable SA1124 // Do not use regions
#pragma warning disable SA1201 // Elements must appear in the correct order
#pragma warning disable SA1214 // Readonly fields must appear before non-readonly fields
namespace Datadog.Trace.ClrProfiler
{
    internal class ActivityCollector : IDisposable
    {
        #region Static APIs

        private static readonly TimeSpan CompletedTracesBufferSendIntervalMin = TimeSpan.FromMilliseconds(1);

        private static ActivityCollector _defaultActivityCollector;

        static ActivityCollector()
        {
            var config = new ActivityCollectorConfiguration();
            _defaultActivityCollector = new ActivityCollector(config);
        }

        public static ActivityCollector Default
        {
            get { return _defaultActivityCollector; }
        }

        public static void SetDefault(ActivityCollector newDefault, out ActivityCollector previousDefault)
        {
            Validate.NotNull(newDefault, nameof(newDefault));

            previousDefault = _defaultActivityCollector;
            _defaultActivityCollector = newDefault;
        }

        #endregion Static APIs

        private readonly ActivityCollectorConfiguration _config;
        private readonly ActivitySource _defaultActivitySource;

        private readonly TraceCache _activeTraces;
        private GrowingCollection<TraceActivitiesContainer> _completedTraces;
        private GrowingCollection<Activity> _completedActivities;

        private IActivityExporter _traceExporter;
        private ActivityListener _activityListenerHandle;

        private ManualResetEventSlim _sendLoopSignal;
        private readonly Thread _sendLoopThread;
        private bool _isSendLoopRunning;
        private bool _isSendLoopStopRequested;

        public ActivityCollector(ActivityCollectorConfiguration config)
        {
            Validate.NotNull(config, nameof(config));

            config.SetReadOnly();

            _config = config;
            _defaultActivitySource = new ActivitySource(_config.ActivitySourceName, _config.ActivitySourceVersion);

            if (_config.AggregateActivitiesIntoTraces)
            {
                _activeTraces = new TraceCache();
                _completedTraces = new GrowingCollection<TraceActivitiesContainer>();
                _completedActivities = null;
            }
            else
            {
                _activeTraces = null;
                _completedTraces = null;
                _completedActivities = new GrowingCollection<Activity>();
            }

            _traceExporter = CreateTraceExporter();

            _sendLoopThread = CreateAndStartSendLoopThread();
            ConfigureActivityListening();
        }

        public ActivitySource ActivitySource
        {
            get { return _defaultActivitySource; }
        }

        public Activity StartActivity(string name)
        {
            return _defaultActivitySource.StartActivity(name);
        }

        public Activity StartActivity(string name, ActivityKind kind)
        {
            return _defaultActivitySource.StartActivity(name, kind);
        }

        public void LogState()
        {
            // We call this method from Instrumentation.Initialize() to ensure that the default ActivityCollector is initialized.
            // Use this oportunity to write ActivityCollector and IActivityExporter settings to log.
            // @ToDo!
        }

        private IActivityExporter CreateTraceExporter()
        {
            IActivityExporter traceExporter = _config.TraceExporterFactory(_config);

            if (traceExporter == null)
            {
                throw new InvalidOperationException($"Invalid {nameof(ActivityCollectorConfiguration)}:"
                                                  + $" The specified {nameof(ActivityCollectorConfiguration.TraceExporterFactory)} created a null {nameof(IActivityExporter)}.");
            }

            if (_config.AggregateActivitiesIntoTraces && !traceExporter.IsSendTracesSupported)
            {
                throw new InvalidOperationException($"Invalid {nameof(ActivityCollectorConfiguration)}:"
                                                  + $" The {nameof(ActivityCollectorConfiguration.AggregateActivitiesIntoTraces)} setting is True."
                                                  + $" However, the specified {nameof(ActivityCollectorConfiguration.TraceExporterFactory)} created a"
                                                  + $" {nameof(IActivityExporter)} with {nameof(IActivityExporter.IsSendTracesSupported)} == False.");
            }

            if (!_config.AggregateActivitiesIntoTraces && !traceExporter.IsSendActivitiesSupported)
            {
                throw new InvalidOperationException($"Invalid {nameof(ActivityCollectorConfiguration)}:"
                                                  + $" The {nameof(ActivityCollectorConfiguration.AggregateActivitiesIntoTraces)} setting is False."
                                                  + $" However, the specified {nameof(ActivityCollectorConfiguration.TraceExporterFactory)} created a"
                                                  + $" {nameof(IActivityExporter)} with {nameof(IActivityExporter.IsSendActivitiesSupported)} == False.");
            }

            return traceExporter;
        }

        private Thread CreateAndStartSendLoopThread()
        {
            // We create a new thread rather than using the thread pool.
            // This is because inside of the main loop in the TracesSendLoop() method we use a synchronous wait.
            // The reason for that is to prevent sending from being affected by potential thread pool starvation.
            // As a result, TracesSendLoop() is a very long running task that occupies a thread forever.
            // If we were to schedule TracesSendLoop() on the thread pool it would be possible that the thread chosen by the
            // pool had run user code before. Such user code may be doing an asynchronous wait scheduled to
            // continue on the same thread (e.g. this can occur when using a custom synchronization context or a
            // custom task scheduler). If such case the waiting user code will never continue.
            // By creating our own thread, we guarantee no interactions with potentially incorrectly written async user code.

            _isSendLoopStopRequested = false;
            _sendLoopSignal = new ManualResetEventSlim();

            Thread sendLoopThread = new Thread(this.TracesSendLoop);
            sendLoopThread.Name = this.GetType().Name + "." + nameof(TracesSendLoop) + "-" + _traceExporter.GetType().Name;
            sendLoopThread.IsBackground = true;

            sendLoopThread.Start();
            return sendLoopThread;
        }

        private void ConfigureActivityListening()
        {
            _activityListenerHandle = new ActivityListener()
            {
                ActivityStarted = OnActivityStarted,
                ActivityStopped = OnActivityStopped,
                ShouldListenTo = (_) => true,
                // GetRequestedDataUsingParentId = null,
                // GetRequestedDataUsingContext = null,
            };

            ActivitySource.AddActivityListener(_activityListenerHandle);
        }

        private IReadOnlyCollection<TraceActivitiesContainer> GetResetCompletedTraces()
        {
            var newCompletedTracesBuffer = new GrowingCollection<TraceActivitiesContainer>();

            GrowingCollection<TraceActivitiesContainer> prevCompletedTracesBuffer = Interlocked.Exchange(ref _completedTraces, newCompletedTracesBuffer);
            return prevCompletedTracesBuffer;
        }

        private IReadOnlyCollection<Activity> GetResetCompletedActivities()
        {
            var newCompletedActivitiesBuffer = new GrowingCollection<Activity>();

            GrowingCollection<Activity> prevCompletedActivitiesBuffer = Interlocked.Exchange(ref _completedActivities, newCompletedActivitiesBuffer);
            return prevCompletedActivitiesBuffer;
        }

        private void OnActivityStarted(Activity activity)
        {
            if (activity == null)
            {
                return;
            }

            if (!_config.AggregateActivitiesIntoTraces)
            {
                return;
            }

            GetLocalTraceInfo(activity, out bool isLocalRootActivity, out ulong localRootId);

            if (isLocalRootActivity)
            {
                var trace = new TraceActivitiesContainer(localRootId, activity);
                bool isNewTrace = _activeTraces.TryCreate(localRootId, trace);

                if (!isNewTrace)
                {
                    // We should log this properly but in the prototype, we just throw.
                    throw new Exception($"Activity '{activity.Id}' started. It is a local root, but a trace for this activity is already in-flight.");
                }
            }
            else
            {
                bool isExistingTrace = _activeTraces.TryGet(localRootId, out TraceActivitiesContainer trace);

                if (!isExistingTrace)
                {
                    // We should log this properly but in the prototype, we just throw.
                    throw new Exception($"Activity '{activity.Id}' started. It is NOT a local root; a trace for its parent chain should be in-flight, but it cannot be found.");
                }
                else
                {
                    trace.Add(activity);
                }
            }
        }

        private void OnActivityStopped(Activity activity)
        {
            if (activity == null)
            {
                return;
            }

            if (_config.AggregateActivitiesIntoTraces)
            {
                _completedActivities.Add(activity);

                if (_completedActivities.Count >= _config.CompletedItemsBufferMaxSize)
                {
                    _sendLoopSignal.Set();
                }
            }
            else
            {
                GetLocalTraceInfo(activity, out bool isLocalRootActivity, out ulong localRootId);

                if (isLocalRootActivity)
                {
                    bool isExistingTrace = _activeTraces.TryRemove(localRootId, out TraceActivitiesContainer trace);

                    if (!isExistingTrace)
                    {
                        // We should log this properly but in the prototype, we just throw.
                        throw new Exception($"Activity '{activity.Id}' stopped. It is a local root; a trace for its parent chain should be in-flight, but it cannot be found.");
                    }
                    else
                    {
                        _completedTraces.Add(trace);

                        if (_completedTraces.Count >= _config.CompletedItemsBufferMaxSize)
                        {
                            _sendLoopSignal.Set();
                        }
                    }
                }
            }
        }

        private void GetLocalTraceInfo(Activity activity, out bool isLocalRootActivity, out ulong localRootId)
        {
            isLocalRootActivity = (activity.Parent == null);

            Activity localRootActivity = activity;
            while (localRootActivity.Parent != null)
            {
                localRootActivity = localRootActivity.Parent;
            }

            if (localRootActivity.IdFormat == ActivityIdFormat.W3C)
            {
                ActivitySpanId rootSpanId = localRootActivity.SpanId;
                localRootId = rootSpanId.ToUInt64();
            }
            else
            {
                string rootSpanId = localRootActivity.Id;
                localRootId = Hash.ComputeFastStableUInt64(rootSpanId);
            }
        }

        private void TracesSendLoop()
        {
            _isSendLoopRunning = true;

            DateTimeOffset nextSendTargetTime = DateTimeOffset.Now + _config.CompletedItemsBufferSendInterval;

            while (!_isSendLoopStopRequested)
            {
                try
                {
                    WaitForNextTracesSend(nextSendTargetTime);

                    if (_config.AggregateActivitiesIntoTraces)
                    {
                        IReadOnlyCollection<TraceActivitiesContainer> completedTraces = GetResetCompletedTraces();
                        _traceExporter.SendTraces(completedTraces);
                    }
                    else
                    {
                        IReadOnlyCollection<Activity> completedActivities = GetResetCompletedActivities();
                        _traceExporter.SendActivities(completedActivities);
                    }

                    nextSendTargetTime = DateTimeOffset.Now + _config.CompletedItemsBufferSendInterval;
                }
                catch (Exception ex)
                {
                    // Need to log this properly. For now just rethrow.
                    ExceptionDispatchInfo.Capture(ex).Throw();
                }
            }

            _isSendLoopRunning = false;
        }

        private void WaitForNextTracesSend(DateTimeOffset nextSendTargetTime)
        {
            while (true)
            {
                TimeSpan remainingWaitInterval = nextSendTargetTime - DateTimeOffset.Now;
                if (remainingWaitInterval < CompletedTracesBufferSendIntervalMin)
                {
                    remainingWaitInterval = CompletedTracesBufferSendIntervalMin;
                }

                _sendLoopSignal.Wait(remainingWaitInterval);

                if (_completedTraces.Count >= _config.CompletedItemsBufferMaxSize
                    || DateTimeOffset.Now >= nextSendTargetTime
                    || _isSendLoopStopRequested)
                {
                    return;
                }
            }
        }

        private void ShutDownSendLoop()
        {
            _isSendLoopStopRequested = true;

            if (_isSendLoopRunning == false)
            {
                return;
            }

            _sendLoopSignal.Set();
            Thread.Yield();

            if (_isSendLoopRunning == false)
            {
                return;
            }

            int[] waits = new int[] { 1, 10, 20, 50, 500 };
            int w = 0;

            while (_isSendLoopRunning)
            {
                Thread.Sleep(waits[w]);

                if (++w >= waits.Length)
                {
                    w = 0;
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // We need to signal the send loop to exit and then wait for it, before we can dispose.
                ShutDownSendLoop();

                // Dispose managed state:

                ManualResetEventSlim sendLoopSignal = _sendLoopSignal;
                if (sendLoopSignal != null)
                {
                    sendLoopSignal.Dispose();
                    _sendLoopSignal = null;
                }

                ActivityListener activityListenerHandle = _activityListenerHandle;
                if (activityListenerHandle != null)
                {
                    activityListenerHandle.Dispose();
                    _activityListenerHandle = null;
                }

                IActivityExporter traceExporter = _traceExporter;
                if (traceExporter != null)
                {
                    traceExporter.Dispose();
                    _traceExporter = null;
                }
            }

            // Free unmanaged resources and override finalizer
            // Set large fields to null
        }

        // Uncomment/Override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ActivityCollector()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The normal Dispose methos can take a long time, becasue it waits for the send loop to shut down.
        /// Call DisposeAsync, in order to perform the wait on the threadpool instead of the current thread.
        /// </summary>
        /// <returns>A task representing the completion of the Dispose.</returns>
        public Task DisposeAsync()
        {
            return Task.Run(() =>
                            {
                                try
                                {
                                    Dispose();
                                }
                                catch (Exception)
                                {
                                    // log!
                                }
                            });
        }
    }
}
