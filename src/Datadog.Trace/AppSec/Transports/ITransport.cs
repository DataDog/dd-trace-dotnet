// <copyright file="ITransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.EventModel;
using Datadog.Trace.AppSec.Waf;

namespace Datadog.Trace.AppSec.Transport
{
    internal interface ITransport
    {
        Request Request();

        Response Response(bool blocked);

        void Block();

        IContext GetAdditiveContext();

        void SetAdditiveContext(IContext additiveContext);

        void AddRequestScope(Guid guid);
    }
}
