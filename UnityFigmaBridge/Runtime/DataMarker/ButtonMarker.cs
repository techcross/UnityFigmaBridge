using TMPro;
using UnityEngine;

namespace UnityFigmaBridge.Editor.Nodes.DataMarker
{
    public class ButtonMarker : MonoBehaviour, IUIDataMarker
    {
        public TMP_Text labelText;
        public string labelName;
        public string commandKey;
        public GameObject iconObj;
    }
}