// <copyright file="ExceptionsProfilerTestScenario.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;

namespace ExceptionGenerator
{
    internal class ExceptionsProfilerTestScenario
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Run()
        {
            // Throw 2 InvalidOperationExceptions with same message/callstack
            // Then 2 NotSupportedExceptions with same message/callstack
            // Then 2 NotImplementedException with same message but different callstack
            // Then 2 Exception with different messages but same callstack

            Throw1(new InvalidOperationException("IOE"));
            Throw1(new InvalidOperationException("IOE"));

            Throw1(new NotSupportedException("NSE"));
            Throw1(new NotSupportedException("NSE"));

            Throw1(new NotImplementedException("NIE"));
            Throw2(new NotImplementedException("NIE"));

            Throw1(new Exception("E1"));
            Throw1(new Exception("E2"));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Throw1(Exception ex)
        {
            try
            {
                Throw1_1(ex);
            }
            catch
            {
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Throw1_1(Exception ex)
        {
            Throw1_2(ex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Throw1_2(Exception ex)
        {
            throw ex;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Throw2(Exception ex)
        {
            try
            {
                Throw2_1(ex);
            }
            catch
            {
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Throw2_1(Exception ex)
        {
            Throw2_2(ex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Throw2_2(Exception ex)
        {
            Throw2_3(ex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Throw2_3(Exception ex)
        {
            throw ex;
        }
    }
}
