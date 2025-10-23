using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityFigmaBridge.Runtime.UI
{
    /// <summary>
    /// ラジオボタン管理用クラス
    /// （ラジオボタンの基本的な動作はToggleの設定で行う前提）
    /// </summary>
    public class RadioButtonManager : MonoBehaviour
    {
        [SerializeField]private List<Toggle> toggles = new List<Toggle>();
        private int selectIndex = -1;
        public int SelectIndex => selectIndex;
        
        private void Start()
        {
            for(var i = 0; i < toggles.Count; i++)
            {
                var toggle = toggles[i];
                if(toggle == null)return;
                var index = i;
                toggle.onValueChanged.AddListener(isOn =>
                {
                    if (isOn)
                    {
                        selectIndex = index;
                    }
                });
            }
        }

        public void Add(Toggle toggle)
        {
            toggles.Add(toggle);
        }
        
        public void SetSelectedIndex(int index)
        {
            if(index < 0 || index >= toggles.Count)return;
            toggles[index].isOn = true;
        }
        
        public void Clear()
        {
            toggles.Clear();
        }

        public void AutoAssignee()
        {
            toggles.Clear();
            var toggleGroup = GetComponent<ToggleGroup>();
            if (toggleGroup == null)
            {
                toggleGroup =  gameObject.AddComponent<ToggleGroup>();
            }
            toggleGroup.allowSwitchOff = false;
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("List")) continue;
                
                var toggle = child.GetComponent<Toggle>();
                if (toggle == null)
                {
                    toggle = child.gameObject.AddComponent<Toggle>();
                    if (toggle == null) continue;
                }
                
                toggle.group = toggleGroup;
                toggles.Add(toggle);
                var contentChildren = child.GetComponentsInChildren<Transform>(true);
                foreach (var contentChild in contentChildren)
                {
                    if (contentChild.name.Contains("Check"))
                    {
                        toggle.graphic = child.GetComponent<Image>();
                        break;
                    }
                }
            }
        }
    }
}