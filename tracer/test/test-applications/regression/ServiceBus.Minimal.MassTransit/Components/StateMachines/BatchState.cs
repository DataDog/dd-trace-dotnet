namespace ServiceBus.Minimal.MassTransit.Components.StateMachines
{
    using System;
    using System.Collections.Generic;
    using Automatonymous;
    using Contracts.Enums;


    public class BatchState :
        SagaStateMachineInstance
    {
        public Guid CorrelationId { get; set; }

        public string CurrentState { get; set; }

        public DateTime? ReceiveTimestamp { get; set; }

        public DateTime? CreateTimestamp { get; set; }

        public DateTime? UpdateTimestamp { get; set; }

        public BatchAction? Action { get; set; }

        /// <summary>
        /// The maximum amount of active Jobs allowed to be processing. Typically an amount larger than your Job Consumer can handle concurrently, to allow for some additional prefetch while the Batch Saga dispatches more
        /// </summary>
        public int? ActiveThreshold { get; set; } = 20;

        public int? Total { get; set; }

        public Guid? ScheduledId { get; set; }

        public Stack<Guid> UnprocessedOrderIds { get; set; } = new Stack<Guid>();

        public Dictionary<Guid, Guid> ProcessingOrderIds { get; set; } = new Dictionary<Guid, Guid>(); // CorrelationId, OrderId

        // Navigation Properties
        public List<JobState> Jobs { get; set; } = new List<JobState>();
    }
}