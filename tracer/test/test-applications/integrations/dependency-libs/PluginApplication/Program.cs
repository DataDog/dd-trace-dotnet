
using System;
using System.Threading;

namespace PluginApplication
{
    public class Program : MarshalByRefObject
    {
        public void Invoke()
        {
            Console.WriteLine("Invoked PluginApplication.Program.Invoke()");
            Thread.Sleep(1000);
        }
    }
}
