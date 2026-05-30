using UnityEngine;
using UnityEngine.UI;

namespace Rava.UI
{
    public class ComboDropdown : Dropdown
    {
        protected override GameObject CreateBlocker(Canvas rootCanvas)
        {
            return null;
        }
    }
}
