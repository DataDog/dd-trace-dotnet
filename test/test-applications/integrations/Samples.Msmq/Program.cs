using System;
using System.Messaging;
using System.Threading;

namespace Samples.Msmq
{
    internal class Program
    {
        const string PrivateQueuePath = ".\\Private$\\myQueue3";
        public static void Main(string[] args)
        {
            var queue = GetOrCreate(PrivateQueuePath);

            SendWithTransactionType(queue);
            Console.WriteLine("message sent");
            Console.ReadLine();
            var rec = queue.Receive();
            Console.WriteLine("first message received"+ rec.ToString());
            //Console.ReadLine();

            //SendWithTransactionType(queue);
            //rec = queue.Receive(TimeSpan.FromSeconds(1));
            //Console.WriteLine("second message received");
            //SendWithTransactionType(queue);

            //var transQ = SendWithinTransaction(queue);

            //rec = queue.Receive(TimeSpan.FromSeconds(2));
            //Console.WriteLine("third nessage received");


            queue.Purge();

            Console.WriteLine("finish");
        }

        private static MessageQueue GetOrCreate(string privateQueuePath) => MessageQueue.Exists(privateQueuePath) ? new MessageQueue(privateQueuePath) : MessageQueue.Create(privateQueuePath, true);

        private static MessageQueueTransaction SendWithinTransaction(MessageQueue queue)
        {
            var transQ = new MessageQueueTransaction();
            transQ.Begin();
            queue.Send(3, "label3", transQ);
            transQ.Commit();
            return transQ;
        }

        private static void SendWithTransactionType(MessageQueue queue)
        {
            queue.Send("a message with transaction type", "label2", MessageQueueTransactionType.Single);
        }

        private static void SendWithoutTransaction(MessageQueue queue)
        {
            queue.Send("a message", "label");
        }
    }
}
