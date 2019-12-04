using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using Datadog.Trace;

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
            Debug.Write("\nDDCustomLoggingModule: OnBeginRequest: call from: " + HttpContext.Current.Request.Path + "\n");
        }

        private void OnEndRequest(object sender, EventArgs eventArgs)
        {
            Debug.Write("\nDDCustomLoggingModule: OnEndRequest: call from: " + HttpContext.Current.Request.Path + "\n");
        }
    }
}
