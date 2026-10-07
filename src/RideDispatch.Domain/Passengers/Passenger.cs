namespace RideDispatch.Domain.Passengers;

public sealed class Passenger
{
    private Passenger()
    {
        Name = null!;
        PhoneNumber = null!;
    }

    private Passenger(Guid id, string name, string phoneNumber)
    {
        Id = id;
        Name = name;
        PhoneNumber = phoneNumber;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string PhoneNumber { get; private set; }

    public static Passenger Create(string name, string phoneNumber)
    {
        var normalizedName = NormalizeRequired(name, 200, nameof(name), "Passenger name");
        var normalizedPhoneNumber = NormalizeRequired(
            phoneNumber,
            32,
            nameof(phoneNumber),
            "Passenger phone number");

        return new Passenger(Guid.CreateVersion7(), normalizedName, normalizedPhoneNumber);
    }

    private static string NormalizeRequired(
        string value,
        int maximumLength,
        string parameterName,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            throw new ArgumentException($"{displayName} must not be empty or whitespace.", parameterName);
        }

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{displayName} must not exceed {maximumLength} characters.",
                parameterName);
        }

        return normalized;
    }
}
