namespace Samples.MassTransit7.Contracts;

public record GettingStartedMessage
{
    public string Value { get; set; } = string.Empty;
}

public record FailingMessage
{
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Message for testing handler-based exception handling (uses Handler instead of Consumer)
/// </summary>
public record HandlerFailingMessage
{
    public string Value { get; set; } = string.Empty;
}
