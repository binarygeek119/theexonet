using System.Threading.Tasks;
using Rava.Core.Dtos;
using Rava.Core.Events;
using Rava.Networking;
using UnityEngine;

namespace Rava.Mining
{
    public class GameSession : MonoBehaviour
    {
        [SerializeField] private ApiClient apiClient;

        public ApiClient Api => apiClient;
        public string PlayerId { get; private set; }
        public string MineId { get; private set; }
        public string Username { get; private set; }
        public MineDetailResponse CurrentMine { get; private set; }
        public FinanceResponse CurrentFinances { get; private set; }
        public MarketTodayResponse CurrentMarket { get; private set; }

        private void Awake()
        {
            if (apiClient == null)
            {
                apiClient = GetComponent<ApiClient>();
            }
        }

        public void Initialize(ApiClient client)
        {
            apiClient = client;
        }

        public async Task<AuthResponse> RegisterAsync(string username, string email, string password)
        {
            var response = await apiClient.RegisterAsync(username, email, password);
            ApplyAuth(response);
            await RefreshAllAsync();
            return response;
        }

        public async Task<AuthResponse> LoginAsync(string username, string password)
        {
            var response = await apiClient.LoginAsync(username, password);
            ApplyAuth(response);
            await RefreshAllAsync();
            return response;
        }

        public void Logout()
        {
            apiClient.ClearToken();
            PlayerId = null;
            MineId = null;
            Username = null;
            CurrentMine = null;
            CurrentFinances = null;
            CurrentMarket = null;
            GameEvents.RaiseLoggedOut();
        }

        public async Task RefreshMineAsync()
        {
            CurrentMine = await apiClient.GetMineAsync(MineId);
            GameEvents.RaiseMineUpdated(CurrentMine);
        }

        public async Task RefreshFinancesAsync()
        {
            CurrentFinances = await apiClient.GetFinancesAsync();
            GameEvents.RaiseFinancesUpdated(CurrentFinances);
        }

        public async Task RefreshMarketAsync()
        {
            CurrentMarket = await apiClient.GetMarketTodayAsync();
            GameEvents.RaiseMarketUpdated(CurrentMarket);
        }

        public async Task RefreshAllAsync()
        {
            await RefreshMineAsync();
            await RefreshFinancesAsync();
            await RefreshMarketAsync();
        }

        public async Task AssignWorkerAsync(string workerId, string zoneId)
        {
            await apiClient.AssignWorkerAsync(MineId, workerId, zoneId);
            await RefreshAllAsync();
        }

        public async Task UnassignWorkerAsync(string workerId)
        {
            await apiClient.UnassignWorkerAsync(MineId, workerId);
            await RefreshAllAsync();
        }

        public async Task BuySupplyAsync(SupplyTypeDto supplyType, float quantity)
        {
            await apiClient.BuySupplyAsync(MineId, supplyType, quantity);
            await RefreshAllAsync();
        }

        public async Task SellOreAsync(OreTypeDto oreType, float quantity, bool emergency = false)
        {
            await apiClient.SellOreAsync(MineId, oreType, quantity, emergency);
            await RefreshAllAsync();
        }

        public async Task<DayAdvanceResponse> AdvanceDayAsync()
        {
            var result = await apiClient.AdvanceDayAsync();
            await RefreshAllAsync();
            GameEvents.RaiseDayAdvanced(result);
            return result;
        }

        private void ApplyAuth(AuthResponse response)
        {
            apiClient.SetToken(response.token);
            PlayerId = response.playerId;
            MineId = response.mineId;
            Username = response.username;
        }
    }
}
