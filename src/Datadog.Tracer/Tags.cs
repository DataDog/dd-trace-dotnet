using System;
using System.Collections.Generic;

namespace Datadog.Tracer
{
    public static class Tags
    {
        // TODO:bertrand sync with what we did with Java
        public const string Service = "Datadog.Service";
        public const string Resource = "Datadog.Resource";
        public const string Error = "Datadog.Error";
        public const string Type = "Datadog.Type";
    }
}
