using UnityEngine;

namespace UnityFigmaBridge.Editor.Nodes.DataMarker
{
    /// <summary>
    /// 外部コンポーネント用のマーカー
    /// </summary>
    public class RemoteComponentMarker : MonoBehaviour
    {
#if UNITY_EDITOR
        public string nodeId;
        public string fileName;
        public string componentName;
#endif
    }
}