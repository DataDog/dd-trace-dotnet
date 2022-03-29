// <copyright file="DemoService1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Datadog.TestUtil;

namespace Datadog.Demos.WindowsService01
{
    public partial class DemoService1 : ServiceBase
    {
        private const string VersionMoniker = "4";
        private const string LogSourceMoniker = nameof(DemoService1);

        private Computer _computer;
        private Task _computerTask;

        public DemoService1()
        {
            LogConfigurator.SetupLogger();
            Log.Info(Log.WithCallInfo(LogSourceMoniker), "Ctor invoked", nameof(VersionMoniker), VersionMoniker);

            Log.Info(Log.WithCallInfo(LogSourceMoniker), $"Multi-line {EnvironmentInfo.GetDescription()}");

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

            Log.Info(Log.WithCallInfo(LogSourceMoniker), "Ctor completed. Instance created.");
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
            Log.Info(Log.WithCallInfo(LogSourceMoniker), "OnContinue invoked. Resuming...");

            _computer.Resume();

            base.OnContinue();
            Log.Info(Log.WithCallInfo(LogSourceMoniker), "Resume completed. Exiting OnContinue.");
        }

        //
        // Summary:
        //     When implemented in a derived class, System.ServiceProcess.ServiceBase.OnCustomCommand(System.Int32)
        //     executes when the Service Control Manager (SCM) passes a custom command to the
        //     service. Specifies actions to take when a command with the specified parameter
        //     value occurs.
        //
        // Parameters:
        //   command:
        //     The command message sent to the service.
        protected override void OnCustomCommand(int command)
        {
            Log.Info(Log.WithCallInfo(LogSourceMoniker), "OnCustomCommand invoked", "command", command);
            base.OnCustomCommand(command);
        }

        //
        // Summary:
        //     When implemented in a derived class, executes when a Pause command is sent to
        //     the service by the Service Control Manager (SCM). Specifies actions to take when
        //     a service pauses.
        protected override void OnPause()
        {
            Log.Info(Log.WithCallInfo(LogSourceMoniker), "OnPause invoked. Pausing...");

            _computer.Pause();

            base.OnPause();
            Log.Info(Log.WithCallInfo(LogSourceMoniker), "Pause initiated. Exiting OnPause.");
        }

        //
        // Summary:
        //     When implemented in a derived class, executes when the computer's power status
        //     has changed. This applies to laptop computers when they go into suspended mode,
        //     which is not the same as a system shutdown.
        //
        // Parameters:
        //   powerStatus:
        //     A System.ServiceProcess.PowerBroadcastStatus that indicates a notification from
        //     the system about its power status.
        //
        // Returns:
        //     When implemented in a derived class, the needs of your application determine
        //     what value to return. For example, if a QuerySuspend broadcast status is passed,
        //     you could cause your application to reject the query by returning false.
        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            Log.Info(Log.WithCallInfo(LogSourceMoniker), "OnPause invoked", "powerStatus", powerStatus);
            return base.OnPowerEvent(powerStatus);
        }

        //
        // Summary:
        //     Executes when a change event is received from a Terminal Server session.
        //
        // Parameters:
        //   changeDescription:
        //     A structure that identifies the change type.
        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
#pragma warning disable SA1117 // Parameters should be on same line or separate lines
            Log.Info(
                Log.WithCallInfo(LogSourceMoniker),
                "OnSessionChange invoked",
                "changeDescription.Reason", changeDescription.Reason,
                "changeDescription.SessionId", changeDescription.SessionId);
#pragma warning restore SA1117 // Parameters should be on same line or separate lines
            base.OnSessionChange(changeDescription);
        }

        //
        // Summary:
        //     When implemented in a derived class, executes when the system is shutting down.
        //     Specifies what should occur immediately prior to the system shutting down.
        protected override void OnShutdown()
        {
            Log.Info(Log.WithCallInfo(LogSourceMoniker), "OnShutdown invoked. Stopping...");

            _computer.Stop();
            WaitForComputerTask();

            base.OnShutdown();
            Log.Info(Log.WithCallInfo(LogSourceMoniker), "Stopped. Exiting OnShutdown.");
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
            Log.Info(Log.WithCallInfo(LogSourceMoniker), "OnStart invoked. Staring...", "args.Length", args?.Length);

            _computerTask = _computer.Run();

            base.OnStart(args);
            Log.Info(Log.WithCallInfo(LogSourceMoniker), "Start initiated. Exiting OnStart.");
        }

        //
        // Summary:
        //     When implemented in a derived class, executes when a Stop command is sent to
        //     the service by the Service Control Manager (SCM). Specifies actions to take when
        //     a service stops running.
        protected override void OnStop()
        {
            Log.Info(Log.WithCallInfo(LogSourceMoniker), "OnStop invoked. Stopping...");

            _computer.Stop();
            WaitForComputerTask();

            base.OnStop();
            Log.Info(Log.WithCallInfo(LogSourceMoniker), "Stopped. Exiting OnStop.");
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
