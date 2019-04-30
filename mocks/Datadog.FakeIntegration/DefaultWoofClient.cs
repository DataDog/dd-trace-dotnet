namespace Datadog.FakeIntegration
{
    public class DefaultWoofClient : WoofClientBase
    {
        private readonly WoofStore _database;

        public DefaultWoofClient()
        {
            _database = new WoofStore();
        }

        public override void Add(string query, WoofSettings settings)
        {
            Add(_database, query, settings.SpecialSettings);
        }

        public override void Add(string query, WoofCallback callback)
        {
            var record = Add(_database, query);
            callback.Callbacks.ForEach(cb => cb(record));
        }
    }
}
