using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.Tagging
{
    internal class MsmqTags : InstrumentationTags
    {
        protected static readonly IProperty<string>[] MsmqTagsProperties =
           InstrumentationTagsProperties.Concat(
               new Property<MsmqTags, string>(Trace.Tags.InstrumentationName, t => t.InstrumentationName, (t, v) => t.InstrumentationName = v),
               new Property<MsmqTags, string>(Trace.Tags.MsmqQueue, t => t.Queue, (t, v) => t.Queue = v),
               new Property<MsmqTags, string>(Trace.Tags.MsmqQueueLabel, t => t.QueueLabel, (t, v) => t.QueueLabel = v),
               new Property<MsmqTags, string>(Trace.Tags.MsmqQueueLastModifiedTime, t => t.QueueLastModifiedTime, (t, v) => t.QueueLastModifiedTime = v),
               new Property<MsmqTags, string>(Trace.Tags.MsmqQueueUniqueName, t => t.UniqueQueueName, (t, v) => t.UniqueQueueName = v),
               new Property<MsmqTags, string>(Trace.Tags.MsmqIsTransactionalQueue, t => t.IsTransactionalQueue, (t, v) => t.IsTransactionalQueue = v),
               new Property<MsmqTags, string>(Trace.Tags.MsmqTransactionType, t => t.TransactionType, (t, v) => t.TransactionType = v));

        /// <summary>
        /// Initializes a new instance of the <see cref="MsmqTags"/> class.
        /// </summary>
        /// <param name="spanKind">kind of span</param>
        public MsmqTags(string spanKind) => SpanKind = spanKind;

        /// <inheritdoc/>
        public override string SpanKind { get; }

        public string Queue { get; set; }

        public string QueueLabel { get; set; }

        public string QueueLastModifiedTime { get; set; }

        public string UniqueQueueName { get; set; }

        public string TransactionType { get; set; }

        public string IsTransactionalQueue { get; set; }

        public string InstrumentationName { get; set; }

        protected override IProperty<string>[] GetAdditionalTags() => MsmqTagsProperties;
    }
}
