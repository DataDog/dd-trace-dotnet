using Ninject;
using Ninject.Activation.Caching;
using Ninject.Components;

namespace Samples.NinjectMvcCache.Infrastructure
{
    public sealed class NoOpActivationCache : NinjectComponent, IActivationCache
    {
        public void Clear()
        {
        }

        public void AddActivatedInstance(object instance)
        {
        }

        public void AddDeactivatedInstance(object instance)
        {
        }

        public bool IsActivated(object instance)
        {
            return false;
        }

        public bool IsDeactivated(object instance)
        {
            return false;
        }

        public void Prune()
        {
        }
    }
}
