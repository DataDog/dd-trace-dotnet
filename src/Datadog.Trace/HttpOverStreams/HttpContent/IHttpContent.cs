using System.IO;
using System.Threading.Tasks;

namespace HttpOverStream
{
    internal interface IHttpContent
    {
        long? Length { get; }

        void CopyTo(Stream destination);

        Task CopyToAsync(Stream destination);
    }
}
