using System.Data;

namespace Datadog.FakeIntegration
{
    public abstract class WoofClientBase
    {
        public abstract void Add(string query, WoofSettings settings);

        public abstract void Add(string query, WoofCallback callback);

        protected WoofRecord Add(WoofStore store, string query, string specialSettings = null, bool isSpecial = false)
        {
            var record = new WoofRecord()
            {
                Id = store.Records.Count + 1,
                Query = query,
                SpecialSettings = specialSettings
            };

            store.Records.Add(record);

            return record;
        }
    }
}
