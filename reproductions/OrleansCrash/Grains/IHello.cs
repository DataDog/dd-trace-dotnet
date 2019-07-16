using System.Threading.Tasks;

namespace OrleansCrash.Grains
{
    public interface IHello : Orleans.IGrainWithIntegerKey
    {
        Task<string> SayHello(string msg);
    }
}
