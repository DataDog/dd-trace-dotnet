using System.Collections.Generic;

namespace Datadog.FakeIntegration
{
    public class WoofStore
    {
        private static List<WoofRecord> _records = new List<WoofRecord>();

        public List<WoofRecord> Records
        {
            get => _records;
            set => _records = value;
        }
    }
}
