namespace ServiceBus.Minimal.MassTransit.Components.StateMachines
{
    using System;
    using System.Threading.Tasks;
    using Automatonymous;
    using Contracts;
    using GreenPipes;
    using global::MassTransit;
    using global::MassTransit.Definition;


    public class JobStateMachine :
        MassTransitStateMachine<JobState>
    {
        public JobStateMachine()
        {
            InstanceState(x => x.CurrentState);

            Event(() => BatchJobReceived, x => x.CorrelateById(c => c.Message.BatchJobId));
            Event(() => BatchJobCompleted, x => x.CorrelateById(c => c.Message.BatchJobId));
            Event(() => BatchJobFailed, x => x.CorrelateById(c => c.Message.BatchJobId));

            Initially(
                When(BatchJobReceived)
                    .Then(context => Touch(context.Instance, context.Data.Timestamp))
                    .Then(context => SetReceiveTimestamp(context.Instance, context.Data.Timestamp))
                    .Then(Initialize)
                    .ThenAsync(InitiateProcessing)
                    .TransitionTo(Received));

            During(Received,
                When(BatchJobCompleted)
                    .Then(context => Touch(context.Instance, context.Data.Timestamp))
                    .PublishAsync(context => context.Init<BatchJobDone>(new
                    {
                        BatchJobId = context.Instance.CorrelationId,
                        context.Instance.BatchId,
                        InVar.Timestamp
                    }))
                    .TransitionTo(Completed),
                When(BatchJobFailed)
                    .Then(context => Touch(context.Instance, context.Data.Timestamp))
                    .Then(context => context.Instance.ExceptionMessage = context.Data.ExceptionInfo.Message)
                    .PublishAsync(context => context.Init<BatchJobDone>(new
                    {
                        BatchJobId = context.Instance.CorrelationId,
                        context.Instance.BatchId,
                        InVar.Timestamp
                    }))
                    .TransitionTo(Failed));
        }

        public State Received { get; private set; }
        public State Completed { get; private set; }
        public State Failed { get; private set; }

        public Event<BatchJobReceived> BatchJobReceived { get; private set; }
        public Event<BatchJobFailed> BatchJobFailed { get; private set; }
        public Event<BatchJobCompleted> BatchJobCompleted { get; private set; }

        static void Touch(JobState state, DateTime timestamp)
        {
            state.CreateTimestamp ??= timestamp;

            if (!state.UpdateTimestamp.HasValue || state.UpdateTimestamp.Value < timestamp)
                state.UpdateTimestamp = timestamp;
        }

        static void SetReceiveTimestamp(JobState state, DateTime timestamp)
        {
            if (!state.ReceiveTimestamp.HasValue || state.ReceiveTimestamp.Value > timestamp)
                state.ReceiveTimestamp = timestamp;
        }

        static void Initialize(BehaviorContext<JobState, BatchJobReceived> context)
        {
            InitializeInstance(context.Instance, context.Data);
        }

        static void InitializeInstance(JobState instance, BatchJobReceived data)
        {
            instance.Action = data.Action;
            instance.OrderId = data.OrderId;
            instance.BatchId = data.BatchId;
        }

        static async Task InitiateProcessing(BehaviorContext<JobState, BatchJobReceived> context)
        {
            await context.Send<JobState, BatchJobReceived, ProcessBatchJob>(new
            {
                BatchJobId = context.Instance.CorrelationId,
                Timestamp = DateTime.UtcNow,
                context.Instance.BatchId,
                context.Instance.OrderId,
                context.Instance.Action
            });
        }
    }


    public class JobStateMachineDefinition :
        SagaDefinition<JobState>
    {
        public JobStateMachineDefinition()
        {
            ConcurrentMessageLimit = 8;
        }

        protected override void ConfigureSaga(IReceiveEndpointConfigurator endpointConfigurator, ISagaConfigurator<JobState> sagaConfigurator)
        {
            sagaConfigurator.UseMessageRetry(r => r.Immediate(5));
            sagaConfigurator.UseInMemoryOutbox();
        }
    }
}