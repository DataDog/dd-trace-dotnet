// <copyright file="TypeNameTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class TypeNameTests
    {
        public static IEnumerable<object[]> GetConstTypeAssociations()
        {
            yield return new object[] { ClrNames.Ignore, "_" };
            yield return new object[] { ClrNames.Void, typeof(void) };
            yield return new object[] { ClrNames.Object, typeof(object) };
            yield return new object[] { ClrNames.Bool, typeof(bool) };
            yield return new object[] { ClrNames.String, typeof(string) };
            yield return new object[] { ClrNames.SByte, typeof(sbyte) };
            yield return new object[] { ClrNames.Int16, typeof(short) };
            yield return new object[] { ClrNames.Int32, typeof(int) };
            yield return new object[] { ClrNames.Int64, typeof(long) };
            yield return new object[] { ClrNames.Byte, typeof(byte) };
            yield return new object[] { ClrNames.UInt16, typeof(ushort) };
            yield return new object[] { ClrNames.UInt32, typeof(uint) };
            yield return new object[] { ClrNames.UInt64, typeof(ulong) };
            yield return new object[] { ClrNames.CancellationToken, typeof(System.Threading.CancellationToken) };
            yield return new object[] { ClrNames.Task, typeof(Task) };
            yield return new object[] { ClrNames.IAsyncResult, typeof(IAsyncResult) };
            yield return new object[] { ClrNames.AsyncCallback, typeof(AsyncCallback) };
            yield return new object[] { ClrNames.HttpRequestMessage, typeof(System.Net.Http.HttpRequestMessage) };
            yield return new object[] { ClrNames.HttpResponseMessage, typeof(System.Net.Http.HttpResponseMessage) };
            yield return new object[] { ClrNames.HttpResponseMessageTask, "System.Threading.Tasks.Task`1[System.Net.Http.HttpResponseMessage]" }; // Generic full names have square brackets
            yield return new object[] { ClrNames.GenericTask, typeof(Task<>) };
            yield return new object[] { ClrNames.Stream, typeof(Stream) };
            yield return new object[] { ClrNames.GenericTaskWithGenericClassParameter, "System.Threading.Tasks.Task`1[!0]" };
            yield return new object[] { ClrNames.GenericTaskWithGenericMethodParameter, "System.Threading.Tasks.Task`1[!!0]" };
            yield return new object[] { ClrNames.ObjectTask, "System.Threading.Tasks.Task`1[System.Object]" };
            yield return new object[] { ClrNames.Int32Task, "System.Threading.Tasks.Task`1[System.Int32]" };
            yield return new object[] { ClrNames.TimeSpan, "System.TimeSpan" };
            yield return new object[] { ClrNames.Type, typeof(Type) };
            yield return new object[] { ClrNames.Activity, "System.Diagnostics.Activity" };
            yield return new object[] { ClrNames.Process, "System.Diagnostics.Process" };
            yield return new object[] { ClrNames.ByteArray, typeof(byte[]) };
            yield return new object[] { ClrNames.Exception, typeof(Exception) };
        }

        [Fact]
        public void EveryMemberOfTypeNamesIsRepresented()
        {
            var associations = GetConstTypeAssociations().Select(i => i[0]).ToList();
            var expectedItems =
                typeof(ClrNames)
                   .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                   .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string))
                   .ToList();

            Assert.Equal(actual: associations.Count, expected: expectedItems.Count);

            var missing = new List<string>();
            foreach (var expectedItem in expectedItems)
            {
                var value = (string)expectedItem.GetRawConstantValue();
                if (associations.Contains(value))
                {
                    continue;
                }

                missing.Add(value);
            }

            Assert.Empty(missing);
        }

        [Theory]
        [MemberData(nameof(GetConstTypeAssociations))]
        public void MatchesExpectedTypeName(string constant, object type)
        {
            if (type is Type)
            {
                Assert.Equal(constant, ((Type)type).FullName);
            }

            if (type is string)
            {
                Assert.Equal(constant, (string)type);
            }
        }
    }
}
