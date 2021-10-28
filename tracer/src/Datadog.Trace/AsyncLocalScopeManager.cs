// <copyright file="AsyncLocalScopeManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;

namespace Datadog.Trace
{
    internal class AsyncLocalScopeManager : ScopeManagerBase
    {
        private readonly AsyncLocal<Scope> _activeScope = new();

        public override Scope Active
        {
            get
            {
                return _activeScope.Value;
            }

            protected set
            {
                _activeScope.Value = value;
            }
        }
    }
}
