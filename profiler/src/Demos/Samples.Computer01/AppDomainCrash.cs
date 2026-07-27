// <copyright file="AppDomainCrash.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Threading;

namespace Samples.Computer01
{
    internal class AppDomainCrash : ScenarioBase
    {
        private readonly int _nbAppDomain;

        public AppDomainCrash(int nbAppdomain)
        {
            _nbAppDomain = nbAppdomain;
        }

        public override void OnProcess()
        {
            Console.WriteLine("Thread start " + Thread.CurrentThread.GetHashCode() + " for #" + _nbAppDomain + " app domains in app domain " + AppDomain.CurrentDomain.FriendlyName);
            var appDomain = AppDomain.CreateDomain("Test-1");
            while (!IsEventSet())
            {
                appDomain.DoCallBack(new CrossAppDomainDelegate(Worker));
                Thread.Sleep(TimeSpan.FromMilliseconds(500));
            }

            try
            {
                AppDomain.Unload(appDomain);
            }
            catch
            {
            }

            Console.WriteLine("Thread end " + Thread.CurrentThread.GetHashCode() + " in app domain " + AppDomain.CurrentDomain.FriendlyName);
        }

        private static void Worker()
        {
            try
            {
                Console.WriteLine("Throwing bye bye in app domain" + AppDomain.CurrentDomain.FriendlyName);
                // Use the exception profiler to capture callstack
                throw new CatException("Fluff");
            }
            catch
            {
                // just swallow
            }
        }

        private class CatException : Exception
        {
            public CatException()
                : base()
            {
            }

            public CatException(string message)
                : base(message)
            {
            }
        }
    }
}
#endif
