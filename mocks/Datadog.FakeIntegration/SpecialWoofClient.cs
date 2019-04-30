namespace Datadog.FakeIntegration
{
    public class SpecialWoofClient : WoofClientBase
    {
        private readonly WoofStore _database;

        public SpecialWoofClient()
        {
            _database = new WoofStore();
        }

        public void Add(string query, SpecialWoofSettings settings)
        {
            Add(_database, query, settings.SpecialSettings + settings.EvenMoreSpecialSettings, true);
        }

        public override void Add(string query, WoofSettings settings)
        {
            Add(_database, query, settings.SpecialSettings, isSpecial: true);
        }

        public override void Add(string query, WoofCallback callback)
        {
            var record = Add(_database, query, isSpecial: true);
            callback.Callbacks.ForEach(cb => cb(record));
        }
    }
}