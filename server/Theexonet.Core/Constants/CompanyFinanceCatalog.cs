using Theexonet.Core.Enums;

namespace Theexonet.Core.Constants;

public static class CompanyFinanceCatalog
{
    public static readonly ReserveTransactionType[] CompanyActivityTypes =
    [
        ReserveTransactionType.MinePayroll,
        ReserveTransactionType.CompanyTax,
        ReserveTransactionType.HealthInsurance,
        ReserveTransactionType.JobInsurance,
        ReserveTransactionType.BeltFee,
        ReserveTransactionType.MiningRights,
        ReserveTransactionType.HireFee,
        ReserveTransactionType.LayoffSeverance,
        ReserveTransactionType.FireSeverance,
    ];
}
