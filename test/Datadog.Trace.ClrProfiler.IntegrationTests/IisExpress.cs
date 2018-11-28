using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public sealed class IisExpress : IDisposable
    {
        private readonly Process _process;

        public IisExpress()
        {
            _process = new Process();
            _process.Exited += (sender, args) => IsRunning = false;
            _process.EnableRaisingEvents = true;
        }

        public event DataReceivedEventHandler OutputDataReceived
        {
            add => _process.OutputDataReceived += value;
            remove => _process.OutputDataReceived -= value;
        }

        public event DataReceivedEventHandler ErrorDataReceived
        {
            add => _process.ErrorDataReceived += value;
            remove => _process.ErrorDataReceived -= value;
        }

        public event EventHandler<EventArgs<string>> Message;

        public bool IsRunning { get; private set; }

        public void Start(
            string applicationDirectory,
            bool run64BitProcess,
            int iisPort,
            IDictionary<string, string> environmentVariables)
        {
            if (applicationDirectory == null) { throw new ArgumentNullException(nameof(applicationDirectory)); }

            if (!Directory.Exists(applicationDirectory))
            {
                var exception = new ArgumentException("Web application directory not found.");
                exception.Data["ApplicationDirectory"] = applicationDirectory;
                throw exception;
            }

            var exe = run64BitProcess
                          ? @"C:\Program Files\IIS Express\iisexpress.exe"
                          : @"C:\Program Files (x86)\IIS Express\iisexpress.exe";

            if (!File.Exists(exe))
            {
                var exception = new FileNotFoundException("IIS Express executable not found.");
                exception.Data["ExecutablePath"] = exe;
                throw exception;
            }

            var args = $"/clr:v4.0 /path:{applicationDirectory} /port:{iisPort} /systray:false /trace:info";

            var startInfo = new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = false,
            };

            if (environmentVariables != null)
            {
                foreach (var keyValuePair in environmentVariables)
                {
                    startInfo.Environment[keyValuePair.Key] = keyValuePair.Value;
                }
            }

            OnMessage($"starting \"{exe}\" {args}");

            _process.StartInfo = startInfo;
            _process.Start();

            if (!_process.HasExited)
            {
                IsRunning = true;

                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }
        }

        public void Stop()
        {
            const int windowsMessageKeyDown = 0x100;
            const int virtualKeyQ = 0x51;

            if (_process == null || _process.HasExited)
            {
                // nothing to do here
                return;
            }

            try
            {
                // we can't output to the test runner anymore, so stop redirecting so at least
                // it shows up in the IIS Express console window when running in a dev machine
                _process.CancelOutputRead();
                _process.CancelErrorRead();

                IntPtr windowHandle = _process.MainWindowHandle;

                if (windowHandle != IntPtr.Zero)
                {
                    OnMessage("Sending 'Q' keystroke to IIS Express window.");
                    bool success = NativeMethods.PostMessage(windowHandle, windowsMessageKeyDown, virtualKeyQ, 0);

                    if (success)
                    {
                        OnMessage("Stop requested. Waiting for IIS Express to exit...");

                        if (_process.WaitForExit(20000))
                        {
                            // WaitForExit(int) doesn't wait for asynchronous event handlers to finish,
                            // so if it returns true, we need an additional WaitForExit() call
                            _process.WaitForExit();
                        }
                    }
                    else
                    {
                        int hr = Marshal.GetHRForLastWin32Error();
                        throw Marshal.GetExceptionForHR(hr);
                    }
                }
            }
            finally
            {
                if (!_process.HasExited)
                {
                    OnMessage("IIS Express still running. Killing forcefully...");
                    _process.Kill();
                }

                OnMessage("IIS Express stopped.");
            }
        }

        public void Dispose()
        {
            if (_process != null)
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                }

                _process.Dispose();
            }
        }

        private void OnMessage(string message)
        {
            Message?.Invoke(this, new EventArgs<string>(message));
        }
    }
}
