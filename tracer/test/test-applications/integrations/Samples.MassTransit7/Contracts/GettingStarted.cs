namespace Samples.MassTransit7.Contracts;

public record GettingStartedMessage
{
    public string Value { get; init; } = string.Empty;
}
