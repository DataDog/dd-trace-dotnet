// <copyright file="FrameworkDiagnosticObserverListenerObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETFRAMEWORK

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Activity
{
    internal sealed class FrameworkDiagnosticObserverListenerObserver
    {
        [DuckReverseMethod]
        public void OnCompleted()
        {
        }

        [DuckReverseMethod]
        public void OnError(Exception error)
        {
        }

        [DuckReverseMethod]
        public void OnNext(object value)
        {
            DiagnosticObserverListener.OnSetListener(value);
        }
    }
}

#endif
