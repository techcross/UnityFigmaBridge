using UnityEngine;

namespace UnityFigmaBridge.Runtime.UI
{
    /// <summary>
    /// Temporary representative object for FIGMA nodes to allow them to be matched
    /// When the generation and subtitution process continues
    /// </summary>
    public class FigmaNodeObject : MonoBehaviour
    {
        // Reference to the full FIGMA node id
        public string NodeId;

        public string NodeName;

        /// <summary>
        /// 明示的に初期化するための関数。
        /// Sync 時の差分マッチングで利用する値をここで設定する。
        /// </summary>
        public void Initialise(string nodeId, string nodeName)
        {
            NodeId = nodeId;
            NodeName = nodeName;
        }
    }
}