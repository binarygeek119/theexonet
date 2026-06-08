using Theexonet.Core.Constants;
using Theexonet.Core.Services;

namespace Theexonet.Core.Tests;

public class CompanyObligationsCalculatorTests
{
    [Fact]
    public void ComputePreview_ScalesWithWorkerCount()
    {
        var threeWorkers = CompanyObligationsCalculator.ComputePreview(3, 500m, 10, 40);
        var fiveWorkers = CompanyObligationsCalculator.ComputePreview(5, 500m, 10, 40);

        Assert.True(fiveWorkers.HealthInsurance > threeWorkers.HealthInsurance);
        Assert.True(fiveWorkers.JobInsurance > threeWorkers.JobInsurance);
        Assert.Equal(GameBalance.BeltOperatingFee, threeWorkers.BeltFee);
    }

    [Fact]
    public void ComputePreview_AppliesTaxOnEstimatedIncome()
    {
        var result = CompanyObligationsCalculator.ComputePreview(2, 250m, 5, 30);

        Assert.Equal(Math.Round(250m * GameBalance.CompanyTaxRate, 2), result.CompanyTax);
    }

    [Fact]
    public void ComputeDaily_WhenExpired_AttemptsRenewal()
    {
        var result = CompanyObligationsCalculator.ComputeDaily(2, 100m, 50, 40, attemptAutoRenewal: true);

        Assert.True(result.MiningRightsRenewed);
        Assert.Equal(80, result.NewPaidThroughDay);
        Assert.True(result.MiningRights >= GameBalance.MiningRightsRenewalFee);
    }
}
