using System.Linq;
using Rava.Core.Dtos;
using Rava.Core.Events;
using Rava.Mining;
using UnityEngine;
using UnityEngine.UI;

namespace Rava.UI
{
    public class GameHud : MonoBehaviour
    {
        private GameSession _session;
        private Text _creditsText;
        private Text _dayText;
        private Text _statusText;
        private FinancePanel _financePanel;
        private SupplyShopPanel _supplyPanel;
        private GameObject _messagePanel;

        public void Initialize(
            GameSession session,
            FinancePanel financePanel,
            SupplyShopPanel supplyPanel,
            Transform parent)
        {
            _session = session;
            _financePanel = financePanel;
            _supplyPanel = supplyPanel;

            var hud = UIFactory.CreatePanel(parent, "HUD", new Color(0.06f, 0.08f, 0.11f, 0.95f));
            var hudRect = hud.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0f, 0.88f);
            hudRect.anchorMax = new Vector2(1f, 1f);
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;

            _creditsText = UIFactory.CreateText(hud.transform, "Credits", "Credits: ---", 18, TextAnchor.MiddleLeft);
            var creditsRect = _creditsText.rectTransform;
            creditsRect.anchorMin = new Vector2(0.02f, 0.1f);
            creditsRect.anchorMax = new Vector2(0.3f, 0.9f);
            creditsRect.offsetMin = Vector2.zero;
            creditsRect.offsetMax = Vector2.zero;

            _dayText = UIFactory.CreateText(hud.transform, "Day", "Day: ---", 18, TextAnchor.MiddleCenter);
            var dayRect = _dayText.rectTransform;
            dayRect.anchorMin = new Vector2(0.35f, 0.1f);
            dayRect.anchorMax = new Vector2(0.55f, 0.9f);
            dayRect.offsetMin = Vector2.zero;
            dayRect.offsetMax = Vector2.zero;

            var endDayBtn = UIFactory.CreateButton(hud.transform, "EndDay", "End Day", new Color(0.45f, 0.3f, 0.15f));
            var endDayRect = endDayBtn.GetComponent<RectTransform>();
            endDayRect.anchorMin = new Vector2(0.58f, 0.15f);
            endDayRect.anchorMax = new Vector2(0.72f, 0.85f);
            endDayRect.offsetMin = Vector2.zero;
            endDayRect.offsetMax = Vector2.zero;
            endDayBtn.onClick.AddListener(() => _ = AdvanceDayAsync());

            var financeBtn = UIFactory.CreateButton(hud.transform, "FinanceTab", "Finances", new Color(0.2f, 0.35f, 0.5f));
            SetTabRect(financeBtn, 0.74f, 0.84f);
            financeBtn.onClick.AddListener(() => TogglePanel(_financePanel));

            var supplyBtn = UIFactory.CreateButton(hud.transform, "SupplyTab", "Supplies", new Color(0.2f, 0.4f, 0.35f));
            SetTabRect(supplyBtn, 0.86f, 0.96f);
            supplyBtn.onClick.AddListener(() => TogglePanel(_supplyPanel));

            _statusText = UIFactory.CreateText(parent, "StatusBar", "", 14, TextAnchor.MiddleCenter);
            var statusRect = _statusText.rectTransform;
            statusRect.anchorMin = new Vector2(0.2f, 0.02f);
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
            GameEvents.OnError += ShowError;
        }

        private void OnDestroy()
        {
            GameEvents.OnMineUpdated -= RefreshHud;
            GameEvents.OnDayAdvanced -= ShowDayMessages;
            GameEvents.OnError -= ShowError;
        }

        private void RefreshHud(MineDetailResponse mine)
        {
            _creditsText.text = $"Credits: {mine.credits:F0}";
            _dayText.text = $"Day {mine.currentGameDay}";
        }

        private async System.Threading.Tasks.Task AdvanceDayAsync()
        {
            _statusText.text = "Advancing day...";
            try
            {
                await _session.AdvanceDayAsync();
                _statusText.text = "";
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
        }

        private void ShowError(string message)
        {
            _statusText.text = message;
        }

        private void TogglePanel(FinancePanel panel)
        {
            panel.SetVisible(!panel.IsVisible);
            _supplyPanel.SetVisible(false);
        }

        private void TogglePanel(SupplyShopPanel panel)
        {
            panel.SetVisible(!panel.IsVisible);
            _financePanel.SetVisible(false);
        }

        private static void SetTabRect(Button button, float xMin, float xMax)
        {
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(xMin, 0.15f);
            rect.anchorMax = new Vector2(xMax, 0.85f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
