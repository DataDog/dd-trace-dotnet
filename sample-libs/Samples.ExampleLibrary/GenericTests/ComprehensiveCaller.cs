using System.Threading.Tasks;

namespace Samples.ExampleLibrary.GenericTests
{
    public class ComprehensiveCaller<Task, T2>
    {
        #region GetInputMethodGen
        public void CallGetInputMethodGen<M1>(ComprehensiveTarget<Task, T2> target, M1 input)
        {
            target.GetInputMethodGen<M1>(input);
        }

        public void CallGetInputMethodGenStruct(ComprehensiveTarget<Task, T2> target, PointStruct input)
        {
            target.GetInputMethodGen<PointStruct>(input);
        }

        public void CallGetInputMethodGenObject(ComprehensiveTarget<Task, T2> target, object input)
        {
            target.GetInputMethodGen<object>(input);
        }

        public void CallGetInputMethodGenValuetypeGenericInst(ComprehensiveTarget<Task, T2> target, StructContainer<Task> input)
        {
            target.GetInputMethodGen<StructContainer<Task>>(input);
        }

        public void CallGetInputMethodGenReferenceTypeGenericInst(ComprehensiveTarget<Task, T2> target, Task<Task> input)
        {
            target.GetInputMethodGen<Task<Task>>(input);
        }
        #endregion

        #region GetInputTypeGen1
        public void CallGetInputTypeGen1(ComprehensiveTarget<Task, T2> target, object input)
        {
            target.GetInputTypeGen1(input);
        }

        public void CallGetInputTypeGen1Struct(ComprehensiveTarget<PointStruct, T2> target, object input)
        {
            target.GetInputTypeGen1(input);
        }

        public void CallGetInputTypeGen1Object(ComprehensiveTarget<object, T2> target, object input)
        {
            target.GetInputTypeGen1(input);
        }

        public void CallGetInputTypeGen1ValuetypeGenericInst(ComprehensiveTarget<StructContainer<Task>, T2> target, object input)
        {
            target.GetInputTypeGen1(input);
        }

        public void CallGetInputTypeGen1ReferenceTypeGenericInst(ComprehensiveTarget<Task<Task>, T2> target, object input)
        {
            target.GetInputTypeGen1(input);
        }
        #endregion

        #region GetInputTypeGen2
        public void CallGetInputTypeGen2(ComprehensiveTarget<Task, T2> target, object input)
        {
            target.GetInputTypeGen2(input);
        }

        public void CallGetInputTypeGen2Struct(ComprehensiveTarget<Task, PointStruct> target, object input)
        {
            target.GetInputTypeGen2(input);
        }

        public void CallGetInputTypeGen1Object(ComprehensiveTarget<Task, object> target, object input)
        {
            target.GetInputTypeGen2(input);
        }

        public void CallGetInputTypeGen2ValuetypeGenericInst(ComprehensiveTarget<Task, StructContainer<T2>> target, object input)
        {
            target.GetInputTypeGen2(input);
        }

        public void CallGetInputTypeGen2ReferenceTypeGenericInst(ComprehensiveTarget<Task, Task<T2>> target, object input)
        {
            target.GetInputTypeGen2(input);
        }
        #endregion
    }
}
