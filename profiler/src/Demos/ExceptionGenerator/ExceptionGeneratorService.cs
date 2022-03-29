// <copyright file="ExceptionGeneratorService.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.ServiceProcess;
using System.Threading.Tasks;

#pragma warning disable CA1416 // available only on Windows
namespace Datadog.Demos.ExceptionGenerator
{
    internal class ExceptionGeneratorService
    {
        private ExceptionGenerator _exceptionGenerator;

        public void StartService()
        {
            Console.WriteLine(" ########### Starting.");

            _exceptionGenerator = new ExceptionGenerator();
            _exceptionGenerator.Start();
        }

        public void StopService()
        {
            _exceptionGenerator.Stop();
            Console.WriteLine($"{Environment.NewLine} ########### Stopped.");
        }

        public void RunAsService(TimeSpan timeout)
        {
            var windowsService = new WindowsService(this, timeout);
            ServiceBase.Run(windowsService);
        }

        private class WindowsService : ServiceBase
        {
            private ExceptionGeneratorService _computerService;

            public WindowsService(ExceptionGeneratorService service, TimeSpan timeout)
            {
                _computerService = service;
                Task.Delay(timeout).ContinueWith(t => Stop());
            }

            protected override void OnStart(string[] args)
            {
                _computerService.StartService();
                base.OnStart(args);
            }

            protected override void OnStop()
            {
                _computerService.StopService();
                base.OnStop();
            }
        }
    }
}
#pragma warning disable CA1416
