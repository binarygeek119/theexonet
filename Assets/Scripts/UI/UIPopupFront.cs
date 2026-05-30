using UnityEngine;

namespace Rava.UI
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
