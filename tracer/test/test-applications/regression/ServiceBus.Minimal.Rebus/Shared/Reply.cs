namespace ServiceBus.Minimal.Rebus.Shared
{
    public class Reply
    {
        public int Id { get; private set; }
        public int OsProcessId { get; private set; }

        public Reply(int id, int osProcessId)
        {
            Id = id;
            OsProcessId = osProcessId;
        }
    }
}
