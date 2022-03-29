// <copyright file="IScopeManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace
{
    /// <summary>
    /// Interface for managing a scope.
    /// </summary>
    internal interface IScopeManager
    {
        Scope Active { get; }

        Scope Activate(Span span, bool finishOnClose);

        void Close(Scope scope);
    }
}
