using Rava.Core.Config;
using Rava.Mining;
using Rava.Networking;
using Rava.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rava.Game
{
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameContentConfig contentConfig;
        [SerializeField] private string apiBaseUrl = "http://localhost:5000";

        private GameSession _session;
        private GameObject _gameRoot;

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
            var loginPanel = loginGo.AddComponent<LoginPanel>();
            UIFactory.Stretch(loginGo.AddComponent<RectTransform>());
            loginPanel.Initialize(_session, root.transform);

            var mineMapGo = new GameObject("MineMapUI");
            mineMapGo.transform.SetParent(_gameRoot.transform, false);
            var mineMap = mineMapGo.AddComponent<MineMapUI>();
            UIFactory.Stretch(mineMapGo.AddComponent<RectTransform>());
            mineMap.Initialize(_session, contentConfig, _gameRoot.transform);

            var financeGo = new GameObject("FinancePanel");
            financeGo.transform.SetParent(_gameRoot.transform, false);
            var financePanel = financeGo.AddComponent<FinancePanel>();
            UIFactory.Stretch(financeGo.AddComponent<RectTransform>());
            financePanel.Initialize(_session, contentConfig, _gameRoot.transform);

            var supplyGo = new GameObject("SupplyShopPanel");
            supplyGo.transform.SetParent(_gameRoot.transform, false);
            var supplyPanel = supplyGo.AddComponent<SupplyShopPanel>();
            UIFactory.Stretch(supplyGo.AddComponent<RectTransform>());
            supplyPanel.Initialize(_session, contentConfig, _gameRoot.transform);

            var hudGo = new GameObject("GameHud");
            hudGo.transform.SetParent(_gameRoot.transform, false);
            var hud = hudGo.AddComponent<GameHud>();
            UIFactory.Stretch(hudGo.AddComponent<RectTransform>());
            hud.Initialize(_session, financePanel, supplyPanel, _gameRoot.transform);

            _gameRoot.SetActive(false);
            loginPanel.OnLoginSuccess += () => _gameRoot.SetActive(true);

            Camera.main!.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
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
            es.AddComponent<StandaloneInputModule>();
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
