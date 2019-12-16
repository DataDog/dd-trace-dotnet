using System;
using System.Threading.Tasks;

namespace Samples.ExampleLibrary.GenericTests
{
    public class ComprehensiveCaller<CallerT1, CallerT2>
    {
        #region CallReturnM1
        public void CallReturnM1WithCallerTypeArgs(GenericTarget<CallerT1, CallerT2> target, CallerT1 input1, CallerT2 input2)
        {
            target.ReturnM1<CallerT1, CallerT2>(input1, input2);
        }

        public void CallReturnM1WithCallerTypeArgsReversed(GenericTarget<CallerT1, CallerT2> target, CallerT1 input1, CallerT2 input2)
        {
            target.ReturnM1<CallerT2, CallerT1>(input2, input1);
        }

        public void CallReturnM1WithClass(GenericTarget<CallerT1, CallerT2> target, Exception input1, CallerT2 input2)
        {
            target.ReturnM1<Exception, CallerT2>(input1, input2);
        }

        public void CallReturnM1WithStruct(GenericTarget<CallerT1, CallerT2> target, PointStruct input1, CallerT2 input2)
        {
            target.ReturnM1<PointStruct, CallerT2>(input1, input2);
        }

        public void CallReturnM1WithReferenceTypeGenericInstantiation(GenericTarget<CallerT1, CallerT2> target, Task<Exception> input1, CallerT2 input2)
        {
            target.ReturnM1<Task<Exception>, CallerT2>(input1, input2);
        }

        public void CallReturnM1WithValueTypeGenericInstantiation(GenericTarget<CallerT1, CallerT2> target, StructContainer<Exception> input1, CallerT2 input2)
        {
            target.ReturnM1<StructContainer<Exception>, CallerT2>(input1, input2);
        }
        #endregion

        #region CallReturnM2
        public void CallReturnM2WithCallerTypeArgs(GenericTarget<CallerT1, CallerT2> target, CallerT1 input1, CallerT2 input2)
        {
            target.ReturnM2<CallerT1, CallerT2>(input1, input2);
        }

        public void CallReturnM2WithCallerTypeArgsReversed(GenericTarget<CallerT1, CallerT2> target, CallerT1 input1, CallerT2 input2)
        {
            target.ReturnM2<CallerT2, CallerT1>(input2, input1);
        }

        public void CallReturnM2WithClass(GenericTarget<CallerT1, CallerT2> target, CallerT1 input1, Exception input2)
        {
            target.ReturnM2<CallerT1, Exception>(input1, input2);
        }

        public void CallReturnM2WithStruct(GenericTarget<CallerT1, CallerT2> target, CallerT1 input1, PointStruct input2)
        {
            target.ReturnM2<CallerT1, PointStruct>(input1, input2);
        }

        public void CallReturnM2WithReferenceTypeGenericInstantiation(GenericTarget<CallerT1, CallerT2> target, CallerT1 input1, Task<Exception> input2)
        {
            target.ReturnM2<CallerT1, Task<Exception>>(input1, input2);
        }

        public void CallReturnM2WithValueTypeGenericInstantiation(GenericTarget<CallerT1, CallerT2> target, CallerT1 input1, StructContainer<Exception> input2)
        {
            target.ReturnM2<CallerT1, StructContainer<Exception>>(input1, input2);
        }
        #endregion

        #region CallReturnT1
        public void CallReturnT1WithCallerTypeArgs(GenericTarget<CallerT1, CallerT2> target, object input)
        {
            target.ReturnT1(input);
        }

        public void CallReturnT1WithCallerTypeArgsReversed(GenericTarget<CallerT2, CallerT1> target, object input)
        {
            target.ReturnT1(input);
        }

        public void CallReturnT1WithClass(GenericTarget<Exception, CallerT2> target, object input)
        {
            target.ReturnT1(input);
        }

        public void CallReturnT1WithStruct(GenericTarget<PointStruct, CallerT2> target, object input)
        {
            target.ReturnT1(input);
        }

        public void CallReturnT1WithReferenceTypeGenericInstantiation(GenericTarget<Task<Exception>, CallerT2> target, object input)
        {
            target.ReturnT1(input);
        }

        public void CallReturnT1WithValueTypeGenericInstantiation(GenericTarget<StructContainer<Exception>, CallerT2> target, object input)
        {
            target.ReturnT1(input);
        }
        #endregion

        #region CallReturnT2
        public void CallReturnT2WithCallerTypeArgs(GenericTarget<CallerT1, CallerT2> target, object input)
        {
            target.ReturnT2(input);
        }

        public void CallReturnT2WithCallerTypeArgsReversed(GenericTarget<CallerT2, CallerT1> target, object input)
        {
            target.ReturnT2(input);
        }

        public void CallReturnT2WithClass(GenericTarget<CallerT1, Exception> target, object input)
        {
            target.ReturnT2(input);
        }

        public void CallReturnT2WithStruct(GenericTarget<CallerT1, PointStruct> target, object input)
        {
            target.ReturnT2(input);
        }

        public void CallReturnT2WithReferenceTypeGenericInstantiation(GenericTarget<CallerT1, Task<Exception>> target, object input)
        {
            target.ReturnT2(input);
        }

        public void CallReturnT2WithValueTypeGenericInstantiation(GenericTarget<CallerT1, StructContainer<Exception>> target, object input)
        {
            target.ReturnT2(input);
        }
        #endregion
    }
}
