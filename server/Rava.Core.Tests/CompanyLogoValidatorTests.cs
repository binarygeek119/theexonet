using Rava.Core.Validation;

namespace Rava.Core.Tests;

public class CompanyLogoValidatorTests
{
    [Fact]
    public void Validate_rejects_non_png_content_type()
    {
        var png = MinimalPngWithColorType(6);
        var error = CompanyLogoValidator.Validate("image/jpeg", png);
        Assert.NotNull(error);
        Assert.Contains("PNG", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_rejects_opaque_rgb_png()
    {
        var png = MinimalPngWithColorType(2);
        var error = CompanyLogoValidator.Validate("image/png", png);
        Assert.NotNull(error);
        Assert.Contains("transparent", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_accepts_rgba_png()
    {
        var png = MinimalPngWithColorType(6);
        var error = CompanyLogoValidator.Validate("image/png", png);
        Assert.Null(error);
    }

    private static byte[] MinimalPngWithColorType(byte colorType)
    {
        var png = new byte[64];
        png[0] = 0x89;
        png[1] = (byte)'P';
        png[2] = (byte)'N';
        png[3] = (byte)'G';
        png[4] = 0x0D;
        png[5] = 0x0A;
        png[6] = 0x1A;
        png[7] = 0x0A;
        png[12] = (byte)'I';
        png[13] = (byte)'H';
        png[14] = (byte)'D';
        png[15] = (byte)'R';
        png[25] = colorType;
        return png;
    }
}
