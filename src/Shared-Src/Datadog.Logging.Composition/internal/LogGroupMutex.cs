using System;
using System.Threading;

namespace Datadog.Logging.Composition
{
    internal sealed class LogGroupMutex : IDisposable
    {
        private const int WaitMillis = 500;  // 0.5 sec
        private const int IterationTimeoutMillis = 7000;  // 7 sec
        private const int ImmediateIterationsBeforeGiveUp = 3;  // total 21 sec of blocking

        private readonly object _iterationUpdateLock = new object();
        private readonly Guid _groupId;
        private int _iteration;
        private string _mutexName;
        private Mutex _mutex;

        public LogGroupMutex(Guid groupId)
        {
            _groupId = groupId;
            _iteration = 0;
            _mutexName = ConstructMutextName(_groupId, _iteration);
            _mutex = new Mutex(initiallyOwned: false, _mutexName);
        }

        public string CurrentMutextName
        {
            get { return _mutexName; }
        }

        public int CurrentIteration
        {
            get { return _iteration; }
        }

        public bool TryAcquire(out Mutex acquiredMutex)
        {
            try
            {
                Mutex mutex = _mutex;
                if (mutex != null)
                {
                    if (mutex.WaitOne(WaitMillis))
                    {
                        acquiredMutex = mutex;
                        return true;
                    }
                }
            }
            catch
            {
            }

            // We timed out or there was an exception.
            return TryAcquireSlow(out acquiredMutex);
        }

        public void Dispose()
        {
            if (_iteration < 0)
            {
                return;
            }

            lock (_iterationUpdateLock)
            {
                try
                {
                    Mutex mutex = _mutex;
                    if (mutex != null)
                    {
                        _mutex = null;
                        mutex.Dispose();
                    }

                    _mutexName = string.Empty;
                    _iteration = -1;
                }
                catch
                { }
            }
        }

        private static string ConstructMutextName(Guid groupId, int iteration)
        {
            return $"Global\\Datadog-FileLigSink-{groupId.ToString("D")}-{iteration}";
        }

        private bool TryAcquireSlow(out Mutex acquiredMutex)
        {
            lock (_iterationUpdateLock)
            {
                if (_iteration < 0)
                {
                    // Disposed!
                    acquiredMutex = null;
                    return false;
                }

                try
                {
                    Random rnd = null;
                    for (int immediateIteration = 0; immediateIteration < ImmediateIterationsBeforeGiveUp; immediateIteration++)
                    {
                        try
                        {
                            int startMillis = Environment.TickCount;
                            int passedMillis = 0;
                            while (passedMillis < IterationTimeoutMillis)
                            {
                                if (_mutex.WaitOne(WaitMillis))
                                {
                                    acquiredMutex = _mutex;
                                    return true;
                                }

                                rnd = rnd ?? new Random();
                                Thread.Sleep(rnd.Next(300));
                            }
                        }
                        catch
                        { }

                        // Ok, sombody is holding the lock super long.
                        // Perhaps they died. We can no longer block on them.
                        // Create a new mutex with a new name.

                        _mutex.Dispose();
                        _iteration++;
                        _mutexName = ConstructMutextName(_groupId, _iteration);
                        _mutex = new Mutex(initiallyOwned: false, _mutexName);
                    }
                }
                catch
                { }

                acquiredMutex = null;
                return false;
            }
        }
    }
}
