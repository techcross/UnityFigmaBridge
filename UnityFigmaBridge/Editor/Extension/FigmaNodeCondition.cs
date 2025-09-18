using System;

namespace UnityFigmaBridge.Editor.Extension
{
    /// <summary>
    /// FigmaNodeの状態
    /// </summary>
    [Flags]
    public enum FigmaNodeCondition : byte
    {
        None = 0,// 何もなし 
        ServerRenderNode = 1 << 0, // サーバーレンダー画像を利用しているノードか
        // ServerRenderSubstitution = 1 << 1 | ServerRenderNode, // サーバーレンダー画像に置き換えるか
    }

    public static class FigmaNodeConditionExtension
    {
        public static bool IsServerRenderNode(this FigmaNodeCondition condition)
        {
            return (condition & FigmaNodeCondition.ServerRenderNode) != FigmaNodeCondition.None;
        }
        
        // public static bool IsServerRenderSubstitution(this FigmaNodeCondition condition)
        // {
        //     return (condition & FigmaNodeCondition.ServerRenderSubstitution) != FigmaNodeCondition.None;
        // }
    }
}