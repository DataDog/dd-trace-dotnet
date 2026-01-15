namespace Samples.MassTransit.Contracts;

public record GettingStartedMessage
{
    public string Value { get; init; } = string.Empty;
}
