using Ninject.Modules;

namespace Samples.WebForms.Ninject.Services
{
    public class AppModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IDataRepository>().To<InMemoryDataRepository>();
        }
    }
}
