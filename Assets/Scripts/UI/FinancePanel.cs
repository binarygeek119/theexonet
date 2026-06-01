using System.Linq;
using Rava.Core.Config;
using Rava.Core.Dtos;
using Rava.Core.Enums;
using Rava.Core.Events;
using Rava.Mining;
using UnityEngine;
using UnityEngine.UI;

namespace Rava.UI
{
    public class FinancePanel : MonoBehaviour
    {
        private GameSession _session;
        private GameContentConfig _content;
        private Text _summaryText;
        private Transform _transactionRoot;
        private Button _emergencyBtn;
        private GameObject _panelRoot;
        private bool _visible;

        public bool IsVisible => _visible;

        public void Initialize(GameSession session, GameContentConfig content, Transform parent)
        {
            _session = session;
            _content = content;
            _panelRoot = UIFactory.CreatePanel(parent, "FinancePanel", new Color(0.1f, 0.12f, 0.16f, 0.95f));
            var rect = _panelRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.1f);
            rect.anchorMax = new Vector2(0.9f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _panelRoot.SetActive(false);

            UIFactory.CreateText(_panelRoot.transform, "Title", "Finances", 22, TextAnchor.UpperCenter);
            var titleRect = _panelRoot.transform.Find("Title").GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.9f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            _summaryText = UIFactory.CreateText(_panelRoot.transform, "Summary", "", 15, TextAnchor.UpperLeft);
            var summaryRect = _summaryText.rectTransform;
            summaryRect.anchorMin = new Vector2(0.05f, 0.55f);
            summaryRect.anchorMax = new Vector2(0.95f, 0.88f);
            summaryRect.offsetMin = Vector2.zero;
            summaryRect.offsetMax = Vector2.zero;

            UIFactory.CreateText(_panelRoot.transform, "TxTitle", "Recent Transactions", 16, TextAnchor.UpperLeft);
            var txTitleRect = _panelRoot.transform.Find("TxTitle").GetComponent<RectTransform>();
            txTitleRect.anchorMin = new Vector2(0.05f, 0.48f);
            txTitleRect.anchorMax = new Vector2(0.95f, 0.54f);
            txTitleRect.offsetMin = Vector2.zero;
            txTitleRect.offsetMax = Vector2.zero;

            var txList = new GameObject("Transactions", typeof(RectTransform));
            txList.transform.SetParent(_panelRoot.transform, false);
            _transactionRoot = txList.transform;
            var txRect = txList.GetComponent<RectTransform>();
            txRect.anchorMin = new Vector2(0.05f, 0.18f);
            txRect.anchorMax = new Vector2(0.95f, 0.46f);
            txRect.offsetMin = Vector2.zero;
            txRect.offsetMax = Vector2.zero;

            _emergencyBtn = UIFactory.CreateButton(
                _panelRoot.transform, "EmergencyBtn", "Emergency Ore Buyback (50%)",
                new Color(0.6f, 0.25f, 0.2f));
            var emergencyRect = _emergencyBtn.GetComponent<RectTransform>();
            emergencyRect.anchorMin = new Vector2(0.2f, 0.06f);
            emergencyRect.anchorMax = new Vector2(0.8f, 0.14f);
            emergencyRect.offsetMin = Vector2.zero;
            emergencyRect.offsetMax = Vector2.zero;
            _emergencyBtn.onClick.AddListener(() => _ = EmergencySellAsync());
            _emergencyBtn.gameObject.SetActive(false);

            GameEvents.OnFinancesUpdated += Refresh;
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            _panelRoot.SetActive(visible);
            if (visible)
            {
                UIPopupFront.BringToFront(_panelRoot.transform);
            }
        }

        private void OnDestroy()
        {
            GameEvents.OnFinancesUpdated -= Refresh;
        }

        private void Refresh(FinanceResponse finances)
        {
            _summaryText.text =
                $"{CurrencyFormat.FormatLabel(finances.credits)}\n" +
                $"Daily Payroll: {finances.dailyPayroll:F0}\n" +
                $"Daily Supply Cost: {finances.dailySupplyCost:F0}\n" +
                $"Est. Daily Income: {finances.estimatedDailyIncome:F0}\n" +
                $"Runway: {(finances.runwayDays >= 999 ? "∞" : finances.runwayDays.ToString("F1"))} days\n" +
                (finances.isSoftlocked ? "\nSOFTLOCKED — Use emergency buyback!" : "");

            _summaryText.color = finances.isSoftlocked ? new Color(1f, 0.4f, 0.4f) : Color.white;
            _emergencyBtn.gameObject.SetActive(finances.canEmergencyBuyback);

            foreach (Transform child in _transactionRoot)
            {
                Destroy(child.gameObject);
            }

            if (finances.recentTransactions == null)
            {
                return;
            }

            var y = 1f;
            const float rowHeight = 0.12f;
            foreach (var tx in finances.recentTransactions.Take(8))
            {
                var sign = tx.amount >= 0 ? "+" : "";
                var line = UIFactory.CreateText(
                    _transactionRoot,
                    tx.description,
                    $"Day {tx.gameDay}: {sign}{tx.amount:F0} — {tx.description}",
                    13,
                    TextAnchor.MiddleLeft);
                var lineRect = line.rectTransform;
                lineRect.anchorMin = new Vector2(0f, y - rowHeight);
                lineRect.anchorMax = new Vector2(1f, y);
                lineRect.offsetMin = Vector2.zero;
                lineRect.offsetMax = Vector2.zero;
                y -= rowHeight;
            }
        }

        private async System.Threading.Tasks.Task EmergencySellAsync()
        {
            var mine = _session.CurrentMine;
            if (mine?.inventory == null)
            {
                return;
            }

            var salvage = mine.inventory.FirstOrDefault(i =>
                i.category == "Ore" && i.itemType == OreTypeDto.SalvageScrap.ToString() && i.quantity > 0);

            if (salvage == null)
            {
                salvage = mine.inventory.FirstOrDefault(i => i.category == "Ore" && i.quantity > 0);
            }

            if (salvage == null)
            {
                GameEvents.RaiseError("No ore available for emergency buyback.");
                return;
            }

            try
            {
                if (!System.Enum.TryParse<OreTypeDto>(salvage.itemType, out var oreType))
                {
                    return;
                }

                var qty = Mathf.Min(salvage.quantity, 5f);
                await _session.SellOreAsync(oreType, qty, emergency: true);
            }
            catch (Networking.ApiException ex)
            {
                GameEvents.RaiseError(ex.Message);
            }
        }
    }
}
