namespace ServiceBus.Minimal.MassTransit.Components.StateMachines
{
    using System;
    using System.Collections.Generic;
    using Common;
    using Contracts.Enums;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using Microsoft.EntityFrameworkCore.Storage.ValueConversion;


    class BatchStateEntityConfiguration :
        IEntityTypeConfiguration<BatchState>
    {
        public void Configure(EntityTypeBuilder<BatchState> builder)
        {
            builder.HasKey(c => c.CorrelationId);

            builder.Property(c => c.CorrelationId)
                .ValueGeneratedNever()
                .HasColumnName("BatchId");

            builder.Property(c => c.CurrentState).IsRequired();

            builder.Property(c => c.Action)
                .HasConversion(new EnumToStringConverter<BatchAction>());

            builder.Property(c => c.UnprocessedOrderIds)
                .HasConversion(new JsonValueConverter<Stack<Guid>>())
                .Metadata.SetValueComparer(new JsonValueComparer<Stack<Guid>>());

            builder.Property(c => c.ProcessingOrderIds)
                .HasConversion(new JsonValueConverter<Dictionary<Guid, Guid>>())
                .Metadata.SetValueComparer(new JsonValueComparer<Dictionary<Guid, Guid>>());
        }
    }
}