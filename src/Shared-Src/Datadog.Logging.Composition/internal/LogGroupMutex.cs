using System;
using System.Threading;

namespace Datadog.Logging.Composition
{
    /// <summary>
    /// We encapsulate an cross-process Mutext with the logis that it it takes too long to aquire it,
    /// we assume that another process using it is hanging and give it. Instead, we construct another global Mutex
    /// and use it instead. The benefit of this avoids hanging at the cost of possibe log corruption.
    /// 
    /// !!!! @ToDo: Testing Required !!!!
    /// </summary>
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

        public struct Handle : IDisposable
        {
            private Mutex _acquiredMutex;

            internal Handle(Mutex acquiredMutex)
            {
                _acquiredMutex = acquiredMutex;
            }

            public bool IsValid { get { return (_acquiredMutex != null); } }

            public void Dispose()
            {
                Mutex acquiredMutex = Interlocked.Exchange(ref _acquiredMutex, null);
                if (acquiredMutex != null)
                {
                    try
                    {
                        acquiredMutex.ReleaseMutex();
                    }
                    catch (ObjectDisposedException)
                    { }
                }
            }
        }

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

        public bool TryAcquire(out LogGroupMutex.Handle acquiredMutex)
        {
            try
            {
                Mutex mutex = _mutex;
                if (mutex != null)
                {
                    if (mutex.WaitOne(WaitMillis))
                    {
                        acquiredMutex = new LogGroupMutex.Handle(mutex);
                        return true;
                    }
                }
            }
            catch
            { }

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

        private bool TryAcquireSlow(out LogGroupMutex.Handle acquiredMutex)
        {
            lock (_iterationUpdateLock)
            {
                if (_iteration < 0)
                {
                    // Disposed!
                    acquiredMutex = new LogGroupMutex.Handle(_mutex);
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
                                    acquiredMutex = new LogGroupMutex.Handle(_mutex);
                                    return true;
                                }

                                rnd = rnd ?? new Random();
                                Thread.Sleep(rnd.Next(300));
                            }
                        }
                        catch
                        { }

                        // Ok, sombody in a different process is holding the lock super long.
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

                acquiredMutex = new LogGroupMutex.Handle(_mutex);
                return false;
            }
        }
    }
}
