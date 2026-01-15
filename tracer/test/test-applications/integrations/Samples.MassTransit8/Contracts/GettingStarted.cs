namespace Samples.MassTransit8.Contracts;

public record GettingStartedMessage
{
    public string Value { get; init; } = string.Empty;
}
