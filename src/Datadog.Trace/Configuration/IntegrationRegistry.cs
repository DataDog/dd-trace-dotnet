using System;
using System.Linq;

namespace Datadog.Trace.Configuration
{
    internal static class IntegrationRegistry
    {
        internal static readonly string[] Names;

        static IntegrationRegistry()
        {
            var values = Enum.GetValues(typeof(IntegrationIds));

            Names = new string[values.Cast<int>().Max() + 1];

            foreach (IntegrationIds value in values)
            {
                Names[(int)value] = value.ToString();
            }
        }
    }
}
