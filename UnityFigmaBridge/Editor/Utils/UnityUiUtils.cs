using UnityEngine;
using UnityEngine.UI;
using UnityFigmaBridge.Runtime.UI;

namespace UnityFigmaBridge.Editor.Utils
{
    public static class UnityUiUtils
    {
        public static RectTransform CreateRectTransform(string name, Transform parentTransform)
        {
            var newObject = new GameObject(name);
            var newTransform=newObject.AddComponent<RectTransform>();
            newTransform.SetParent(parentTransform,false);
            SetTransformFullStretch(newTransform);
            return newTransform;
        }

        public static void SetTransformFullStretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition=Vector2.zero;
            rectTransform.sizeDelta=Vector2.zero;
        }
        
        public static void CloneTransformData(RectTransform source, RectTransform destination)
        {
            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.anchoredPosition = source.anchoredPosition;
            destination.sizeDelta = source.sizeDelta;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;
        }

        /// <summary>
        /// Retrieves and returns the specified component if it already exists. If it does not exist, it is added and returned
        /// </summary>
        /// <param name="T"></param>
        /// <param name="gameObject"></param>
        public static T GetOrAddComponent<T>(GameObject gameObject) where T : UnityEngine.Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null) component = gameObject.AddComponent<T>() as T;
            return component;
        }
        
        public static T GetOrAddComponent<T>(Transform transform) where T : UnityEngine.Component
        {
            return GetOrAddComponent<T>(transform.gameObject);
        }

        /// <summary>
        /// Unity.Engine.UI.ImageとFigmaImageの値コピー
        /// </summary>
        /// <param name="img1">コピー先 Image</param>
        /// <param name="img2">コピーする Image</param>
        /// <param name="isCopySourceImage">ソースイメージをコピーするかどうか</param>
        public static void CopyImage(this Image img1, Image img2, bool isCopySourceImage)
        {
            if(img1 == null || img2 == null) return;
            if (isCopySourceImage)
            {
                img1.sprite = img2.sprite;
            }
            img1.color = img2.color;
            img1.material = img2.material;
            img1.raycastTarget = img2.raycastTarget;
            img1.raycastPadding = img2.raycastPadding;
            img1.maskable = img2.maskable;
        }
        
        public enum AnchorPreset
        {
            StretchWidthTop,
            StretchWidthMiddle,
            StretchWidthBottom,
            StretchHeightLeft,
            StretchHeightCenter,
            StretchHeightRight,
            StretchFull,
        }
        
        /// <summary>
        /// アンカーを指定プリセットの通りに設定
        /// </summary>
        public static void ApplyAnchorPreset(this RectTransform rectTransform, AnchorPreset preset)
        {
            switch (preset)
            {
                case AnchorPreset.StretchWidthTop:
                    rectTransform.anchorMin = new Vector2(0, 1);
                    rectTransform.anchorMax = new Vector2(1, 1);
                    break;
                case AnchorPreset.StretchWidthMiddle:
                    rectTransform.anchorMin = new Vector2(0, 0.5f);
                    rectTransform.anchorMax = new Vector2(1, 0.5f);
                    break;
                case AnchorPreset.StretchWidthBottom:
                    rectTransform.anchorMin = new Vector2(0, 0);
                    rectTransform.anchorMax = new Vector2(1, 0);
                    break;
                case AnchorPreset.StretchHeightLeft:
                    rectTransform.anchorMin = new Vector2(0, 0);
                    rectTransform.anchorMax = new Vector2(0, 1);
                    break;
                case AnchorPreset.StretchHeightCenter:
                    rectTransform.anchorMin = new Vector2(0.5f, 0);
                    rectTransform.anchorMax = new Vector2(0.5f, 1);
                    break;
                case AnchorPreset.StretchHeightRight:
                    rectTransform.anchorMin = new Vector2(1, 0);
                    rectTransform.anchorMax = new Vector2(1, 1);
                    break;
                case AnchorPreset.StretchFull:
                    rectTransform.anchorMin = new Vector2(0, 0);
                    rectTransform.anchorMax = new Vector2(1, 1);
                    break;
            }
        }
    }
}