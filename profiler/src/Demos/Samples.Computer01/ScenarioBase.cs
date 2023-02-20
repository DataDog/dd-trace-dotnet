using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    public abstract class ScenarioBase
    {
        private readonly int _nbThreads;
        private ManualResetEvent _stopEvent;
        private List<Task> _activeTasks;

        public ScenarioBase(int nbThreads = 1)
        {
            _nbThreads = nbThreads;
        }

        public void Start()
        {
            if (_stopEvent != null)
            {
                throw new InvalidOperationException("Already running...");
            }

            _stopEvent = new ManualResetEvent(false);
            _activeTasks = CreateThreads();
        }

        public void Run()
        {
            OnRun();
        }

        public abstract void OnRun();

        public void Stop()
        {
            if (_stopEvent == null)
            {
                throw new InvalidOperationException("Not running...");
            }

            _stopEvent.Set();

            Task.WhenAll(_activeTasks).Wait();

            _stopEvent.Dispose();
            _stopEvent = null;
            _activeTasks = null;
        }

        protected bool IsEventSet()
        {
            return _stopEvent.WaitOne(0);
        }

        private List<Task> CreateThreads()
        {
            var result = new List<Task>(_nbThreads);

            for (var i = 0; i < _nbThreads; i++)
            {
                result.Add(
                    Task.Factory.StartNew(
                        () =>
                        {
                            while (!IsEventSet())
                            {
                                OnRun();
                            }
                        },
                        TaskCreationOptions.LongRunning));
            }

            return result;
        }
    }
}
