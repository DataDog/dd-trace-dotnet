// <copyright file="ArrayExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class ArrayExtensions
    {
        public static T[] Concat<T>(this T[] array, params T[] newElements)
        {
            var destination = new T[array.Length + newElements.Length];

            Array.Copy(array, 0, destination, 0, array.Length);
            Array.Copy(newElements, 0, destination, array.Length, newElements.Length);

            return destination;
        }
    }
}
