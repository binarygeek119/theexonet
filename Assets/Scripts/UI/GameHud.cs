using System;
using System.Linq;
using Theexonet.Core.Dtos;
using Theexonet.Core.Events;
using Theexonet.Mining;
using UnityEngine;
using UnityEngine.UI;

namespace Theexonet.UI
{
    public class GameHud : MonoBehaviour
    {
        private GameSession _session;
        private Text _creditsText;
        private Image _raxIcon;
        private Text _dayText;
        private Text _utcText;
        private Text _statusText;
        private FinancePanel _financePanel;
        private SupplyShopPanel _supplyPanel;
        private StorePanel _storePanel;
        private ShippingPanel _shippingPanel;
        private ProfilePanel _profilePanel;
        private FriendsPanel _friendsPanel;
        private GameObject _messagePanel;
        private DateTime _nextDayBoundaryUtc;
        private float _nextRefreshTime;

        public void Initialize(
            GameSession session,
            FinancePanel financePanel,
            SupplyShopPanel supplyPanel,
            StorePanel storePanel,
            ShippingPanel shippingPanel,
            ProfilePanel profilePanel,
            FriendsPanel friendsPanel,
            Transform parent)
        {
            _session = session;
            _financePanel = financePanel;
            _supplyPanel = supplyPanel;
            _storePanel = storePanel;
            _shippingPanel = shippingPanel;
            _profilePanel = profilePanel;
            _friendsPanel = friendsPanel;

            const float sidebarWidth = 0.13f;

            var sidebar = UIFactory.CreatePanel(parent, "NavSidebar", new Color(0.05f, 0.07f, 0.1f, 0.98f));
            var sidebarRect = sidebar.GetComponent<RectTransform>();
            sidebarRect.anchorMin = new Vector2(0f, 0.08f);
            sidebarRect.anchorMax = new Vector2(sidebarWidth, 0.88f);
            sidebarRect.offsetMin = Vector2.zero;
            sidebarRect.offsetMax = Vector2.zero;

            var statsBar = UIFactory.CreatePanel(parent, "StatsBar", new Color(0.06f, 0.08f, 0.11f, 0.95f));
            var statsRect = statsBar.GetComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(sidebarWidth, 0.88f);
            statsRect.anchorMax = new Vector2(1f, 1f);
            statsRect.offsetMin = Vector2.zero;
            statsRect.offsetMax = Vector2.zero;

            _raxIcon = CurrencyFormat.CreateIcon(statsBar.transform, 20f);
            if (_raxIcon != null)
            {
                var iconRect = _raxIcon.rectTransform;
                iconRect.anchorMin = new Vector2(0.02f, 0.15f);
                iconRect.anchorMax = new Vector2(0.02f, 0.85f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(22f, 26f);
            }

            _creditsText = UIFactory.CreateText(
                statsBar.transform,
                "Rax",
                _raxIcon != null ? "---" : $"{CurrencyFormat.Name}: ---",
                18,
                TextAnchor.MiddleLeft);
            var creditsRect = _creditsText.rectTransform;
            creditsRect.anchorMin = new Vector2(_raxIcon != null ? 0.055f : 0.02f, 0.1f);
            creditsRect.anchorMax = new Vector2(0.28f, 0.9f);
            creditsRect.offsetMin = Vector2.zero;
            creditsRect.offsetMax = Vector2.zero;

            _dayText = UIFactory.CreateText(statsBar.transform, "Day", "Day: ---", 18, TextAnchor.MiddleLeft);
            var dayRect = _dayText.rectTransform;
            dayRect.anchorMin = new Vector2(0.28f, 0.1f);
            dayRect.anchorMax = new Vector2(0.42f, 0.9f);
            dayRect.offsetMin = Vector2.zero;
            dayRect.offsetMax = Vector2.zero;

            _utcText = UIFactory.CreateText(statsBar.transform, "UtcClock", "UTC ---", 14, TextAnchor.MiddleLeft);
            var utcRect = _utcText.rectTransform;
            utcRect.anchorMin = new Vector2(0.42f, 0.1f);
            utcRect.anchorMax = new Vector2(1f, 0.9f);
            utcRect.offsetMin = Vector2.zero;
            utcRect.offsetMax = Vector2.zero;
            _utcText.color = new Color(0.75f, 0.82f, 0.95f);

            var profileBtn = UIFactory.CreateButton(sidebar.transform, "ProfileTab", "Profile", new Color(0.25f, 0.3f, 0.55f));
            SetVerticalTabRect(profileBtn, 0.905f, 0.975f);
            profileBtn.onClick.AddListener(() => TogglePanel(_profilePanel));

            var financeBtn = UIFactory.CreateButton(sidebar.transform, "FinanceTab", "Finances", new Color(0.2f, 0.35f, 0.5f));
            SetVerticalTabRect(financeBtn, 0.815f, 0.885f);
            financeBtn.onClick.AddListener(() => TogglePanel(_financePanel));

            var friendsBtn = UIFactory.CreateButton(sidebar.transform, "FriendsTab", "Friends", new Color(0.25f, 0.35f, 0.45f));
            SetVerticalTabRect(friendsBtn, 0.725f, 0.795f);
            friendsBtn.onClick.AddListener(() => TogglePanel(_friendsPanel));

            var tradeMarketBtn = UIFactory.CreateButton(sidebar.transform, "TradeMarketTab", "Market", new Color(0.2f, 0.4f, 0.35f));
            SetVerticalTabRect(tradeMarketBtn, 0.635f, 0.705f);
            tradeMarketBtn.onClick.AddListener(() => TogglePanel(_supplyPanel));

            var storeBtn = UIFactory.CreateButton(sidebar.transform, "StoreTab", "Store", new Color(0.45f, 0.3f, 0.25f));
            SetVerticalTabRect(storeBtn, 0.545f, 0.615f);
            storeBtn.onClick.AddListener(() => TogglePanel(_storePanel));

            var shippingBtn = UIFactory.CreateButton(sidebar.transform, "ShippingTab", "Ship", new Color(0.35f, 0.32f, 0.5f));
            SetVerticalTabRect(shippingBtn, 0.455f, 0.525f);
            shippingBtn.onClick.AddListener(() => TogglePanel(_shippingPanel));

            var logoutBtn = UIFactory.CreateButton(sidebar.transform, "LogoutBtn", "Logout", new Color(0.35f, 0.2f, 0.2f));
            SetVerticalTabRect(logoutBtn, 0.055f, 0.125f);
            logoutBtn.onClick.AddListener(Logout);

            _statusText = UIFactory.CreateText(parent, "StatusBar", "", 14, TextAnchor.MiddleCenter);
            var statusRect = _statusText.rectTransform;
            statusRect.anchorMin = new Vector2(0.15f, 0.02f);
            statusRect.anchorMax = new Vector2(0.8f, 0.08f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            _statusText.color = new Color(1f, 0.75f, 0.5f);

            _messagePanel = UIFactory.CreatePanel(parent, "DayMessages", new Color(0.08f, 0.1f, 0.14f, 0.9f));
            var msgRect = _messagePanel.GetComponent<RectTransform>();
            msgRect.anchorMin = new Vector2(0.25f, 0.35f);
            msgRect.anchorMax = new Vector2(0.75f, 0.65f);
            msgRect.offsetMin = Vector2.zero;
            msgRect.offsetMax = Vector2.zero;
            _messagePanel.SetActive(false);

            GameEvents.OnMineUpdated += RefreshHud;
            GameEvents.OnDayAdvanced += ShowDayMessages;
            GameEvents.OnBirthdayBonus += ShowBirthdayMessage;
            GameEvents.OnError += ShowError;
        }

        private void OnDestroy()
        {
            GameEvents.OnMineUpdated -= RefreshHud;
            GameEvents.OnDayAdvanced -= ShowDayMessages;
            GameEvents.OnBirthdayBonus -= ShowBirthdayMessage;
            GameEvents.OnError -= ShowError;
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            UpdateUtcClock();

            if (Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.unscaledTime + 60f;
            _ = RefreshForUtcAsync();
        }

        private void RefreshHud(MineDetailResponse mine)
        {
            _creditsText.text = _raxIcon != null
                ? CurrencyFormat.FormatAmountOnly(mine.credits)
                : CurrencyFormat.FormatLabel(mine.credits);
            _dayText.text = $"Day {mine.currentGameDay}";

            if (!string.IsNullOrEmpty(mine.nextDayAtUtc)
                && System.DateTime.TryParse(
                    mine.nextDayAtUtc,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var nextDay))
            {
                _nextDayBoundaryUtc = nextDay.ToUniversalTime();
            }

            UpdateUtcClock();
        }

        private void UpdateUtcClock()
        {
            var utcNow = System.DateTime.UtcNow;
            var utcDate = _session.CurrentMine != null && !string.IsNullOrEmpty(_session.CurrentMine.utcDate)
                ? _session.CurrentMine.utcDate
                : utcNow.ToString("yyyy-MM-dd");
            var remaining = _nextDayBoundaryUtc - utcNow;
            if (remaining.TotalSeconds < 0)
            {
                remaining = System.TimeSpan.Zero;
            }

            _utcText.text = $"UTC {utcDate} · next day in {remaining:hh\\:mm\\:ss}";
        }

        private async System.Threading.Tasks.Task RefreshForUtcAsync()
        {
            try
            {
                await _session.RefreshAllAsync();
            }
            catch (Networking.ApiException ex)
            {
                _statusText.text = ex.Message;
            }
        }

        private void ShowDayMessages(DayAdvanceResponse result)
        {
            foreach (Transform child in _messagePanel.transform)
            {
                if (child.name != "Image")
                {
                    Destroy(child.gameObject);
                }
            }

            var title = UIFactory.CreateText(_messagePanel.transform, "MsgTitle",
                $"Day {result.newGameDay} Report", 20, TextAnchor.UpperCenter);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.05f, 0.8f);
            titleRect.anchorMax = new Vector2(0.95f, 0.95f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            var body = result.messages != null ? string.Join("\n", result.messages) : "Day complete.";
            if (result.oreExtracted != null && result.oreExtracted.Length > 0)
            {
                body += "\n\nExtracted: " + string.Join(", ",
                    result.oreExtracted.Select(o => $"{o.quantity:F1} {o.oreType}"));
            }

            var msg = UIFactory.CreateText(_messagePanel.transform, "MsgBody", body, 15, TextAnchor.UpperLeft);
            var msgRect = msg.rectTransform;
            msgRect.anchorMin = new Vector2(0.05f, 0.2f);
            msgRect.anchorMax = new Vector2(0.95f, 0.78f);
            msgRect.offsetMin = Vector2.zero;
            msgRect.offsetMax = Vector2.zero;

            var closeBtn = UIFactory.CreateButton(_messagePanel.transform, "Close", "Continue", new Color(0.25f, 0.4f, 0.55f));
            var closeRect = closeBtn.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.35f, 0.05f);
            closeRect.anchorMax = new Vector2(0.65f, 0.16f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            closeBtn.onClick.AddListener(() => _messagePanel.SetActive(false));

            _messagePanel.SetActive(true);
            UIPopupFront.BringToFront(_messagePanel.transform);
        }

        private void ShowBirthdayMessage(string message)
        {
            _statusText.text = message;
            _statusText.color = new Color(0.6f, 1f, 0.7f);
        }

        private void ShowError(string message)
        {
            _statusText.text = message;
            _statusText.color = new Color(1f, 0.75f, 0.5f);
        }

        private void Logout()
        {
            _financePanel.SetVisible(false);
            _supplyPanel.SetVisible(false);
            _storePanel.SetVisible(false);
            _shippingPanel.SetVisible(false);
            _profilePanel.SetVisible(false);
            _friendsPanel.SetVisible(false);
            _messagePanel.SetActive(false);
            _statusText.text = string.Empty;
            _session.Logout();
        }

        private void HideOtherPanels()
        {
            _financePanel.SetVisible(false);
            _supplyPanel.SetVisible(false);
            _storePanel.SetVisible(false);
            _shippingPanel.SetVisible(false);
            _profilePanel.SetVisible(false);
            _friendsPanel.SetVisible(false);
        }

        private void TogglePanel(FinancePanel panel)
        {
            var show = !panel.IsVisible;
            HideOtherPanels();
            panel.SetVisible(show);
        }

        private void TogglePanel(SupplyShopPanel panel)
        {
            var show = !panel.IsVisible;
            HideOtherPanels();
            panel.SetVisible(show);
        }

        private void TogglePanel(ShippingPanel panel)
        {
            var show = !panel.IsVisible;
            HideOtherPanels();
            panel.SetVisible(show);
        }

        private void TogglePanel(StorePanel panel)
        {
            var show = !panel.IsVisible;
            HideOtherPanels();
            panel.SetVisible(show);
        }

        private void TogglePanel(ProfilePanel panel)
        {
            var show = !panel.IsVisible;
            HideOtherPanels();
            panel.SetVisible(show);
        }

        private void TogglePanel(FriendsPanel panel)
        {
            var show = !panel.IsVisible;
            HideOtherPanels();
            panel.SetVisible(show);
        }

        private static void SetVerticalTabRect(Button button, float yMin, float yMax)
        {
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.08f, yMin);
            rect.anchorMax = new Vector2(0.92f, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
