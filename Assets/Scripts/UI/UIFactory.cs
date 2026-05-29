using UnityEngine;
using UnityEngine.UI;

namespace Rava.UI
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

        public static InputField CreateInputField(Transform parent, string placeholder)
        {
            var go = CreatePanel(parent, "InputField", new Color(0.15f, 0.17f, 0.22f, 1f));
            var input = go.AddComponent<InputField>();

            var text = CreateText(go.transform, "Text", "", 16, TextAnchor.MiddleLeft);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);

            var placeholderText = CreateText(go.transform, "Placeholder", placeholder, 16, TextAnchor.MiddleLeft);
            placeholderText.color = new Color(1f, 1f, 1f, 0.4f);
            var phRect = placeholderText.rectTransform;
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(10, 5);
            phRect.offsetMax = new Vector2(-10, -5);

            input.textComponent = text;
            input.placeholder = placeholderText;
            return input;
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
