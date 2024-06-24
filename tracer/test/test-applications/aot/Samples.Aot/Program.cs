namespace Samples.Aot
{
    internal class Program
    {
        static void Main(string[] args)
        {

            Console.WriteLine("Hello, " + AotText + " World!");
        }

        static string AotText => "AOT";
    }
}
