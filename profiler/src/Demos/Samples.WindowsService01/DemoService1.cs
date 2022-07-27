// <copyright file="DemoService1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.WindowsService01
{
    public partial class DemoService1 : ServiceBase
    {
        private Computer _computer;
        private Task _computerTask;

        public DemoService1()
        {
            InitializeComponent();

            CanStop = true;                     // (default is True)
            CanShutdown = true;                 // (default is False)
            CanPauseAndContinue = true;         // (default is False)
            CanHandleSessionChangeEvent = true; // (default is False)
            CanHandlePowerEvent = true;         // (default is False)
            AutoLog = true;                     // (default is True)

            ServiceName = "Datadog_Demos_WindowsService01";

            _computer = new Computer();
            _computerTask = null;
        }

        public void CallStart(string[] args)
        {
            OnStart(args);
        }

        public void CallStop()
        {
            OnStop();
        }

        //
        // Summary:
        //     When implemented in a derived class, System.ServiceProcess.ServiceBase.OnContinue
        //     runs when a Continue command is sent to the service by the Service Control Manager
        //     (SCM). Specifies actions to take when a service resumes normal functioning after
        //     being paused.
        protected override void OnContinue()
        {
            _computer.Resume();

            base.OnContinue();
        }

        //
        // Summary:
        //     When implemented in a derived class, executes when a Pause command is sent to
        //     the service by the Service Control Manager (SCM). Specifies actions to take when
        //     a service pauses.
        protected override void OnPause()
        {
            _computer.Pause();

            base.OnPause();
        }

        //
        // Summary:
        //     When implemented in a derived class, executes when the system is shutting down.
        //     Specifies what should occur immediately prior to the system shutting down.
        protected override void OnShutdown()
        {
            _computer.Stop();
            WaitForComputerTask();

            base.OnShutdown();
        }

        //
        // Summary:
        //     When implemented in a derived class, executes when a Start command is sent to
        //     the service by the Service Control Manager (SCM) or when the operating system
        //     starts (for a service that starts automatically). Specifies actions to take when
        //     the service starts.
        //
        // Parameters:
        //   args:
        //     Data passed by the start command.
        protected override void OnStart(string[] args)
        {
            _computerTask = _computer.Run();

            base.OnStart(args);
        }

        //
        // Summary:
        //     When implemented in a derived class, executes when a Stop command is sent to
        //     the service by the Service Control Manager (SCM). Specifies actions to take when
        //     a service stops running.
        protected override void OnStop()
        {
            _computer.Stop();
            WaitForComputerTask();

            base.OnStop();
        }

        private void WaitForComputerTask()
        {
            Task computerTask = Interlocked.Exchange(ref _computerTask, null);
            if (computerTask != null)
            {
                computerTask.Wait();
            }
        }
    }
}
