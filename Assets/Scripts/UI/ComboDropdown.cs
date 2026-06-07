using UnityEngine;
using UnityEngine.UI;

namespace Theexonet.UI
{
    public class ComboDropdown : Dropdown
    {
        protected override GameObject CreateBlocker(Canvas rootCanvas)
        {
            return null;
        }
    }
}
