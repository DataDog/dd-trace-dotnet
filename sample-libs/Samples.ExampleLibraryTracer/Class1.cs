using System;

namespace Samples.ExampleLibraryTracer
{
    public class Class1
    {
        public int Add(int x, int y)
        {
            return 2 * (x + y);
        }

        public virtual int Multiply(int x, int y)
        {
            return 2 * (x * y);
        }
    }
}
