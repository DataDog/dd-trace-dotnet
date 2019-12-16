namespace Samples.ExampleLibrary.GenericTests
{
    public class ComprehensiveTarget<T1, T2>
    {
        public M1 GetInputMethodGen<M1>(M1 input)
        {
            return input;
        }

        public T1 GetInputTypeGen1(object input)
        {
            return (T1)input;
        }

        public T2 GetInputTypeGen2(object input)
        {
            return (T2)input;
        }
    }
}
