using System;
using System.Threading;

namespace Datadog.Logging.Composition
{
    /// <summary>
    /// We encapsulate a cross-process Mutex with out-of-proc stalling-detection logic:
    /// If it takes too long to aquire the mutex, we assume that another process holding it is hanging and give up.
    /// Instead, we construct another global Mutex and use it instead.
    /// As a result we avoid hanging for more than some number of seconds if another process takes over the log mutex.
    /// This happens at the cost of possibe log corruption.
    /// 
    /// Note that it is possible that while one in-proc thread gives up and creates an alternative global mutex,
    /// another in-proc is successful in aquiring the older mutex. To prevent this, we use an in-proc semaphore to control 
    /// access to the mutex. As a result, only one in-proc thread can hold any mutext encapsulated by an instance of this class.
    /// 
    /// !!!! @ToDo: Testing Required !!!!
    /// </summary>
    internal sealed class LogGroupMutex : IDisposable
    {
        private const int WaitForMutexMillis = 1000;            // 0.5 sec
        private const int IterationTimeoutMillis = 7000;        // 7 sec
        private const int MaxPauseBetweenWaitsMillis = 250;     // 300 msecs
        private const int ImmediateIterationsBeforeGiveUp = 3;  // 7 x 3 = total 21 secs of blocking (+ contention on _mutexProtector with other threads) 

        //private readonly object _iterationUpdateLock = new object();
        private readonly SemaphoreSlim _mutexProtector = new SemaphoreSlim(1);
        private readonly Guid _logGroupId;
        private int _iteration;
        private string _mutexName;
        private Mutex _mutex;

        #region struct LogGroupMutex.Handle
        public struct Handle : IDisposable
        {
            internal static readonly Handle InvalidInstance = new Handle(null, null);

            private Mutex _acquiredMutex;
            private SemaphoreSlim _mutexProtector;

            internal Handle(Mutex acquiredMutex, SemaphoreSlim mutexProtector)
            {
                if ((acquiredMutex == null && mutexProtector != null) || (acquiredMutex != null && mutexProtector == null))
                {
                    throw new ArgumentException($"{nameof(acquiredMutex)} and {nameof(mutexProtector)} must either both be null, or both be non-null.");
                }

                _acquiredMutex = acquiredMutex;
                _mutexProtector = mutexProtector;
            }

            public bool IsValid { get { return (_acquiredMutex != null && _mutexProtector != null); } }

            public void Dispose()
            {
                try
                {
                    ReleaseMutex();
                }
                finally
                {
                    _acquiredMutex = null;
                    ReleaseMutexProtector();
                }
            }

            private void ReleaseMutex()
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

            private void ReleaseMutexProtector()
            {
                SemaphoreSlim mutexProtector = Interlocked.Exchange(ref _mutexProtector, null);
                if (mutexProtector != null)
                {
                    try
                    {
                        mutexProtector.Release();
                    }
                    catch (ObjectDisposedException)
                    { }
                }
            }
        }
        #endregion struct LogGroupMutex.Handle

        public LogGroupMutex(Guid logGroupId)
        {
            _logGroupId = logGroupId;
            _iteration = 0;
            _mutexName = ConstructMutextName(_logGroupId, _iteration);
            _mutex = new Mutex(initiallyOwned: false, _mutexName);
        }

        public Guid LogGroupId
        {
            get { return _logGroupId; }
        }

        public string CurrentMutextName
        {
            get { return _mutexName; }
        }

        public int CurrentIteration
        {
            get { return Interlocked.Add(ref _iteration, 0); }
        }

        public bool TryAcquire(out LogGroupMutex.Handle acquiredMutex)
        {
            if (TryAcquireIteration(out acquiredMutex))
            {
                return true;
            }

            for (int immediateIteration = 1; immediateIteration < ImmediateIterationsBeforeGiveUp; immediateIteration++)
            {
                if (TryAcquireIteration(out acquiredMutex))
                {
                    return true;
                }

                if (IsDisposed())
                {
                    acquiredMutex = Handle.InvalidInstance;
                    return false;
                }
            }

            acquiredMutex = Handle.InvalidInstance;
            return false;
        }

        public bool IsDisposed()
        {
            return (CurrentIteration < 0);
        }

        public void Dispose()
        {
            if (IsDisposed())
            {
                return;
            }

            try
            {
                _mutexProtector.Wait();
                try
                {
                    if (IsDisposed())
                    {
                        return;
                    }

                    SafeDisposeAndSetToNull(ref _mutex);
                    _mutexName = String.Empty;
                    Interlocked.Exchange(ref _iteration, -1);
                }
                finally
                {
                    _mutexProtector.Release();
                }

                _mutexProtector.Dispose();
            }
            catch
            { }
        }

        private static string ConstructMutextName(Guid groupId, int iteration)
        {
            string groupIdString = groupId.ToString("D");
            return $"Global\\Datadog-FileLigSink-{groupIdString}-{iteration}";
        }

        private static bool TryAcquireCore(Mutex mutex, SemaphoreSlim mutexProtector, ref LogGroupMutex.Handle handle)
        {
            try
            {
                if (mutex.WaitOne(WaitForMutexMillis))
                {
                    handle = new LogGroupMutex.Handle(mutex, mutexProtector);
                    return true;
                }
            }
            catch
            { }

            return false;
        }

        private bool TryAcquireIteration(out LogGroupMutex.Handle acquiredMutex)
        {
            acquiredMutex = Handle.InvalidInstance;

            try
            {
                if (IsDisposed())
                {
                    return false;
                }

                _mutexProtector.Wait();
                try
                {
                    if (IsDisposed())
                    {
                        return false;
                    }

                    int startMillis = Environment.TickCount & Int32.MaxValue;
                    int passedMillis = 0;
                    Random rnd = null;
                    while (passedMillis < IterationTimeoutMillis)
                    {
                        if (TryAcquireCore(_mutex, _mutexProtector, ref acquiredMutex))
                        {
                            return true;
                        }

                        if (rnd == null)
                        {
                            rnd = new Random();
                        }

                        Thread.Sleep(rnd.Next(MaxPauseBetweenWaitsMillis));

                        int now = Environment.TickCount & Int32.MaxValue;
                        passedMillis = Math.Abs(now - startMillis);
                    }

                    // Ok, somebody in a different process is holding the mutex super long.
                    // Perhaps they died. We can no longer block on them.
                    // Create a new mutex with a new name.

                    IncMutexIteration();

                    // Try acquiring the new mutex:
                    if (TryAcquireCore(_mutex, _mutexProtector, ref acquiredMutex))
                    {
                        return true;
                    }
                }
                finally
                {
                    if (!acquiredMutex.IsValid)
                    {
                        _mutexProtector.Release();
                    }
                }
            }
            catch
            { }

            return false;
        }

        private void IncMutexIteration()
        {
            // This method MUST be only called while under  the _mutexProtector's lock!

            SafeDisposeAndSetToNull(ref _mutex);
            int newIteration = Interlocked.Increment(ref _iteration);
            _mutexName = ConstructMutextName(_logGroupId, newIteration);
            _mutex = new Mutex(initiallyOwned: false, _mutexName);
        }

        private static bool SafeDisposeAndSetToNull<T>(ref T reference) where T : class, IDisposable
        {
            T referencedItem = Interlocked.Exchange(ref reference, null);
            if (referencedItem != null)
            {
                try
                {
                    referencedItem.Dispose();
                    return true;
                }
                catch
                { }
            }

            return false;
        }
    }
}
