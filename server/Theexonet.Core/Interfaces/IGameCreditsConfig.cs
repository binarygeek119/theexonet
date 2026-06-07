namespace Theexonet.Core.Interfaces;

public interface IGameCreditsConfig
{
    decimal SignUp { get; }
    decimal BirthdayBonus { get; }
    decimal CompanyNameReclaimFee { get; }
    void Reload();
}
