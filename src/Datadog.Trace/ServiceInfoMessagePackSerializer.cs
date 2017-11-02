using MsgPack;
using MsgPack.Serialization;

namespace Datadog.Trace
{
    internal class ServiceInfoMessagePackSerializer : MessagePackSerializer<ServiceInfo>
    {
        public ServiceInfoMessagePackSerializer(SerializationContext context) : base(context)
        {
        }

        protected override void PackToCore(Packer packer, ServiceInfo serviceInfo)
        {
            packer.PackMapHeader(1);
            packer.PackString(serviceInfo.ServiceName);
            packer.PackMapHeader(2);
            packer.PackString("app");
            packer.PackString(serviceInfo.App);
            packer.PackString("app_type");
            packer.PackString(serviceInfo.AppType);
        }

        protected override ServiceInfo UnpackFromCore(Unpacker unpacker)
        {
            throw new System.NotImplementedException();
        }
    }
}
