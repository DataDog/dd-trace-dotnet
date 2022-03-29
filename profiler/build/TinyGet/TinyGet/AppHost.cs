using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TinyGet.Requests;

namespace TinyGet
{
    internal class AppHost
    {
        private readonly Context _context;
        private readonly IRequestSenderCreator _requestSenderCreator;

        public AppHost(Context context, IRequestSenderCreator requestSenderCreator)
        {
            _context = context;
            _requestSenderCreator = requestSenderCreator;
        }

        public void Run()
        {
            var senders = new List<IRequestSender>(_context.Arguments.Threads);

            for (int i = 0; i < _context.Arguments.Threads; i++)
            {
                senders.Add(_requestSenderCreator.Create(_context));
            }

            Task[] tasks = senders.Select(s => s.Run()).ToArray();
            Task.WaitAll(tasks);
        }
    }
}
