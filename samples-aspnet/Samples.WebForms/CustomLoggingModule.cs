using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Datadog.Trace;

namespace Samples.WebForms
{
    public class CustomLoggingModule : IHttpModule
    {
        public void Dispose()
        {
            // Nothing to do
        }

        public void Init(HttpApplication context)
        {
            context.BeginRequest += OnBeginRequest;
            context.EndRequest += OnEndRequest;
        }

        private void OnBeginRequest(object sender, EventArgs eventArgs)
        {
            File.WriteAllText("C:\\logs\\Samples.WebForms.log", $"dd.trace_id={CorrelationIdentifier.TraceId}, dd.span_id={CorrelationIdentifier.SpanId}{Environment.NewLine}");
        }

        private void OnEndRequest(object sender, EventArgs eventArgs)
        {
            // Do nothing
        }
    }
}
