using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallTargetNativeTest
{
    internal class WithRefArguments
    {
        public string StringValue { get; set; }
        public int IntValue { get; set; }

        public void VoidMethod(string arg1, int arg2)
        {
            StringValue = arg1;
            IntValue = arg2;
        }

        public void VoidRefMethod(ref string arg1, ref int arg2)
        {
            StringValue = arg1;
            IntValue = arg2;
        }


        public void VoidMethod(string arg1)
        {
        }

        public void VoidRefMethod(ref string arg1)
        {
            arg1 = "Hello world";
        }
    }
}
