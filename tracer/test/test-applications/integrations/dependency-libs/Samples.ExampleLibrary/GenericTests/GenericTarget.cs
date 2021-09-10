namespace Samples.ExampleLibrary.GenericTests
{
    public class GenericTarget<T1, T2>
    {
        public M1 ReturnM1<M1, M2>(M1 input1, M2 input2)
        {
            return input1;
        }

        public M2 ReturnM2<M1, M2>(M1 input1, M2 input2)
        {
            return input2;
        }

        public T1 ReturnT1(object input)
        {
            return (T1)input;
        }

        public T2 ReturnT2(object input)
        {
            return (T2)input;
        }
    }
}
