using Rava.Core.Config;
using Rava.Core.Events;
using Rava.Mining;
using Rava.Networking;
using Rava.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Rava.Game
{
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameContentConfig contentConfig;
        [SerializeField] private string apiBaseUrl = "http://localhost:5000";

        private GameSession _session;
        private FinancePanel _financePanel;
        private SupplyShopPanel _supplyPanel;
        private StorePanel _storePanel;
        private ShippingPanel _shippingPanel;
        private ProfilePanel _profilePanel;
        private FriendsPanel _friendsPanel;
        private GameObject _gameRoot;
        private LoginPanel _loginPanel;

        private void Awake()
        {
            EnsureEventSystem();
            contentConfig = Resources.Load<GameContentConfig>("GameContentConfig");
            contentConfig ??= CreateDefaultContentConfig();

            var root = CreateCanvas();

            var apiGo = new GameObject("ApiClient");
            apiGo.transform.SetParent(transform);
            var apiClient = apiGo.AddComponent<ApiClient>();
            apiClient.BaseUrl = apiBaseUrl;

            var sessionGo = new GameObject("GameSession");
            sessionGo.transform.SetParent(transform);
            _session = sessionGo.AddComponent<GameSession>();
            _session.Initialize(apiClient);

            _gameRoot = new GameObject("GameUI");
            _gameRoot.transform.SetParent(root.transform, false);
            UIFactory.Stretch(_gameRoot.AddComponent<RectTransform>());

            var loginGo = new GameObject("LoginPanel");
            loginGo.transform.SetParent(root.transform, false);
            _loginPanel = loginGo.AddComponent<LoginPanel>();
            UIFactory.Stretch(loginGo.AddComponent<RectTransform>());
            _loginPanel.Initialize(_session);

            var mineMapGo = new GameObject("MineMapUI");
            mineMapGo.transform.SetParent(_gameRoot.transform, false);
            var mineMap = mineMapGo.AddComponent<MineMapUI>();
            UIFactory.Stretch(mineMapGo.AddComponent<RectTransform>());
            mineMap.Initialize(_session, contentConfig, _gameRoot.transform);

            var financeGo = new GameObject("FinancePanel");
            financeGo.transform.SetParent(_gameRoot.transform, false);
            _financePanel = financeGo.AddComponent<FinancePanel>();
            UIFactory.Stretch(financeGo.AddComponent<RectTransform>());
            _financePanel.Initialize(_session, contentConfig, _gameRoot.transform);

            var supplyGo = new GameObject("SupplyShopPanel");
            supplyGo.transform.SetParent(_gameRoot.transform, false);
            _supplyPanel = supplyGo.AddComponent<SupplyShopPanel>();
            UIFactory.Stretch(supplyGo.AddComponent<RectTransform>());
            _supplyPanel.Initialize(_session, contentConfig, _gameRoot.transform);

            var storeGo = new GameObject("StorePanel");
            storeGo.transform.SetParent(_gameRoot.transform, false);
            _storePanel = storeGo.AddComponent<StorePanel>();
            UIFactory.Stretch(storeGo.AddComponent<RectTransform>());
            _storePanel.Initialize(_session, contentConfig, _gameRoot.transform);

            var shippingGo = new GameObject("ShippingPanel");
            shippingGo.transform.SetParent(_gameRoot.transform, false);
            _shippingPanel = shippingGo.AddComponent<ShippingPanel>();
            UIFactory.Stretch(shippingGo.AddComponent<RectTransform>());
            _shippingPanel.Initialize(_session, contentConfig, _gameRoot.transform);

            var profileGo = new GameObject("ProfilePanel");
            profileGo.transform.SetParent(_gameRoot.transform, false);
            _profilePanel = profileGo.AddComponent<ProfilePanel>();
            UIFactory.Stretch(profileGo.AddComponent<RectTransform>());
            _profilePanel.Initialize(_session, _gameRoot.transform);

            var friendsGo = new GameObject("FriendsPanel");
            friendsGo.transform.SetParent(_gameRoot.transform, false);
            _friendsPanel = friendsGo.AddComponent<FriendsPanel>();
            UIFactory.Stretch(friendsGo.AddComponent<RectTransform>());
            _friendsPanel.Initialize(_session, _gameRoot.transform);

            var hudGo = new GameObject("GameHud");
            hudGo.transform.SetParent(_gameRoot.transform, false);
            var hud = hudGo.AddComponent<GameHud>();
            UIFactory.Stretch(hudGo.AddComponent<RectTransform>());
            hud.Initialize(_session, _financePanel, _supplyPanel, _storePanel, _shippingPanel, _profilePanel, _friendsPanel, _gameRoot.transform);

            _gameRoot.SetActive(false);
            _loginPanel.OnLoginSuccess += EnterGame;
            GameEvents.OnLoggedOut += ExitToLogin;

            Camera.main!.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
        }

        private void OnDestroy()
        {
            GameEvents.OnLoggedOut -= ExitToLogin;
        }

        private async void Start()
        {
            if (await _session.TryRestoreSessionAsync())
            {
                EnterGame();
            }
        }

        private void EnterGame()
        {
            _loginPanel.gameObject.SetActive(false);
            _gameRoot.SetActive(true);
        }

        private void ExitToLogin()
        {
            _financePanel.SetVisible(false);
            _supplyPanel.SetVisible(false);
            _storePanel.SetVisible(false);
            _shippingPanel.SetVisible(false);
            _profilePanel.SetVisible(false);
            _friendsPanel.SetVisible(false);
            _gameRoot.SetActive(false);
            _loginPanel.gameObject.SetActive(true);
        }

        private static Transform CreateCanvas()
        {
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvasGo.transform;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private static GameContentConfig CreateDefaultContentConfig()
        {
            var config = ScriptableObject.CreateInstance<GameContentConfig>();
            config.oreConfig = ScriptableObject.CreateInstance<OreTypeConfig>();
            config.supplyConfig = ScriptableObject.CreateInstance<SupplyTypeConfig>();
            return config;
        }
    }
}
