using System;

namespace TinyGet.Requests
{
    internal class RequestSenderCreator : IRequestSenderCreator
    {
        public IRequestSender Create(Context context)
        {
            return new RequestSender(context);
        }
    }
}
