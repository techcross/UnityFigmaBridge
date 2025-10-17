using TMPro;
using UnityEngine;

namespace UnityFigmaBridge.Runtime.UI
{
    /// <summary>
    /// ドロップダウンの矢印を展開状況に応じて表示切替するクラス
    /// </summary>
    [RequireComponent(typeof(TMP_Dropdown))]
    public class DropDownArrowController : MonoBehaviour
    {
        [SerializeField] private Animator animator; // 矢印部分のアニメーション
        [SerializeField] private string arrowChangeFlag = "on"; // アニメ状態切替用のパラメーター
        
        private TMP_Dropdown dropdown;
        private int hashArrowChange;
        
        private bool isClose = true; // ドロップダウン閉じているか
        
        private void Awake()
        {
            dropdown = GetComponent<TMP_Dropdown>();
            hashArrowChange = Animator.StringToHash(arrowChangeFlag);
        }

        private void Update()
        {
            // アニメーションが存在しなければ無視
            if(animator == null)return;
            
            
            // ドロップダウンの展開状態に変化があったとき
            if (isClose != dropdown.IsExpanded)
            {
                isClose = dropdown.IsExpanded;
                // アニメ変更
                animator.SetBool(hashArrowChange, isClose);
            }
            
        }
    }
}