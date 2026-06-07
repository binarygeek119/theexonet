namespace Theexonet.Core.Constants;

public static class CompanyNameFormats
{
    public const int MinLength = 3;
    public const int MaxLength = 48;
    public const int LimboDays = 30;
    public const decimal MinListingPrice = 1m;
    public const decimal MaxListingPrice = 1_000_000m;
    public const string DefaultStarterName = "Starter Claim Alpha";

    public const string ValidationMessage =
        "Company names must be 3–48 characters and use letters, numbers, spaces, and . & - ' only.";
}
