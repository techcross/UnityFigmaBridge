using System.Collections.Generic;
using UnityEngine;

namespace UnityFigmaBridge.Editor.Extension.ImportCache
{
    /// <summary>
    /// 1回のインポートで利用するキャッシュデータをまとめておくクラス
    /// </summary>
    public static class ImportSessionCache
    {
        // 画像名取得用コンテナ　(imageRef, 画像のノード名(＝画像名))
        public static Dictionary<string, string> imageNameMap = new Dictionary<string, string>();
        // 画像名重複数保持用コンテナ　(画像名, 画像数)
        public static Dictionary<string, int> imageNameCountMap = new Dictionary<string, int>();

        /// <summary>
        /// 外部コンポーネントのASS紐付け用マップ（ローカルNodeId、リソースパス）
        /// </summary>
        public static Dictionary<string, RemoteComponentData> remoteComponentKeyDataMap = new Dictionary<string, RemoteComponentData>();
        /// <summary>
        /// 外部コンポ―ネントかどうか (ノードID)
        /// </summary>
        public static HashSet<string> remoteComponentFlagMap = new HashSet<string>();

        public class RemoteComponentData
        {
            public string fileName;
            public string componentName;
            public Vector2 size;
        }
        
        
        public static void CacheClear()
        {
            imageNameMap.Clear();
            imageNameCountMap.Clear();
            
            remoteComponentKeyDataMap.Clear();
            remoteComponentFlagMap.Clear();
        }
    }
}