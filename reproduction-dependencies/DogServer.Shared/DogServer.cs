using System;

namespace DogServer.Shared
{
    public class DogServer : MarshalByRefObject
    {
        public Guid ServerInstanceId;

        public DogServer()
        {
            ServerInstanceId = Guid.NewGuid();
        }

        public virtual int StartServer(string[] args)
        {
            // no-op
            return 0;
        }
    }
}
