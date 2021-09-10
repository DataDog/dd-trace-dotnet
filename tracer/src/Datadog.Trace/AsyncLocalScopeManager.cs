// <copyright file="AsyncLocalScopeManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class AsyncLocalScopeManager : ScopeManagerBase
    {
        private readonly AsyncLocalCompat<Scope> _activeScope = new AsyncLocalCompat<Scope>();

        public override Scope Active
        {
            get
            {
                return _activeScope.Get();
            }

            protected set
            {
                _activeScope.Set(value);
            }
        }
    }
}
