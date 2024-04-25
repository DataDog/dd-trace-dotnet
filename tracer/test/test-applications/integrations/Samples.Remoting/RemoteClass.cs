using System;

namespace Samples.Remoting
{
    // See https://learn.microsoft.com/en-us/troubleshoot/developer/visualstudio/csharp/language-compilers/create-remote-server
    public class RemoteClass : MarshalByRefObject
    {
        public bool SetString(string input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            using (SampleHelpers.CreateScope("custom-server-span"))
            {
            }

            Console.WriteLine("This string '{0}' has a length of {1}", input, input.Length);
            return input != "";
        }
    }
}
