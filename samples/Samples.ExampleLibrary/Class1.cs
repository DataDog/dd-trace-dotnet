// <copyright file="Class1.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System;

namespace Samples.ExampleLibrary
{
    public class Class1
    {
        public int Add(int x, int y)
        {
            return x + y;
        }

        public virtual int Multiply(int x, int y)
        {
            return x * y;
        }

        public Func<int, int, int> Divide = (int x, int y) => x / y;

        public object Example(object o1, object o2)
        {
            return o1 ?? o2;
        }


        public object ExampleWrapper(object o1, object o2)
        {
            object enter = default(object);
            try
            {
                enter = OnMethodEnter(new object[] { o1, o2 });
            }
            catch
            {
            }
            object res = default(object);
            try
            {
                res = (o1) ?? (o2);
            }
            catch (Exception ex)
            {
                try
                {
                    OnMethodExit(enter, ex, ref res);
                }
                catch
                {
                }
                throw;
            }
            try
            {
                OnMethodExit(enter, null, ref res);
            }
            catch
            {
            }
            return res;
        }

        private object OnMethodEnter(object[] v)
        {
            throw new NotImplementedException();
        }

        private void OnMethodExit(object enter, Exception ex, ref object res)
        {
            throw new NotImplementedException();
        }
    }
}
