using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.UI;

namespace UnityFigmaBridge.Runtime.UI
{
    /// <summary>
    /// チェックボックス群のフラグ管理を行うクラス
    /// 最大32個のチェックボックスをまとまりで扱える
    /// </summary>
    public class CheckBoxFlagManager : MonoBehaviour
    {
        [SerializeField]private List<Toggle> toggles = new List<Toggle>();

        [SerializeField, ReadOnly(true)]private int flags;
        
        public int Flags => flags;
        public bool this[int index]
        {
            get
            {
                if (index < 0 || index >= 32)
                {
                    return false;
                }
                return (flags & (1 << index)) != 0;
            }
        }

        private void Start()
        {
            var count = toggles.Count;
            // 対応範囲は 32bit のため成形
            if (count > 32)
            {
                Debug.LogError("Too many checkboxes");
                count = 32;
            }
            
            for(var i = 0; i < count; i++)
            {
                var toggle = toggles[i];
                if(toggle == null)return;
                var index = i;
                toggle.onValueChanged.AddListener(isOn =>
                {
                    if (isOn)
                    {
                        flags |= 1 << index;
                    }
                    else
                    {
                        flags &= ~(1 << index);
                    }
                });
            }
        }

        public void Add(Toggle toggle)
        {
            toggles.Add(toggle);
        }

        public void AllStateChange(bool isOn)
        {
            foreach (var toggle in toggles)
            {
                toggle.isOn = isOn;
            }
        }
        
        public void AllStateChange(int toggleStates)
        {
            var count = toggles.Count;
            // 対応範囲は 32bit のため成形
            if (count > 32)
            {
                Debug.LogError("Too many checkboxes");
                count = 32;
            }
            for (var i = 0; i < count; i++)
            {
                toggles[i].isOn = ((1 << i) & toggleStates) != 0;
            }
        }

        public void Clear()
        {
            toggles.Clear();
        }

        public void AutoAssignee()
        {
            toggles.Clear();
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("List")) continue;
                
                var toggle = child.GetComponent<Toggle>();
                if (toggle == null)
                {
                    toggle = child.gameObject.AddComponent<Toggle>();
                    if (toggle == null) continue;
                }

                toggles.Add(toggle);
                var contentChildren = child.GetComponentsInChildren<Transform>(true);
                foreach (var contentChild in contentChildren)
                {
                    if (contentChild.name.Contains("Check"))
                    {
                        toggle.graphic = contentChild.GetComponent<Image>();
                        break;
                    }
                }
            }
        }
    }
}