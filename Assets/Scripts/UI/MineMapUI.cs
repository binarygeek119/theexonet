using System.Collections.Generic;
using System.Linq;
using Theexonet.Core.Config;
using Theexonet.Core.Dtos;
using Theexonet.Core.Enums;
using Theexonet.Core.Events;
using Theexonet.Mining;
using UnityEngine;
using UnityEngine.UI;

namespace Theexonet.UI
{
    public class MineMapUI : MonoBehaviour
    {
        private GameSession _session;
        private GameContentConfig _content;
        private Transform _gridRoot;
        private Text _zoneInfoText;
        private Transform _workerListRoot;
        private MineZoneDto _selectedZone;
        private readonly Dictionary<string, Button> _zoneButtons = new();

        public void Initialize(GameSession session, GameContentConfig content, Transform parent)
        {
            _session = session;
            _content = content;

            var panel = UIFactory.CreatePanel(parent, "MineMapPanel", new Color(0.1f, 0.12f, 0.16f, 0.9f));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.15f, 0.12f);
            panelRect.anchorMax = new Vector2(0.68f, 0.88f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            UIFactory.CreateText(panel.transform, "MapTitle", "Asteroid Mine Grid", 20, TextAnchor.UpperCenter);
            var titleRect = panel.transform.Find("MapTitle").GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.92f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            var gridContainer = new GameObject("Grid", typeof(RectTransform));
            gridContainer.transform.SetParent(panel.transform, false);
            _gridRoot = gridContainer.transform;
            var gridRect = gridContainer.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.05f, 0.08f);
            gridRect.anchorMax = new Vector2(0.95f, 0.9f);
            gridRect.offsetMin = Vector2.zero;
            gridRect.offsetMax = Vector2.zero;

            var sidePanel = UIFactory.CreatePanel(panel.transform, "SidePanel", new Color(0.14f, 0.16f, 0.2f, 1f));
            var sideRect = sidePanel.GetComponent<RectTransform>();
            sideRect.anchorMin = new Vector2(1.02f, 0f);
            sideRect.anchorMax = new Vector2(1.45f, 1f);
            sideRect.offsetMin = Vector2.zero;
            sideRect.offsetMax = Vector2.zero;

            _zoneInfoText = UIFactory.CreateText(sidePanel.transform, "ZoneInfo", "Select a zone", 14, TextAnchor.UpperLeft);
            var infoRect = _zoneInfoText.rectTransform;
            infoRect.anchorMin = new Vector2(0.05f, 0.55f);
            infoRect.anchorMax = new Vector2(0.95f, 0.95f);
            infoRect.offsetMin = Vector2.zero;
            infoRect.offsetMax = Vector2.zero;

            UIFactory.CreateText(sidePanel.transform, "WorkersLabel", "Assign Worker", 16, TextAnchor.UpperLeft);
            var workersLabelRect = sidePanel.transform.Find("WorkersLabel").GetComponent<RectTransform>();
            workersLabelRect.anchorMin = new Vector2(0.05f, 0.48f);
            workersLabelRect.anchorMax = new Vector2(0.95f, 0.54f);
            workersLabelRect.offsetMin = Vector2.zero;
            workersLabelRect.offsetMax = Vector2.zero;

            var workerScroll = new GameObject("WorkerList", typeof(RectTransform));
            workerScroll.transform.SetParent(sidePanel.transform, false);
            _workerListRoot = workerScroll.transform;
            var workerRect = workerScroll.GetComponent<RectTransform>();
            workerRect.anchorMin = new Vector2(0.05f, 0.05f);
            workerRect.anchorMax = new Vector2(0.95f, 0.46f);
            workerRect.offsetMin = Vector2.zero;
            workerRect.offsetMax = Vector2.zero;

            GameEvents.OnMineUpdated += Refresh;
        }

        private void OnDestroy()
        {
            GameEvents.OnMineUpdated -= Refresh;
        }

        private void Refresh(MineDetailResponse mine)
        {
            BuildGrid(mine);
            if (_selectedZone != null)
            {
                _selectedZone = mine.zones.FirstOrDefault(z => z.id == _selectedZone.id);
                UpdateZoneInfo();
                BuildWorkerButtons(mine);
            }
        }

        private void BuildGrid(MineDetailResponse mine)
        {
            foreach (Transform child in _gridRoot)
            {
                Destroy(child.gameObject);
            }

            _zoneButtons.Clear();
            var gridSize = _content != null ? _content.gridSize : 8;
            var cellSize = 1f / gridSize;

            foreach (var zone in mine.zones.OrderBy(z => z.y).ThenBy(z => z.x))
            {
                if (!System.Enum.TryParse<OreType>(zone.oreType.ToString(), out var oreType))
                {
                    oreType = OreType.Ferroxite;
                }

                var color = _content?.oreConfig.Get(oreType).color ?? Color.gray;
                if (zone.depletedPct >= 100f && !zone.isSalvageZone)
                {
                    color *= 0.4f;
                }

                if (zone.isSalvageZone)
                {
                    color = new Color(0.6f, 0.65f, 0.7f);
                }

                var btn = UIFactory.CreateButton(_gridRoot, $"Zone_{zone.x}_{zone.y}", "", color);
                var rect = btn.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(zone.x * cellSize, 1f - (zone.y + 1) * cellSize);
                rect.anchorMax = new Vector2((zone.x + 1) * cellSize, 1f - zone.y * cellSize);
                rect.offsetMin = new Vector2(1, 1);
                rect.offsetMax = new Vector2(-1, -1);

                var captured = zone;
                btn.onClick.AddListener(() => SelectZone(captured, mine));
                _zoneButtons[zone.id] = btn;
            }
        }

        private void SelectZone(MineZoneDto zone, MineDetailResponse mine)
        {
            _selectedZone = zone;
            UpdateZoneInfo();
            BuildWorkerButtons(mine);
        }

        private void UpdateZoneInfo()
        {
            if (_selectedZone == null)
            {
                _zoneInfoText.text = "Select a zone";
                return;
            }

            var oreName = _selectedZone.oreType.ToString();
            if (_content?.oreConfig != null &&
                System.Enum.TryParse<OreType>(_selectedZone.oreType.ToString(), out var oreType))
            {
                oreName = _content.oreConfig.Get(oreType).displayName;
            }

            _zoneInfoText.text =
                $"Zone ({_selectedZone.x},{_selectedZone.y})\n" +
                $"Ore: {oreName}\n" +
                $"Richness: {_selectedZone.richness:F2}\n" +
                $"Depleted: {_selectedZone.depletedPct:F0}%\n" +
                (_selectedZone.isSalvageZone ? "Emergency salvage zone\nAlways mineable." : "");
        }

        private void BuildWorkerButtons(MineDetailResponse mine)
        {
            foreach (Transform child in _workerListRoot)
            {
                Destroy(child.gameObject);
            }

            if (_selectedZone == null)
            {
                return;
            }

            var y = 1f;
            const float rowHeight = 0.18f;
            foreach (var worker in mine.workers)
            {
                var isAssignedHere = worker.assignedZoneId == _selectedZone.id;
                var label = isAssignedHere
                    ? $"{worker.name} (assigned)"
                    : string.IsNullOrEmpty(worker.assignedZoneId)
                        ? worker.name
                        : $"{worker.name} (busy)";

                var btn = UIFactory.CreateButton(
                    _workerListRoot,
                    worker.id,
                    label,
                    isAssignedHere ? new Color(0.3f, 0.55f, 0.35f) : new Color(0.25f, 0.3f, 0.4f));

                var rect = btn.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, y - rowHeight);
                rect.anchorMax = new Vector2(1f, y);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                y -= rowHeight + 0.02f;

                var capturedWorker = worker;
                btn.onClick.AddListener(() => AssignWorkerAsync(capturedWorker));
            }
        }

        private async void AssignWorkerAsync(WorkerDto worker)
        {
            if (_selectedZone == null)
            {
                return;
            }

            try
            {
                if (worker.assignedZoneId == _selectedZone.id)
                {
                    await _session.UnassignWorkerAsync(worker.id);
                }
                else
                {
                    await _session.AssignWorkerAsync(worker.id, _selectedZone.id);
                }
            }
            catch (Networking.ApiException ex)
            {
                GameEvents.RaiseError(ex.Message);
            }
        }
    }
}
