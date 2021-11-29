namespace CallTargetNativeTest
{
    internal class WithOutArguments
    {
        public void VoidMethod(out string arg1, out int arg2)
        {
            arg1 = "Arg01";
            arg2 = 12;
        }
    }
}
