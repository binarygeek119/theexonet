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
    public class ShippingPanel : MonoBehaviour
    {
        private GameSession _session;
        private GameContentConfig _content;
        private Text _summaryText;
        private Transform _cargoListRoot;
        private GameObject _panelRoot;
        private bool _visible;

        public bool IsVisible => _visible;

        public void Initialize(GameSession session, GameContentConfig content, Transform parent)
        {
            _session = session;
            _content = content;
            _panelRoot = UIFactory.CreatePanel(parent, "ShippingPanel", new Color(0.09f, 0.11f, 0.17f, 0.95f));
            var rect = _panelRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.1f);
            rect.anchorMax = new Vector2(0.9f, 0.9f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _panelRoot.SetActive(false);

            UIFactory.CreateText(_panelRoot.transform, "Title", "Shipping", 22, TextAnchor.UpperCenter);
            var titleRect = _panelRoot.transform.Find("Title").GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.92f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            _summaryText = UIFactory.CreateText(
                _panelRoot.transform,
                "Summary",
                "Load ore cargo and ship it to the NPC refinery.",
                14,
                TextAnchor.UpperLeft);
            var summaryRect = _summaryText.rectTransform;
            summaryRect.anchorMin = new Vector2(0.05f, 0.82f);
            summaryRect.anchorMax = new Vector2(0.95f, 0.9f);
            summaryRect.offsetMin = Vector2.zero;
            summaryRect.offsetMax = Vector2.zero;

            UIFactory.CreateText(_panelRoot.transform, "CargoLabel", "Cargo Hold", 16, TextAnchor.UpperLeft);
            var cargoLabelRect = _panelRoot.transform.Find("CargoLabel").GetComponent<RectTransform>();
            cargoLabelRect.anchorMin = new Vector2(0.05f, 0.74f);
            cargoLabelRect.anchorMax = new Vector2(0.95f, 0.8f);
            cargoLabelRect.offsetMin = Vector2.zero;
            cargoLabelRect.offsetMax = Vector2.zero;

            var cargoList = new GameObject("CargoList", typeof(RectTransform));
            cargoList.transform.SetParent(_panelRoot.transform, false);
            _cargoListRoot = cargoList.transform;
            var cargoRect = cargoList.GetComponent<RectTransform>();
            cargoRect.anchorMin = new Vector2(0.05f, 0.12f);
            cargoRect.anchorMax = new Vector2(0.95f, 0.72f);
            cargoRect.offsetMin = Vector2.zero;
            cargoRect.offsetMax = Vector2.zero;

            GameEvents.OnMineUpdated += OnMineUpdated;
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

        private void OnDestroy()
        {
            GameEvents.OnMineUpdated -= OnMineUpdated;
        }

        private void RefreshLists()
        {
            foreach (Transform child in _cargoListRoot)
            {
                Destroy(child.gameObject);
            }

            var mine = _session.CurrentMine;
            if (mine?.inventory == null || _content?.oreConfig?.oreTypes == null)
            {
                _summaryText.text = "No cargo loaded.";
                return;
            }

            var totalCargo = mine.inventory
                .Where(i => i.category == "Ore")
                .Sum(i => i.quantity);

            _summaryText.text = totalCargo > 0
                ? $"Cargo ready: {totalCargo:F1} units · Ships up to 10 units per run"
                : "No ore in cargo hold. Mine zones to fill the hold.";

            var y = 1f;
            const float rowHeight = 0.18f;
            var hasCargo = false;

            foreach (var ore in _content.oreConfig.oreTypes)
            {
                var stock = mine.inventory.FirstOrDefault(i =>
                    i.itemType == ore.oreType.ToString())?.quantity ?? 0f;

                if (stock <= 0)
                {
                    continue;
                }

                hasCargo = true;
                var btn = UIFactory.CreateButton(
                    _cargoListRoot,
                    ore.oreType.ToString(),
                    $"Ship {ore.displayName}\n{ore.basePrice:F0} cr/u · Qty: {stock:F1}",
                    ore.color * 0.75f);

                var rect = btn.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, y - rowHeight);
                rect.anchorMax = new Vector2(1f, y);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                y -= rowHeight + 0.02f;

                var captured = ore;
                btn.onClick.AddListener(() => _ = ShipOreAsync(captured, stock));
            }

            if (!hasCargo)
            {
                UIFactory.CreateText(_cargoListRoot, "EmptyCargo", "No ore ready to ship.", 14, TextAnchor.UpperLeft);
            }
        }

        private async System.Threading.Tasks.Task ShipOreAsync(OreTypeEntry ore, float stock)
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
