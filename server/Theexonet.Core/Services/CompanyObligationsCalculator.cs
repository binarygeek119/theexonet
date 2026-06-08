using Theexonet.Core.Constants;
using Theexonet.Core.Models;

namespace Theexonet.Core.Services;

public static class CompanyObligationsCalculator
{
    public static CompanyObligationsResult ComputeDaily(
        int workerCount,
        decimal estimatedDailyIncome,
        int currentGameDay,
        int miningRightsPaidThroughDay,
        bool attemptAutoRenewal)
    {
        var result = new CompanyObligationsResult
        {
            CompanyTax = Math.Round(estimatedDailyIncome * GameBalance.CompanyTaxRate, 2),
            HealthInsurance = Math.Round(workerCount * GameBalance.HealthInsurancePerWorker, 2),
            JobInsurance = Math.Round(workerCount * GameBalance.JobInsurancePerWorker, 2),
            BeltFee = GameBalance.BeltOperatingFee,
        };

        var dailyAmortized = Math.Round(
            GameBalance.MiningRightsRenewalFee / GameBalance.MiningRightsPeriodDays,
            2);

        if (currentGameDay > miningRightsPaidThroughDay)
        {
            result.MiningRights += GameBalance.MiningRightsExpiredDailyPenalty;
            result.Messages.Add("Mining rights expired — belt penalty applied.");

            if (attemptAutoRenewal)
            {
                result.MiningRights += GameBalance.MiningRightsRenewalFee;
                result.MiningRightsRenewed = true;
                result.NewPaidThroughDay = currentGameDay + GameBalance.MiningRightsPeriodDays;
                result.Messages.Add($"Mining rights renewed through day {result.NewPaidThroughDay}.");
            }
        }
        else
        {
            result.MiningRights += dailyAmortized;
        }

        result.Total = result.CompanyTax
            + result.HealthInsurance
            + result.JobInsurance
            + result.BeltFee
            + result.MiningRights;

        return result;
    }

    public static CompanyObligationsResult ComputePreview(
        int workerCount,
        decimal estimatedDailyIncome,
        int currentGameDay,
        int miningRightsPaidThroughDay) =>
        ComputeDaily(workerCount, estimatedDailyIncome, currentGameDay, miningRightsPaidThroughDay, attemptAutoRenewal: false);
}
