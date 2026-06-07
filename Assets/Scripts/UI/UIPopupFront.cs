using UnityEngine;

namespace Theexonet.UI
{
    public static class UIPopupFront
    {
        public static void BringToFront(Transform popup)
        {
            if (popup == null)
            {
                return;
            }

            popup.SetAsLastSibling();
        }
    }
}
