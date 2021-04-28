using System;
using System.Messaging;

namespace Samples.Msmq
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            const string PrivateQueuePath = ".\\Private$\\myQeueue";
            if (MessageQueue.Exists(PrivateQueuePath))
            {
                MessageQueue.Delete(PrivateQueuePath);
            }

            var queue = MessageQueue.Create(PrivateQueuePath);
            queue.Send("Private queue by path name.");
            var rec = queue.Receive();
            queue.Purge();
        }
    }
}
