namespace Rebus.SqlServer.RequestReply.Shared
{
    public class Job
    {
        public int Id { get; private set; }

        public Job(int id)
        {
            Id = id;
        }
    }
}
