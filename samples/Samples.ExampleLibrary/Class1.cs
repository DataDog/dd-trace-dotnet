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
    }
}
