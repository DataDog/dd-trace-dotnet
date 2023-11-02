// <copyright file="SigSegvHandlerExecution.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;

namespace Samples.Computer01
{
    internal class SigSegvHandlerExecution : ScenarioBase
    {
        private interface IFluff
        {
            void Plop();
        }

        public override void OnProcess()
        {
            try
            {
                GetFluff().Plop();
            }
            catch
            {
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Fluffinator GetFluff()
        {
            return null;
        }

        private class Fluffinator
        {
            public virtual void Plop()
            {
                Console.WriteLine("plop");
            }
        }
    }
}
