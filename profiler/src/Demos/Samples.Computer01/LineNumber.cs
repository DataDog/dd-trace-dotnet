// <copyright file="LineNumber.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Computer01
{
    internal class LineNumber : ScenarioBase
    {
        public override void OnProcess()
        {
            CallFirstMethod();
        }

        public override void Run()
        {
            Start();
            Thread.Sleep(TimeSpan.FromSeconds(2));
            Stop();
        }

#line 100
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CallFirstMethod()
        {
            CallSecondMethod();
        }
#line default // The CallSecondMethod must stay at line 39, otherwise an integration test will fail.

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CallSecondMethod()
        {
            CallThirdMethod();
        }

#line hidden
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CallThirdMethod()
        {
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }
    }
}
