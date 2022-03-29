namespace TinyGet.Requests
{
    internal interface IRequestSenderCreator
    {
        IRequestSender Create(Context context);
    }
}