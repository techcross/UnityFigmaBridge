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
        Is9Slice = 1 << 2,// 9Sliceか　画像取得時、コンポーネントNodeに付ける、オブジェクト構築時、インスタンスにつける
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
        
        public static bool Is9Slice(this FigmaNodeCondition condition)
        {
            return (condition & FigmaNodeCondition.Is9Slice) != FigmaNodeCondition.None;
        }
    }
}