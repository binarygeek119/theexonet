using UnityEngine;
using UnityEngine.UI;

namespace Rava.UI
{
    public static class CurrencyFormat
    {
        public const string Name = "Rax";
        private const string IconResourcePath = "currency";

        private static Sprite _iconSprite;

        public static Sprite IconSprite
        {
            get
            {
                if (_iconSprite == null)
                {
                    _iconSprite = Resources.Load<Sprite>(IconResourcePath);
                }

                return _iconSprite;
            }
        }

        public static string FormatAmountOnly(float amount) => amount.ToString("F0");

        public static string FormatAmount(float amount) => $"{amount:F0} {Name}";

        public static string FormatLabel(float amount) => $"{Name}: {amount:F0}";

        public static Image CreateIcon(Transform parent, float size = 18f)
        {
            var sprite = IconSprite;
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject("RaxIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = Color.white;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size, size * 1.2f);
            return image;
        }
    }
}
