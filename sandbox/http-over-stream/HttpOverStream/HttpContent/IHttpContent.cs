using System.IO;
using System.Threading.Tasks;

namespace HttpOverStream
{
    public interface IHttpContent
    {
        long? Length { get; }

        void WriteTo(Stream stream);

        Task WriteToAsync(Stream stream);
    }
}
