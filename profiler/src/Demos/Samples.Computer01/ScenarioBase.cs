// <copyright file="ScenarioBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    // Scenario support Run and Start/Process/Stop modes.
    // By default, Run executes the same OnProcess code as Process.
    // The event in charge of stopping the processing can be checked or wait for.
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

        // by default, do the same thing in Run and Start/Stop
        public virtual void Run()
        {
            OnProcess();
        }

        public void Process()
        {
            OnProcess();
        }

        public abstract void OnProcess();

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
            // in Run situations, the event could be null
            if (_stopEvent == null)
            {
                return false;
            }

            return _stopEvent.WaitOne(0);
        }

        protected void WaitFor(TimeSpan duration)
        {
            _stopEvent.WaitOne(duration);
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
                                Process();
                            }
                        },
                        TaskCreationOptions.LongRunning));
            }

            return result;
        }
    }
}
