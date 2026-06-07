namespace Theexonet.Core.Services;

public static class ProfileNumberGenerator
{
    // Readable sci-fi charset (no I/O to avoid confusion with 1/0).
    private const string Alphanumeric = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    /// <summary>Generates a callsign-style ID like !K7R-8842-9F3A.</summary>
    public static string Generate()
    {
        var sector = RandomBlock(3);
        var node = Random.Shared.Next(1000, 10000);
        var channel = RandomBlock(4);
        return $"!{sector}-{node}-{channel}";
    }

    private static string RandomBlock(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = Alphanumeric[Random.Shared.Next(Alphanumeric.Length)];
        }

        return new string(chars);
    }
}
