using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            Debug.Write("\nCustomLoggingModule: OnBeginRequest: call from: " + HttpContext.Current.Request.Path + "\n");
        }

        private void OnEndRequest(object sender, EventArgs eventArgs)
        {
            Debug.Write("\nCustomLoggingModule: OnEndRequest: call from: " + HttpContext.Current.Request.Path + "\n");
        }
    }
}
