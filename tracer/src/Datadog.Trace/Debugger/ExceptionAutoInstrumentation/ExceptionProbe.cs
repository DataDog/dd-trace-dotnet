// <copyright file="ExceptionProbe.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Configurations.Models;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal sealed class ExceptionProbe
    {
        internal ExceptionProbe(HashSet<Type> exceptionTypes, ExceptionReplayProbe[] parentProbes, ExceptionReplayProbe[] childProbes)
        {
            ExceptionTypes = exceptionTypes;
            ParentProbes = parentProbes;
            ChildProbes = childProbes;
        }

        internal HashSet<Type> ExceptionTypes { get; }

        internal ExceptionReplayProbe[] ChildProbes { get; }

        internal ExceptionReplayProbe[] ParentProbes { get; }
    }
}
