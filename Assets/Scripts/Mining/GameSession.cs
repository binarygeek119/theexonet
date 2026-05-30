using System.Threading.Tasks;
using Rava.Core.Dtos;
using Rava.Core.Enums;
using Rava.Core.Events;
using Rava.Networking;
using UnityEngine;

namespace Rava.Mining
{
    public class GameSession : MonoBehaviour
    {
        private const string TokenKey = "rava_token";
        private const string MineIdKey = "rava_mineId";
        private const string PlayerIdKey = "rava_playerId";
        private const string UsernameKey = "rava_username";

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

        public async Task RegisterAccountAsync(string username, string email, string password, string birthday)
        {
            await apiClient.RegisterAsync(username, email, password, birthday);
        }

        public async Task<AuthResponse> LoginAsync(string username, string password)
        {
            var response = await apiClient.LoginAsync(username, password);
            ApplyAuth(response);
            await RefreshAllAsync();
            return response;
        }

        public Task<MessageResponse> ForgotPasswordAsync(string email) =>
            apiClient.ForgotPasswordAsync(email);

        public Task<MessageResponse> ResetPasswordAsync(string token, string newPassword) =>
            apiClient.ResetPasswordAsync(token, newPassword);

        public void Logout()
        {
            apiClient.ClearToken();
            PlayerId = null;
            MineId = null;
            Username = null;
            CurrentMine = null;
            CurrentFinances = null;
            CurrentMarket = null;
            ClearStoredAuth();
            GameEvents.RaiseLoggedOut();
        }

        public async Task<bool> TryRestoreSessionAsync()
        {
            var token = PlayerPrefs.GetString(TokenKey, string.Empty);
            var mineId = PlayerPrefs.GetString(MineIdKey, string.Empty);
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(mineId))
            {
                return false;
            }

            apiClient.SetToken(token);
            PlayerId = PlayerPrefs.GetString(PlayerIdKey, string.Empty);
            MineId = mineId;
            Username = PlayerPrefs.GetString(UsernameKey, string.Empty);

            try
            {
                await RefreshAllAsync();
                return true;
            }
            catch
            {
                Logout();
                return false;
            }
        }

        public async Task RefreshMineAsync()
        {
            CurrentMine = await apiClient.GetMineAsync(MineId);
            GameEvents.RaiseMineUpdated(CurrentMine);
            if (CurrentMine.latestDayReport != null)
            {
                GameEvents.RaiseDayAdvanced(CurrentMine.latestDayReport);
            }

            if (!string.IsNullOrEmpty(CurrentMine.birthdayMessage))
            {
                GameEvents.RaiseBirthdayBonus(CurrentMine.birthdayMessage);
            }
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

        public Task<PlayerProfileResponse> LoadProfileAsync() =>
            apiClient.GetProfileAsync();

        public Task<PlayerProfileResponse> UpdateProfileAsync(UpdatePlayerProfileRequest request) =>
            apiClient.UpdateProfileAsync(request);

        public Task<PlayerProfileResponse> UploadProfileAvatarAsync(byte[] fileBytes, string fileName, string contentType) =>
            apiClient.UploadProfileAvatarAsync(fileBytes, fileName, contentType);

        public Task<FriendsListResponse> LoadFriendsAsync() =>
            apiClient.GetFriendsAsync();

        public Task<FriendActionResponse> AddFriendAsync(string profileNumber) =>
            apiClient.AddFriendAsync(profileNumber);

        public Task<FriendActionResponse> AcceptFriendAsync(string friendshipId) =>
            apiClient.AcceptFriendAsync(friendshipId);

        public Task<FriendActionResponse> RemoveFriendAsync(string friendshipId) =>
            apiClient.RemoveFriendAsync(friendshipId);

        private void ApplyAuth(AuthResponse response)
        {
            apiClient.SetToken(response.token);
            PlayerId = response.playerId;
            MineId = response.mineId;
            Username = response.username;
            SaveAuth(response);
        }

        private static void SaveAuth(AuthResponse response)
        {
            PlayerPrefs.SetString(TokenKey, response.token);
            PlayerPrefs.SetString(MineIdKey, response.mineId);
            PlayerPrefs.SetString(PlayerIdKey, response.playerId);
            PlayerPrefs.SetString(UsernameKey, response.username);
            PlayerPrefs.Save();
        }

        private static void ClearStoredAuth()
        {
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.DeleteKey(MineIdKey);
            PlayerPrefs.DeleteKey(PlayerIdKey);
            PlayerPrefs.DeleteKey(UsernameKey);
            PlayerPrefs.Save();
        }
    }
}
