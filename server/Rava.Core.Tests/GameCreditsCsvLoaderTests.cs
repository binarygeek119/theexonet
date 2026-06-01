using Rava.Core.Configuration;
using Rava.Core.Constants;

namespace Rava.Core.Tests;

public class GameCreditsCsvLoaderTests
{
    [Fact]
    public void Parse_ReadsAllSettings()
    {
        const string csv = """
            Setting,Amount,Description
            SignUp,6000,Sign up
            BirthdayBonus,750,Birthday
            CompanyNameReclaimFee,3000,Reclaim
            """;

        var values = GameCreditsCsvLoader.Parse(csv);

        Assert.Equal(6000m, values.SignUp);
        Assert.Equal(750m, values.BirthdayBonus);
        Assert.Equal(3000m, values.CompanyNameReclaimFee);
    }

    [Fact]
    public void Parse_IgnoresCommentsAndUsesDefaultsForMissingRows()
    {
        const string csv = """
            Setting,Amount,Description
            # comment
            SignUp,1000,Sign up
            """;

        var values = GameCreditsCsvLoader.Parse(csv);

        Assert.Equal(1000m, values.SignUp);
        Assert.Equal(GameCredits.BirthdayBonus, values.BirthdayBonus);
        Assert.Equal(GameCredits.CompanyNameReclaimFee, values.CompanyNameReclaimFee);
    }

    [Fact]
    public void SaveToFile_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rava-credits-{Guid.NewGuid():N}.csv");
        try
        {
            var original = new GameCreditsValues
            {
                SignUp = 4321m,
                BirthdayBonus = 123m,
                CompanyNameReclaimFee = 987m,
            };

            GameCreditsCsvLoader.SaveToFile(path, original);
            var loaded = GameCreditsCsvLoader.LoadFromFile(path);

            Assert.Equal(original, loaded);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
