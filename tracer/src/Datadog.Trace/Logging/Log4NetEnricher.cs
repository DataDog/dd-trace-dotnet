// <copyright file="Log4NetEnricher.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Logging
{
    internal class Log4NetEnricher : LogEnricher
    {
        public Log4NetEnricher(ILogProvider logProvider)
            : base(logProvider)
        {
        }

        protected override object CreateTracerProperty(Tracer tracer, Func<Tracer, string> getter)
        {
            var fixingRequiredType = Type.GetType("log4net.Core.IFixingRequired, log4net");

            return new TracerProperty(tracer, getter).DuckCast(fixingRequiredType);
        }

        private class TracerProperty
        {
            private readonly Tracer _tracer;
            private readonly Func<Tracer, string> _getter;

            public TracerProperty(Tracer tracer, Func<Tracer, string> getter)
            {
                _tracer = tracer;
                _getter = getter;
            }

            // ReSharper disable once UnusedMember.Global UnusedMember.Local
            [DuckReverseMethod]
            public object GetFixedObject() => _getter(_tracer);

            [DuckInclude]
            public override string ToString()
            {
                // Appenders that format the log in another context (for instance, async appenders) use GetFixedObject to get the value
                // Other appenders use ToString
                return _getter(_tracer);
            }
        }
    }
}
