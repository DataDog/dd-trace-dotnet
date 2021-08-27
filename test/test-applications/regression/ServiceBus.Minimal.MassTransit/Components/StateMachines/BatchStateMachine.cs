namespace ServiceBus.Minimal.MassTransit.Components.StateMachines
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Automatonymous;
    using Contracts;
    using global::MassTransit;
    using global::MassTransit.Definition;
    using GreenPipes;


    public class BatchStateMachine :
        MassTransitStateMachine<BatchState>
    {
        public BatchStateMachine()
        {
            InstanceState(x => x.CurrentState);

            Event(() => BatchReceived, x => x.CorrelateById(c => c.Message.BatchId));
            Event(() => BatchJobDone, x => x.CorrelateById(c => c.Message.BatchId));
            Event(() => CancelBatch, x => x.CorrelateById(c => c.Message.BatchId));
            Event(() => StateRequested, x =>
            {
                x.CorrelateById(c => c.Message.BatchId);
                x.OnMissingInstance(m => m.ExecuteAsync(context => context.RespondAsync<BatchNotFound>(new
                {
                    context.Message.BatchId,
                    InVar.Timestamp
                })));
            });

            Schedule(() => StartBatch, x => x.ScheduledId, x =>
            {
                x.Received = e => e.CorrelateById(context => context.Message.BatchId);
            });

            Initially(
                When(BatchReceived)
                    .Then(context => Touch(context.Instance, context.Data.Timestamp))
                    .Then(context => SetReceiveTimestamp(context.Instance, context.Data.Timestamp))
                    .Then(Initialize)
                    .IfElse(context => context.Data.DelayInSeconds.HasValue,
                        thenBinder => thenBinder
                            .Schedule(StartBatch, context => context.Init<StartBatch>(new {BatchId = context.Instance.CorrelationId}),
                                context => TimeSpan.FromSeconds(context.Data.DelayInSeconds.Value))
                            .TransitionTo(Received),
                        elseBinder => elseBinder
                            .ThenAsync(DispatchJobs)
                            .TransitionTo(Started)),
                When(CancelBatch)
                    .Then(context => Touch(context.Instance, context.Data.Timestamp))
                    .TransitionTo(Finished));

            During(Received,
                When(StartBatch.Received)
                    .ThenAsync(DispatchJobs)
                    .TransitionTo(Started),
                When(CancelBatch)
                    .Then(context => Touch(context.Instance, context.Data.Timestamp))
                    .Unschedule(StartBatch)
                    .TransitionTo(Finished));

            During(Started,
                When(BatchJobDone)
                    .Then(context => Touch(context.Instance, context.Data.Timestamp))
                    .Then(FinalizeJob)
                    .IfElse(context => context.Instance.UnprocessedOrderIds.Count == 0 && context.Instance.ProcessingOrderIds.Count == 0,
                        binder => binder
                            .TransitionTo(Finished),
                        binder => binder
                            .ThenAsync(DispatchJobs)),
                When(CancelBatch)
                    .Then(context => Touch(context.Instance, context.Data.Timestamp))
                    .TransitionTo(Cancelling));

            // We continue receiving Job Done events, but don't Dispatch any new jobs
            During(Cancelling,
                When(BatchJobDone)
                    .Then(context => Touch(context.Instance, context.Data.Timestamp))
                    .Then(FinalizeJob)
                    .If(context => context.Instance.ProcessingOrderIds.Count == 0,
                        binder => binder.TransitionTo(Finished)));

            During(Finished, Ignore(CancelBatch));

            DuringAny(
                When(StateRequested)
                    .RespondAsync(async x => await x.Init<BatchStatus>(new
                    {
                        BatchId = x.Instance.CorrelationId,
                        InVar.Timestamp,
                        ProcessingJobCount = x.Instance.ProcessingOrderIds.Count,
                        UnprocessedJobCount = x.Instance.UnprocessedOrderIds.Count,
                        State = (await this.GetState(x.Instance)).Name
                    })),
                When(BatchReceived)
                    .Then(context => Touch(context.Instance, context.Data.Timestamp))
                    .Then(context => SetReceiveTimestamp(context.Instance, context.Data.Timestamp))
                    .Then(Initialize));
        }

        public State Received { get; private set; }
        public State Started { get; private set; }
        public State Cancelling { get; private set; }
        public State Finished { get; private set; }

        public Event<BatchReceived> BatchReceived { get; private set; }
        public Schedule<BatchState, StartBatch> StartBatch { get; private set; }
        public Event<BatchJobDone> BatchJobDone { get; private set; }
        public Event<CancelBatch> CancelBatch { get; private set; }
        public Event<BatchStatusRequested> StateRequested { get; private set; }

        static void Touch(BatchState state, DateTime timestamp)
        {
            if (!state.CreateTimestamp.HasValue)
                state.CreateTimestamp = timestamp;

            if (!state.UpdateTimestamp.HasValue || state.UpdateTimestamp.Value < timestamp)
                state.UpdateTimestamp = timestamp;
        }

        static void SetReceiveTimestamp(BatchState state, DateTime timestamp)
        {
            if (!state.ReceiveTimestamp.HasValue || state.ReceiveTimestamp.Value > timestamp)
                state.ReceiveTimestamp = timestamp;
        }

        static void Initialize(BehaviorContext<BatchState, BatchReceived> context)
        {
            InitializeInstance(context.Instance, context.Data);
        }

        static void InitializeInstance(BatchState instance, BatchReceived data)
        {
            instance.Action = data.Action;
            instance.Total = data.OrderIds.Length;
            instance.UnprocessedOrderIds = new Stack<Guid>(data.OrderIds);
            instance.ActiveThreshold = data.ActiveThreshold;
        }

        static async Task DispatchJobs(BehaviorContext<BatchState> context)
        {
            var jobsToSend = new List<Task>();

            while (context.Instance.UnprocessedOrderIds.Any()
                && context.Instance.ProcessingOrderIds.Count < context.Instance.ActiveThreshold)
                jobsToSend.Add(InitiateJob(context));

            await Task.WhenAll(jobsToSend);
        }

        static Task InitiateJob(BehaviorContext<BatchState> context)
        {
            var orderId = context.Instance.UnprocessedOrderIds.Pop();
            var batchJobId = NewId.NextGuid();
            context.Instance.ProcessingOrderIds.Add(batchJobId, orderId);
            return context.GetPayload<ConsumeContext>().Publish<BatchJobReceived>(new
            {
                BatchJobId = batchJobId,
                InVar.Timestamp,
                BatchId = context.Instance.CorrelationId,
                OrderId = orderId,
                context.Instance.Action
            });
        }

        static void FinalizeJob(BehaviorContext<BatchState, BatchJobDone> context)
        {
            context.Instance.ProcessingOrderIds.Remove(context.Data.BatchJobId);
        }
    }


    public class BatchStateMachineDefinition :
        SagaDefinition<BatchState>
    {
        public BatchStateMachineDefinition()
        {
            ConcurrentMessageLimit = 8;
        }

        protected override void ConfigureSaga(IReceiveEndpointConfigurator endpointConfigurator, ISagaConfigurator<BatchState> sagaConfigurator)
        {
            sagaConfigurator.UseMessageRetry(r => r.Immediate(5));
            sagaConfigurator.UseInMemoryOutbox();

            var partition = endpointConfigurator.CreatePartitioner(8);

            sagaConfigurator.Message<BatchJobDone>(x => x.UsePartitioner(partition, m => m.Message.BatchId));
            sagaConfigurator.Message<BatchReceived>(x => x.UsePartitioner(partition, m => m.Message.BatchId));
            sagaConfigurator.Message<CancelBatch>(x => x.UsePartitioner(partition, m => m.Message.BatchId));
        }
    }
}