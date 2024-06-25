using System.Reflection;
using System.Runtime.Serialization;

namespace Samples.Aot
{
    [DefaultMember("Main")]
    internal class Program
    {
        [CLSCompliant(true)]
        static void Main(string[] args)
        {

            Console.WriteLine("Hello, " + AotText + " World!");
        }

        [IgnoreDataMember]
        static string AotText => "AOT";
    }
}
