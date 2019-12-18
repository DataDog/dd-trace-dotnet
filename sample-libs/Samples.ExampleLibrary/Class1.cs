// <copyright file="Class1.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;

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

        public string ToCustomString()
        {
            return "Custom";
        }

        public object ToObject()
        {
            return this;
        }

        public Class1[] ToArray()
        {
            return new Class1[] { this };
        }

        public Array ToCustomArray()
        {
            var lengthsArray = new int[2] { 5, 10 };
            var lowerBoundsArray = new int[2] { 20, 15 };
            return Array.CreateInstance(typeof(Class1), lengthsArray, lowerBoundsArray);
        }

        public Class1[, ,] ToMdArray()
        {
            return new Class1[4, 2, 3];
        }

        public Class1[][] ToJaggedArray()
        {
            return new Class1[][]
            {
                new Class1[] { this },
                new Class1[] { null, null }
            };
        }

        public List<Class1> ToList()
        {
            return new List<Class1>() { this };
        }

        public List<Class1>.Enumerator ToEnumerator()
        {
            return ToList().GetEnumerator();
        }
    }
}
