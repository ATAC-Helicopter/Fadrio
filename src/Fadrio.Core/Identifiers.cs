namespace Fadrio.Core;

public readonly record struct ApplicationId
{
    public ApplicationId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct AudioSessionId
{
    public AudioSessionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct DeviceId
{
    public DeviceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
    public override string ToString() => Value;
}
