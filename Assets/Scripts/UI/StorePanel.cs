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
    public class StorePanel : MonoBehaviour
    {
        private GameSession _session;
        private GameContentConfig _content;
        private Text _marketText;
        private Transform _supplyListRoot;
        private GameObject _panelRoot;
        private bool _visible;

        public bool IsVisible => _visible;

        public void Initialize(GameSession session, GameContentConfig content, Transform parent)
        {
            _session = session;
            _content = content;
            _panelRoot = UIFactory.CreatePanel(parent, "StorePanel", new Color(0.1f, 0.12f, 0.18f, 0.95f));
            var rect = _panelRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.1f);
            rect.anchorMax = new Vector2(0.9f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _panelRoot.SetActive(false);

            UIFactory.CreateText(_panelRoot.transform, "Title", "Store", 22, TextAnchor.UpperCenter);
            var titleRect = _panelRoot.transform.Find("Title").GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.92f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            _marketText = UIFactory.CreateText(
                _panelRoot.transform,
                "MarketInfo",
                "Market prices loading...",
                14,
                TextAnchor.UpperLeft);
            var marketRect = _marketText.rectTransform;
            marketRect.anchorMin = new Vector2(0.05f, 0.82f);
            marketRect.anchorMax = new Vector2(0.95f, 0.9f);
            marketRect.offsetMin = Vector2.zero;
            marketRect.offsetMax = Vector2.zero;

            UIFactory.CreateText(_panelRoot.transform, "SupplyLabel", "Buy Supplies", 16, TextAnchor.UpperLeft);
            var supplyLabelRect = _panelRoot.transform.Find("SupplyLabel").GetComponent<RectTransform>();
            supplyLabelRect.anchorMin = new Vector2(0.05f, 0.74f);
            supplyLabelRect.anchorMax = new Vector2(0.95f, 0.8f);
            supplyLabelRect.offsetMin = Vector2.zero;
            supplyLabelRect.offsetMax = Vector2.zero;

            var supplyList = new GameObject("SupplyList", typeof(RectTransform));
            supplyList.transform.SetParent(_panelRoot.transform, false);
            _supplyListRoot = supplyList.transform;
            var supplyRect = supplyList.GetComponent<RectTransform>();
            supplyRect.anchorMin = new Vector2(0.05f, 0.12f);
            supplyRect.anchorMax = new Vector2(0.95f, 0.72f);
            supplyRect.offsetMin = Vector2.zero;
            supplyRect.offsetMax = Vector2.zero;

            GameEvents.OnMineUpdated += OnMineUpdated;
            GameEvents.OnMarketUpdated += OnMarketUpdated;
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            _panelRoot.SetActive(visible);
            if (visible)
            {
                UIPopupFront.BringToFront(_panelRoot.transform);
                RefreshLists();
            }
        }

        private void OnMineUpdated(MineDetailResponse _) => RefreshLists();

        private void OnMarketUpdated(MarketTodayResponse market) => RefreshMarket(market);

        private void OnDestroy()
        {
            GameEvents.OnMineUpdated -= OnMineUpdated;
            GameEvents.OnMarketUpdated -= OnMarketUpdated;
        }

        private void RefreshMarket(MarketTodayResponse market)
        {
            _marketText.text = market == null
                ? "Market prices loading..."
                : $"Game Day {market.gameDay} · {FormatMarketSource(market.source)} · refreshes UTC midnight";
            RefreshLists();
        }

        private static string FormatMarketSource(string source)
        {
            return source switch
            {
                "yahoo-us" => "US stocks (CAT, XOM, JNJ, QCOM)",
                "mock-fallback" => "fallback mock prices",
                _ => source ?? "market"
            };
        }

        private void RefreshLists()
        {
            foreach (Transform child in _supplyListRoot)
            {
                Destroy(child.gameObject);
            }

            if (_content?.supplyConfig?.supplyTypes == null)
            {
                return;
            }

            var market = _session.CurrentMarket;
            var y = 1f;
            const float rowHeight = 0.18f;

            foreach (var supply in _content.supplyConfig.supplyTypes)
            {
                var price = market?.prices?.FirstOrDefault(p =>
                    p.supplyType.ToString() == supply.supplyType.ToString())?.price ?? supply.basePrice;

                var stock = _session.CurrentMine?.inventory?.FirstOrDefault(i =>
                    i.itemType == supply.supplyType.ToString())?.quantity ?? 0f;

                var btn = UIFactory.CreateButton(
                    _supplyListRoot,
                    supply.supplyType.ToString(),
                    $"Buy {supply.displayName}\n{price:F0} cr · Stock: {stock:F0}",
                    supply.color * 0.8f);

                var rect = btn.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, y - rowHeight);
                rect.anchorMax = new Vector2(1f, y);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                y -= rowHeight + 0.02f;

                var captured = supply;
                btn.onClick.AddListener(() => _ = BuySupplyAsync(captured));
            }
        }

        private async System.Threading.Tasks.Task BuySupplyAsync(SupplyTypeEntry supply)
        {
            try
            {
                if (!System.Enum.TryParse<SupplyTypeDto>(supply.supplyType.ToString(), out var dto))
                {
                    return;
                }

                await _session.BuySupplyAsync(dto, 5f);
            }
            catch (Networking.ApiException ex)
            {
                GameEvents.RaiseError(ex.Message);
            }
        }
    }
}
