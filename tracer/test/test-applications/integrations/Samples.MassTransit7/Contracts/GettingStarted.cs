namespace Samples.MassTransit7.Contracts;

public class GettingStartedMessage
{
    public string Value { get; set; } = string.Empty;
}

public class FailingMessage
{
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Message for testing handler-based exception handling (uses Handler instead of Consumer)
/// </summary>
public class HandlerFailingMessage
{
    public string Value { get; set; } = string.Empty;
}
