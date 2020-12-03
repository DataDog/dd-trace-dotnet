using System;

namespace Datadog.Trace.Configuration
{
    internal readonly struct IntegrationInfo
    {
        public readonly string Name;

        public readonly int Id;

        public IntegrationInfo(string integrationName)
        {
            if (integrationName == null)
            {
                throw new ArgumentNullException(nameof(integrationName));
            }

            Name = integrationName;
            Id = 0;
        }

        public IntegrationInfo(int integrationId)
        {
            Name = null;
            Id = integrationId;
        }
    }
}
