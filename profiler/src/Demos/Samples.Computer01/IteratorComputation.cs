// <copyright file="IteratorComputation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Samples.Computer01
{
    public class IteratorComputation : ScenarioBase
    {
        public IteratorComputation(int nbThreads)
            : base(nbThreads)
        {
        }

        public override void OnProcess()
        {
            CallIterators();
        }

        private void CallIterators()
        {
            Iterator it = new Iterator(1000);
        }

        // this is used to see the name of the constructor method (i.e. .ctor)
        public class Iterator
        {
            public Iterator(int count)
            {
                var sequence = GetEvenSequence(count);
                Console.WriteLine($"sequence has {sequence.Count()} elements");
            }

            private static IEnumerable<int> GetEvenSequence(int count)
            {
                int i = 0;
                while (true)
                {
                    if (i % 2 == 0)
                    {
                        Thread.Sleep(10);
                        yield return 2;
                    }

                    i++;

                    if (--count <= 0)
                    {
                        yield break;
                    }
                }
            }
        }
    }
}
