// <copyright file="GarbageCollections.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;

namespace Samples.Computer01
{
    public class GarbageCollections : ScenarioBase
    {
        private int _generation;

        public GarbageCollections(int generation)
        {
            _generation = generation;
        }

        public override void OnProcess()
        {
            TriggerCollections();
        }

        public void TriggerCollections()
        {
            GC.Collect(_generation, GCCollectionMode.Forced);
        }
    }
}
