using System;
using System.Text;
using System.Threading.Tasks;
using Rava.Core.Dtos;
using Rava.Core.Enums;
using UnityEngine;
using UnityEngine.Networking;

namespace Rava.Networking
{
    public class ApiClient : MonoBehaviour
    {
        [SerializeField] private string baseUrl = "http://localhost:5000";

        private string _authToken;

        public string BaseUrl
        {
            get => baseUrl;
            set => baseUrl = value.TrimEnd('/');
        }

        public bool IsAuthenticated => !string.IsNullOrEmpty(_authToken);

        public void SetToken(string token) => _authToken = token;

        public void ClearToken() => _authToken = null;

        public Task<AuthResponse> RegisterAsync(string username, string email, string password, string birthday)
        {
            var body = JsonUtility.ToJson(new RegisterRequest
            {
                username = username,
                email = email,
                password = password,
                birthday = birthday
            });
            return PostAsync<AuthResponse>("/api/auth/register", body, authenticated: false);
        }

        public Task<AuthResponse> LoginAsync(string username, string password)
        {
            var body = JsonUtility.ToJson(new LoginRequest { username = username, password = password });
            return PostAsync<AuthResponse>("/api/auth/login", body, authenticated: false);
        }

        public Task<MessageResponse> ForgotPasswordAsync(string email)
        {
            var body = JsonUtility.ToJson(new ForgotPasswordRequest { email = email });
            return PostAsync<MessageResponse>("/api/auth/forgot-password", body, authenticated: false);
        }

        public Task<MessageResponse> ResetPasswordAsync(string token, string newPassword)
        {
            var body = JsonUtility.ToJson(new ResetPasswordRequest { token = token, newPassword = newPassword });
            return PostAsync<MessageResponse>("/api/auth/reset-password", body, authenticated: false);
        }

        public Task<MineDetailResponse> GetMineAsync(string mineId) =>
            GetAsync<MineDetailResponse>($"/api/mines/{mineId}");

        public Task<ActionResponse> AssignWorkerAsync(string mineId, string workerId, string zoneId)
        {
            var body = JsonUtility.ToJson(new AssignWorkerRequest { workerId = workerId, zoneId = zoneId });
            return PostAsync<ActionResponse>($"/api/mines/{mineId}/assign-worker", body);
        }

        public Task<ActionResponse> UnassignWorkerAsync(string mineId, string workerId)
        {
            var body = JsonUtility.ToJson(new AssignWorkerRequest { workerId = workerId, zoneId = null });
            return PostAsync<ActionResponse>($"/api/mines/{mineId}/assign-worker", body);
        }

        public Task<ActionResponse> BuySupplyAsync(string mineId, SupplyTypeDto supplyType, float quantity)
        {
            var body = JsonUtility.ToJson(new BuySupplyRequest { supplyType = supplyType, quantity = quantity });
            return PostAsync<ActionResponse>($"/api/mines/{mineId}/buy-supply", body);
        }

        public Task<ActionResponse> SellOreAsync(string mineId, OreTypeDto oreType, float quantity, bool emergencyBuyback = false)
        {
            var body = JsonUtility.ToJson(new SellOreRequest
            {
                oreType = oreType,
                quantity = quantity,
                emergencyBuyback = emergencyBuyback
            });
            return PostAsync<ActionResponse>($"/api/mines/{mineId}/sell-ore", body);
        }

        public Task<DayAdvanceResponse> AdvanceDayAsync() =>
            PostAsync<DayAdvanceResponse>("/api/game/advance-day", "{}");

        public Task<MarketTodayResponse> GetMarketTodayAsync() =>
            GetAsync<MarketTodayResponse>("/api/market/today");

        public Task<FinanceResponse> GetFinancesAsync() =>
            GetAsync<FinanceResponse>("/api/player/finances");

        public Task<PlayerProfileResponse> GetProfileAsync() =>
            GetAsync<PlayerProfileResponse>("/api/player/profile");

        public Task<PlayerProfileResponse> UpdateProfileAsync(UpdatePlayerProfileRequest request)
        {
            var body = JsonUtility.ToJson(request);
            return PutAsync<PlayerProfileResponse>("/api/player/profile", body);
        }

        public Task<PlayerProfileResponse> UploadProfileAvatarAsync(byte[] fileBytes, string fileName, string contentType)
        {
            var form = new WWWForm();
            form.AddBinaryData("file", fileBytes, fileName, contentType);
            return PostFormAsync<PlayerProfileResponse>("/api/player/profile/avatar", form);
        }

        public Task<FriendsListResponse> GetFriendsAsync() =>
            GetAsync<FriendsListResponse>("/api/player/friends");

        public Task<FriendActionResponse> AddFriendAsync(string profileNumber)
        {
            var body = JsonUtility.ToJson(new AddFriendRequest { profileNumber = profileNumber });
            return PostAsync<FriendActionResponse>("/api/player/friends", body);
        }

        public Task<FriendActionResponse> AcceptFriendAsync(string friendshipId) =>
            PostAsync<FriendActionResponse>($"/api/player/friends/{friendshipId}/accept", "{}");

        public Task<FriendActionResponse> RemoveFriendAsync(string friendshipId) =>
            SendAsync<FriendActionResponse>($"/api/player/friends/{friendshipId}", UnityWebRequest.kHttpVerbDELETE, null, true);

        private Task<T> PutAsync<T>(string path, string jsonBody) where T : class =>
            SendAsync<T>(path, UnityWebRequest.kHttpVerbPUT, jsonBody, true);

        private Task<T> GetAsync<T>(string path) where T : class =>
            SendAsync<T>(path, UnityWebRequest.kHttpVerbGET, null, true);

        private Task<T> PostAsync<T>(string path, string jsonBody, bool authenticated = true) where T : class =>
            SendAsync<T>(path, UnityWebRequest.kHttpVerbPOST, jsonBody, authenticated);

        private Task<T> PostFormAsync<T>(string path, WWWForm form) where T : class =>
            SendFormAsync<T>(path, form);

        private async Task<T> SendFormAsync<T>(string path, WWWForm form) where T : class
        {
            var url = baseUrl + path;
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(form.data)
            {
                contentType = form.headers["Content-Type"]
            };
            request.downloadHandler = new DownloadHandlerBuffer();

            foreach (var header in form.headers)
            {
                if (header.Key != "Content-Type")
                {
                    request.SetRequestHeader(header.Key, header.Value);
                }
            }

            if (!string.IsNullOrEmpty(_authToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {_authToken}");
            }

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                var message = TryParseError(request.downloadHandler.text) ?? FormatRequestError(request);
                throw new ApiException((int)request.responseCode, message);
            }

            return UnityJson.FromJson<T>(request.downloadHandler.text);
        }

        private async Task<T> SendAsync<T>(string path, string method, string jsonBody, bool authenticated) where T : class
        {
            var url = baseUrl + path;
            using var request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();

            if (!string.IsNullOrEmpty(jsonBody))
            {
                var bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            if (authenticated && !string.IsNullOrEmpty(_authToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {_authToken}");
            }

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                var errorBody = request.downloadHandler.text;
                var message = TryParseError(errorBody) ?? FormatRequestError(request);
                throw new ApiException((int)request.responseCode, message);
            }

            var text = request.downloadHandler.text;
            if (typeof(T) == typeof(string))
            {
                return text as T;
            }

            return UnityJson.FromJson<T>(text);
        }

        private static string FormatRequestError(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                return "Cannot reach game server. Start the API (dotnet run --project Rava.Api) and check the URL.";
            }

            if (request.responseCode == 503)
            {
                return "Database unavailable. Check PostgreSQL connection settings on the server.";
            }

            if (request.responseCode >= 500)
            {
                return "Server error. Check API logs and database connection.";
            }

            return string.IsNullOrEmpty(request.error) ? "Request failed." : request.error;
        }

        private static string TryParseError(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return null;
            }

            try
            {
                var error = UnityJson.FromJson<ErrorResponse>(body);
                return error?.message;
            }
            catch
            {
                return body;
            }
        }
    }

    public class ApiException : Exception
    {
        public int StatusCode { get; }

        public ApiException(int statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
