using Microsoft.Extensions.Hosting;

namespace OrleansCrash.Clients
{
    public interface IHelloWorldHostedService : IHostedService
    {
        Grains.IHello GimmeTheGrain();
    }
}
