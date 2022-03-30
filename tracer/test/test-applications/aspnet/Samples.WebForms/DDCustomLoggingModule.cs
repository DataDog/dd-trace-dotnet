using System;
using System.Diagnostics;
using System.Web;

namespace Samples.WebForms
{
    public class DDCustomLoggingModule : IHttpModule
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
            Debug.Write("\nDDCustomLoggingModule: OnBeginRequest: call from: " + HttpContext.Current.Request.Path + ". TraceID:" + SampleHelpers.GetCorrelationIdentifierTraceId() + "\n");
        }

        private void OnEndRequest(object sender, EventArgs eventArgs)
        {
            Debug.Write("\nDDCustomLoggingModule: OnEndRequest: call from: " + HttpContext.Current.Request.Path + ". TraceID:" + SampleHelpers.GetCorrelationIdentifierTraceId() + "\n");
        }
    }
}
