using System;
using TMPro;
using UnityEngine;

namespace UnityFigmaBridge.Editor.Nodes.DataMarker
{
    public class FontMarker : MonoBehaviour
    {
#if UNITY_EDITOR
        public string fontName;
        public string matName;
#endif
    }
}