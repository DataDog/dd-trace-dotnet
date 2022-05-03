// <copyright file="ActionProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Tagging
{
    internal readonly struct ActionProcessor<T> : IItemProcessor<T>
    {
        private readonly ActionItemProcessor _processor;

        public ActionProcessor(ActionItemProcessor processor)
        {
            _processor = processor;
        }

        internal delegate void ActionItemProcessor(TagItem<T> item);

        public void Process(TagItem<T> item)
        {
            _processor(item);
        }
    }
}
