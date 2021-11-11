using System.ServiceModel.Channels;

namespace Samples.Wcf.Bindings.Custom
{
    public class CustomBindingElement : BindingElement
    {
        public bool ThrowOnFirstMessageAccess { get; set; } = true;

        public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
        {
            return new CustomChannelFactory<TChannel>(context.BuildInnerChannelFactory<TChannel>(), this);
        }

        public override IChannelListener<TChannel> BuildChannelListener<TChannel>(BindingContext context)
        {
            return new CustomChannelListener<TChannel>(context.BuildInnerChannelListener<TChannel>(), this);
        }

        public override BindingElement Clone()
        {
            return new CustomBindingElement()
            {
                ThrowOnFirstMessageAccess = this.ThrowOnFirstMessageAccess,
            };
        }

        public override T GetProperty<T>(BindingContext context)
            => context.GetInnerProperty<T>();
    }
}
