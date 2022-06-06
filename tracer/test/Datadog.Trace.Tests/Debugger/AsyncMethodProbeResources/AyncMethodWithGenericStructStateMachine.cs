// <copyright file="AyncMethodWithGenericStructStateMachine.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.Tests.Debugger.AsyncMethodProbeResources
{
    // Will be addressed in the next async probes PR
    internal class AyncMethodWithGenericStructStateMachine : IAsyncTestRun
    {
        public List<string> Run()
        {
            throw new System.NotImplementedException();
        }
    }
}
