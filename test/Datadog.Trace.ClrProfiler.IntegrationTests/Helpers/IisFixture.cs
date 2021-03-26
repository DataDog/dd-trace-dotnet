using System;
using System.Diagnostics;
using Datadog.Core.Tools;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.Docker;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public sealed class IisFixture : IDisposable
    {
        private IDisposable _testInstance = null;
        private Process _iisExpress = null;

        public MockTracerAgent Agent { get; private set; }

        public int HttpPort { get; private set; }

        public void TryStartIis(TestHelper helper)
        {
            lock (this)
            {
                if (helper.EnvironmentHelper.ContainerModeEnabled)
                {
                    if (_testInstance == null)
                    {
                        Agent = helper.EnvironmentHelper.CreateMockAgent();
                        HttpPort = TcpPortProvider.GetOpenPort();

                        var manager = new DockerManager(
                            environmentHelper: helper.EnvironmentHelper,
                            containerPort: HttpPort);

                        manager.BuildImage();
                        manager.StartContainer();

                        _testInstance = manager;
                    }
                }
                else
                {
                    if (_iisExpress == null)
                    {
                        Agent = helper.EnvironmentHelper.CreateMockAgent();

                        HttpPort = TcpPortProvider.GetOpenPort();

                        _iisExpress = helper.StartIISExpress(Agent.Port, HttpPort);
                    }
                }
            }
        }

        public void Dispose()
        {
            Agent?.Dispose();
            _testInstance?.Dispose();

            lock (this)
            {
                if (_iisExpress != null)
                {
                    try
                    {
                        if (!_iisExpress.HasExited)
                        {
                            // sending "Q" to standard input does not work because
                            // iisexpress is scanning console key press, so just kill it.
                            // maybe try this in the future:
                            // https://github.com/roryprimrose/Headless/blob/master/Headless.IntegrationTests/IisExpress.cs
                            _iisExpress.Kill();
                            _iisExpress.WaitForExit(8000);
                        }
                    }
                    catch
                    {
                        // in some circumstances the HasExited property throws, this means the process probably hasn't even started correctly
                    }

                    _iisExpress.Dispose();
                }
            }
        }
    }
}
