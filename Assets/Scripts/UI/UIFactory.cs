using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Theexonet.UI
{
    public static class UIFactory
    {
        public static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            Stretch(go.GetComponent<RectTransform>());
            return go;
        }

        public static Text CreateText(Transform parent, string name, string text, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static Button CreateButton(Transform parent, string name, string label, Color bgColor)
        {
            var go = CreatePanel(parent, name, bgColor);
            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = bgColor * 1.2f;
            colors.pressedColor = bgColor * 0.8f;
            button.colors = colors;

            var text = CreateText(go.transform, "Label", label, 16, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button;
        }

        public static InputField CreateInputField(
            Transform parent,
            string placeholder,
            int fontSize = 16,
            float paddingX = 10f,
            float paddingY = 5f,
            bool transparentBackground = false)
        {
            var bgColor = transparentBackground
                ? Color.clear
                : new Color(0.15f, 0.17f, 0.22f, 1f);
            var go = CreatePanel(parent, "InputField", bgColor);
            var input = go.AddComponent<InputField>();

            var text = CreateText(go.transform, "Text", "", fontSize, TextAnchor.MiddleLeft);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(paddingX, paddingY);
            textRect.offsetMax = new Vector2(-paddingX, -paddingY);

            var placeholderText = CreateText(go.transform, "Placeholder", placeholder, fontSize, TextAnchor.MiddleLeft);
            placeholderText.color = new Color(1f, 1f, 1f, 0.4f);
            var phRect = placeholderText.rectTransform;
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(paddingX, paddingY);
            phRect.offsetMax = new Vector2(-paddingX, -paddingY);

            input.textComponent = text;
            input.placeholder = placeholderText;
            return input;
        }

        public static (InputField input, Dropdown dropdown, RectTransform root) CreateComboField(
            Transform parent,
            string name,
            string placeholder,
            float listExtraWidth = 96f,
            int fontSize = 12,
            float itemHeight = 22f,
            float listHeight = 110f,
            float pickerWidth = 0.2f)
        {
            var rootGo = CreatePanel(parent, name, new Color(0.17f, 0.19f, 0.25f, 1f));
            var rootRect = rootGo.GetComponent<RectTransform>();

            var input = CreateInputField(
                rootGo.transform,
                placeholder,
                fontSize,
                paddingX: 6f,
                paddingY: 1f,
                transparentBackground: true);
            var inputRect = input.GetComponent<RectTransform>();
            inputRect.anchorMin = Vector2.zero;
            inputRect.anchorMax = new Vector2(1f - pickerWidth, 1f);
            inputRect.offsetMin = Vector2.zero;
            inputRect.offsetMax = Vector2.zero;

            var dropdown = CreateDropdown(
                rootGo.transform,
                $"{name}Picker",
                listExtraWidth,
                fontSize,
                itemHeight,
                listHeight,
                pickerOnly: true);
            var dropdownRect = dropdown.GetComponent<RectTransform>();
            dropdownRect.anchorMin = new Vector2(1f - pickerWidth, 0f);
            dropdownRect.anchorMax = Vector2.one;
            dropdownRect.offsetMin = Vector2.zero;
            dropdownRect.offsetMax = Vector2.zero;

            return (input, dropdown, rootRect);
        }

        public static Dropdown CreateDropdown(
            Transform parent,
            string name,
            float listExtraWidth = 96f,
            int fontSize = 13,
            float itemHeight = 28f,
            float listHeight = 150f,
            bool pickerOnly = false)
        {
            var rootColor = pickerOnly
                ? Color.clear
                : new Color(0.15f, 0.17f, 0.22f, 1f);
            var rootGo = CreatePanel(parent, name, rootColor);
            var dropdown = rootGo.AddComponent<ComboDropdown>();

            var label = CreateText(rootGo.transform, "Label", "", fontSize, TextAnchor.MiddleLeft);
            ConfigureDropdownText(label);
            label.gameObject.SetActive(!pickerOnly);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(2f, 1f);
            labelRect.offsetMax = new Vector2(-12f, -1f);

            if (pickerOnly)
            {
                var pickerHitArea = CreatePanel(rootGo.transform, "PickerHitArea", new Color(1f, 1f, 1f, 0.01f));
                Stretch(pickerHitArea.GetComponent<RectTransform>());
                dropdown.targetGraphic = pickerHitArea.GetComponent<Image>();
            }
            else
            {
                dropdown.targetGraphic = rootGo.GetComponent<Image>();
            }

            var arrow = CreateText(rootGo.transform, "Arrow", "v", Mathf.Max(8, fontSize - 3), TextAnchor.MiddleCenter);
            var arrowRect = arrow.rectTransform;
            if (pickerOnly)
            {
                Stretch(arrowRect);
                arrow.raycastTarget = false;
            }
            else
            {
                arrowRect.anchorMin = new Vector2(0.72f, 0f);
                arrowRect.anchorMax = Vector2.one;
                arrowRect.offsetMin = Vector2.zero;
                arrowRect.offsetMax = Vector2.zero;
            }

            var templateGo = new GameObject("Template", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image), typeof(ScrollRect));
            templateGo.transform.SetParent(rootGo.transform, false);
            templateGo.SetActive(false);
            templateGo.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);

            var templateCanvas = templateGo.GetComponent<Canvas>();
            templateCanvas.overrideSorting = true;
            templateCanvas.sortingOrder = short.MaxValue;

            var templateRect = templateGo.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(1f, 1f);
            templateRect.anchoredPosition = Vector2.zero;
            templateRect.sizeDelta = new Vector2(listExtraWidth, listHeight);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(templateGo.transform, false);
            var viewportImage = viewportGo.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;
            Stretch(viewportGo.GetComponent<RectTransform>());

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, itemHeight);

            var itemGo = new GameObject("Item", typeof(RectTransform), typeof(Toggle), typeof(Image));
            itemGo.transform.SetParent(contentGo.transform, false);
            var itemRect = itemGo.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, itemHeight);

            var itemBg = itemGo.GetComponent<Image>();
            itemBg.color = new Color(0.2f, 0.22f, 0.28f, 1f);

            var itemLabel = CreateText(itemGo.transform, "Item Label", "Option", fontSize, TextAnchor.MiddleLeft);
            ConfigureDropdownText(itemLabel);
            var itemLabelRect = itemLabel.rectTransform;
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(6f, 1f);
            itemLabelRect.offsetMax = new Vector2(-4f, -1f);

            var toggle = itemGo.GetComponent<Toggle>();
            toggle.targetGraphic = itemBg;
            toggle.isOn = true;

            var scrollRect = templateGo.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportGo.GetComponent<RectTransform>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            dropdown.template = templateRect;
            dropdown.captionText = label;
            dropdown.itemText = itemLabel;
            dropdown.gameObject.AddComponent<DropdownListOverlay>();

            return dropdown;
        }

        private static void ConfigureDropdownText(Text label)
        {
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.alignByGeometry = false;
        }

        public static void SetDropdownOptions(Dropdown dropdown, IList<string> options)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
            dropdown.value = 0;
            dropdown.RefreshShownValue();
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
