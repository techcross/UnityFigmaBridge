using UnityEngine;

namespace UnityFigmaBridge.Editor.Extension
{
    /// <summary>
    /// コンポーネントアタッチ用のインターフェース
    /// </summary>
    public interface IComponentAttachment
    {
        /// <summary>
        /// ゲームオブジェクトにコンポーネントアタッチする関数
        /// </summary>
        /// <param name="gameObject">対象のゲームオブジェクト</param>
        public void AttachComponent(GameObject gameObject);
    }
}