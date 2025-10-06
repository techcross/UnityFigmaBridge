using UnityFigmaBridge.Editor.FigmaApi;

namespace UnityFigmaBridge.Editor.Utils
{
    /// <summary>
    /// カスタム命名ルールチェック用のクラス
    /// </summary>
    public static class NameCheckUtils
    {
        /// <summary>
        /// 9Sliceの対象かどうか
        /// </summary>
        /// <param name="node">Figmaノード</param>
        public static bool Is9Slice(this Node node)
        {
            return node.name.EndsWith("_9s");
        }
        
        /// <summary>
        /// ダミーかどうか　ダミーの場合は自身も子オブジェクトも生成しない
        /// </summary>
        /// <param name="node">Figmaノード</param>
        public static bool IsDummyNode(this Node node)
        {
            return node.name.Equals("DummyNode");
        }
        
        /// <summary>
        /// []内を抜き取る
        /// </summary>
        public static string ExtractBracketContent(string input)
        {
            int level = 0;
            int start = -1;

            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '[')
                {
                    if (level == 0) start = i + 1; // 外側の [ 開始位置
                    level++;
                }
                else if (input[i] == ']')
                {
                    level--;
                    if (level == 0 && start >= 0)
                    {
                        return input.Substring(start, i - start);
                    }
                }
            }

            return ""; // [] が見つからなかった場合
        }
    }
}