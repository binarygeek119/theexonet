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
    public class SupplyShopPanel : MonoBehaviour
    {
        private GameSession _session;
        private GameContentConfig _content;
        private Transform _supplyListRoot;
        private Transform _oreListRoot;
        private Text _marketText;
        private GameObject _panelRoot;
        private bool _visible;

        public bool IsVisible => _visible;

        private void OnMineUpdated(MineDetailResponse _) => RefreshLists();

        public void Initialize(GameSession session, GameContentConfig content, Transform parent)
        {
            _session = session;
            _content = content;
            _panelRoot = UIFactory.CreatePanel(parent, "SupplyPanel", new Color(0.1f, 0.12f, 0.16f, 0.95f));
            var rect = _panelRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.1f);
            rect.anchorMax = new Vector2(0.9f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _panelRoot.SetActive(false);

            UIFactory.CreateText(_panelRoot.transform, "Title", "Supplies & Ore Sales", 22, TextAnchor.UpperCenter);
            var titleRect = _panelRoot.transform.Find("Title").GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.92f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            _marketText = UIFactory.CreateText(_panelRoot.transform, "MarketInfo", "Market prices loading...", 14, TextAnchor.UpperLeft);
            var marketRect = _marketText.rectTransform;
            marketRect.anchorMin = new Vector2(0.05f, 0.82f);
            marketRect.anchorMax = new Vector2(0.95f, 0.9f);
            marketRect.offsetMin = Vector2.zero;
            marketRect.offsetMax = Vector2.zero;

            UIFactory.CreateText(_panelRoot.transform, "SupplyLabel", "Buy Supplies (US Market Linked)", 16, TextAnchor.UpperLeft);
            var supplyLabelRect = _panelRoot.transform.Find("SupplyLabel").GetComponent<RectTransform>();
            supplyLabelRect.anchorMin = new Vector2(0.05f, 0.74f);
            supplyLabelRect.anchorMax = new Vector2(0.48f, 0.8f);
            supplyLabelRect.offsetMin = Vector2.zero;
            supplyLabelRect.offsetMax = Vector2.zero;

            var supplyList = new GameObject("SupplyList", typeof(RectTransform));
            supplyList.transform.SetParent(_panelRoot.transform, false);
            _supplyListRoot = supplyList.transform;
            var supplyRect = supplyList.GetComponent<RectTransform>();
            supplyRect.anchorMin = new Vector2(0.05f, 0.42f);
            supplyRect.anchorMax = new Vector2(0.48f, 0.72f);
            supplyRect.offsetMin = Vector2.zero;
            supplyRect.offsetMax = Vector2.zero;

            UIFactory.CreateText(_panelRoot.transform, "OreLabel", "Sell Ore (NPC Refinery)", 16, TextAnchor.UpperLeft);
            var oreLabelRect = _panelRoot.transform.Find("OreLabel").GetComponent<RectTransform>();
            oreLabelRect.anchorMin = new Vector2(0.52f, 0.74f);
            oreLabelRect.anchorMax = new Vector2(0.95f, 0.8f);
            oreLabelRect.offsetMin = Vector2.zero;
            oreLabelRect.offsetMax = Vector2.zero;

            var oreList = new GameObject("OreList", typeof(RectTransform));
            oreList.transform.SetParent(_panelRoot.transform, false);
            _oreListRoot = oreList.transform;
            var oreRect = oreList.GetComponent<RectTransform>();
            oreRect.anchorMin = new Vector2(0.52f, 0.42f);
            oreRect.anchorMax = new Vector2(0.95f, 0.72f);
            oreRect.offsetMin = Vector2.zero;
            oreRect.offsetMax = Vector2.zero;

            GameEvents.OnMineUpdated += OnMineUpdated;
            GameEvents.OnMarketUpdated += RefreshMarket;
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            _panelRoot.SetActive(visible);
            if (visible)
            {
                RefreshLists();
            }
        }

        private void OnDestroy()
        {
            GameEvents.OnMineUpdated -= OnMineUpdated;
            GameEvents.OnMarketUpdated -= RefreshMarket;
        }

        private void RefreshMarket(MarketTodayResponse market)
        {
            if (market?.prices == null)
            {
                return;
            }

            _marketText.text = $"Game Day {market.gameDay} — Mock US Market ({market.source})";
            RefreshLists();
        }

        private void RefreshLists()
        {
            BuildSupplyButtons();
            BuildOreButtons();
        }

        private void BuildSupplyButtons()
        {
            foreach (Transform child in _supplyListRoot)
            {
                Destroy(child.gameObject);
            }

            var market = _session.CurrentMarket;
            var y = 1f;
            const float rowHeight = 0.22f;

            if (_content?.supplyConfig?.supplyTypes == null)
            {
                return;
            }

            foreach (var supply in _content.supplyConfig.supplyTypes)
            {
                var price = market?.prices?.FirstOrDefault(p =>
                    p.supplyType.ToString() == supply.supplyType.ToString())?.price ?? supply.basePrice;

                var stock = _session.CurrentMine?.inventory?.FirstOrDefault(i =>
                    i.itemType == supply.supplyType.ToString())?.quantity ?? 0f;

                var btn = UIFactory.CreateButton(
                    _supplyListRoot,
                    supply.supplyType.ToString(),
                    $"{supply.displayName}\n{price:F0} cr | Stock: {stock:F0}",
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

        private void BuildOreButtons()
        {
            foreach (Transform child in _oreListRoot)
            {
                Destroy(child.gameObject);
            }

            var y = 1f;
            const float rowHeight = 0.22f;

            if (_content?.oreConfig?.oreTypes == null || _session.CurrentMine?.inventory == null)
            {
                return;
            }

            foreach (var ore in _content.oreConfig.oreTypes)
            {
                var stock = _session.CurrentMine.inventory.FirstOrDefault(i =>
                    i.itemType == ore.oreType.ToString())?.quantity ?? 0f;

                if (stock <= 0)
                {
                    continue;
                }

                var btn = UIFactory.CreateButton(
                    _oreListRoot,
                    ore.oreType.ToString(),
                    $"Sell {ore.displayName}\n{ore.basePrice:F0} cr/u | Qty: {stock:F1}",
                    ore.color * 0.7f);

                var rect = btn.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, y - rowHeight);
                rect.anchorMax = new Vector2(1f, y);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                y -= rowHeight + 0.02f;

                var captured = ore;
                btn.onClick.AddListener(() => _ = SellOreAsync(captured, stock));
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

        private async System.Threading.Tasks.Task SellOreAsync(OreTypeEntry ore, float stock)
        {
            try
            {
                if (!System.Enum.TryParse<OreTypeDto>(ore.oreType.ToString(), out var dto))
                {
                    return;
                }

                var qty = Mathf.Min(stock, 10f);
                await _session.SellOreAsync(dto, qty);
            }
            catch (Networking.ApiException ex)
            {
                GameEvents.RaiseError(ex.Message);
            }
        }
    }
}
