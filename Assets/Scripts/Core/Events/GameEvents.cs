using System;
using Rava.Core.Dtos;

namespace Rava.Core.Events
{
public static class GameEvents
{
    public static event Action<MineDetailResponse> OnMineUpdated;
    public static event Action<FinanceResponse> OnFinancesUpdated;
    public static event Action<MarketTodayResponse> OnMarketUpdated;
    public static event Action<DayAdvanceResponse> OnDayAdvanced;
    public static event Action<string> OnBirthdayBonus;
    public static event Action<string> OnError;
    public static event Action OnLoggedOut;

    public static void RaiseMineUpdated(MineDetailResponse mine) => OnMineUpdated?.Invoke(mine);
    public static void RaiseFinancesUpdated(FinanceResponse finances) => OnFinancesUpdated?.Invoke(finances);
    public static void RaiseMarketUpdated(MarketTodayResponse market) => OnMarketUpdated?.Invoke(market);
    public static void RaiseDayAdvanced(DayAdvanceResponse result) => OnDayAdvanced?.Invoke(result);
    public static void RaiseBirthdayBonus(string message) => OnBirthdayBonus?.Invoke(message);
    public static void RaiseError(string message) => OnError?.Invoke(message);
    public static void RaiseLoggedOut() => OnLoggedOut?.Invoke();
}
}
