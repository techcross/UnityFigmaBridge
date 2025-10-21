using System;
using UnityEngine;
using UnityEngine.UI;
using UnityFigmaBridge.Editor.FigmaApi;
using UnityFigmaBridge.Editor.Utils;

namespace UnityFigmaBridge.Editor.Nodes
{
    /// <summary>
    /// Manages layout functionality for Figma nodes
    /// </summary>
    public static class FigmaLayoutManager
    {
        /// <summary>
        /// Applies layout properties for a given node to a gameObject, using Vertical/Horizontal layout groups
        /// </summary>
        /// <param name="nodeGameObject"></param>
        /// <param name="node"></param>
        /// <param name="figmaImportProcessData"></param>
        /// <param name="scrollContentGameObject">Generated scroll content object (if generated)</param>
        public static void ApplyLayoutPropertiesForNode( GameObject nodeGameObject,Node node,
            FigmaImportProcessData figmaImportProcessData,out GameObject scrollContentGameObject)
        {
            
            // Depending on whether scrolling is applied, we may want to add layout to this object or to the content
            // holder
            
            var targetLayoutObject = nodeGameObject;
            scrollContentGameObject = null;
            
            // Check scrolling requirements
            var implementScrolling = node.type == NodeType.FRAME && node.overflowDirection != Node.OverflowDirection.NONE;
            if (implementScrolling)
            {
                // This Frame implements scrolling, so we need to add in appropriate functionality
                
                // Add in a rect mask to implement clipping
                if (node.clipsContent) UnityUiUtils.GetOrAddComponent<RectMask2D>(nodeGameObject);

                // Create the content clip and parent to this object
                scrollContentGameObject = new GameObject($"{node.name}_ScrollContent", typeof(RectTransform));
                var scrollContentRectTransform = scrollContentGameObject.transform as RectTransform;
                scrollContentRectTransform.pivot = new Vector2(0, 1);
                scrollContentRectTransform.anchorMin = scrollContentRectTransform.anchorMax =new Vector2(0,1);
                scrollContentRectTransform.anchoredPosition=Vector2.zero;
                scrollContentRectTransform.SetParent(nodeGameObject.transform, false);
                
                var scrollRectComponent = UnityUiUtils.GetOrAddComponent<ScrollRect>(nodeGameObject);
                scrollRectComponent.content = scrollContentGameObject.transform as RectTransform;
                scrollRectComponent.horizontal =
                    node.overflowDirection is Node.OverflowDirection.HORIZONTAL_SCROLLING 
                        or Node.OverflowDirection.HORIZONTAL_AND_VERTICAL_SCROLLING;

                scrollRectComponent.vertical =
                    node.overflowDirection is Node.OverflowDirection.VERTICAL_SCROLLING 
                        or Node.OverflowDirection.HORIZONTAL_AND_VERTICAL_SCROLLING;


                // If using layout, we need to use content size fitter to ensure proper sizing for child components
                if (node.layoutMode != Node.LayoutMode.NONE)
                {
                    var contentSizeFitter = UnityUiUtils.GetOrAddComponent<ContentSizeFitter>(scrollContentGameObject);
                    contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }

                // Apply layout to this content clip
                targetLayoutObject = scrollContentGameObject;
            }
            
            
            // Ignore if layout mode is NONE or layout disabled
            if (node.layoutMode == Node.LayoutMode.NONE || !figmaImportProcessData.Settings.EnableAutoLayout) return;
            
            // Remove an existing layout group if it exists
            var existingLayoutGroup = targetLayoutObject.GetComponent<LayoutGroup>();
            if (existingLayoutGroup!=null) UnityEngine.Object.DestroyImmediate(existingLayoutGroup);
            
            LayoutGroup layoutGroup = null;
            
            switch (node.layoutMode)
            {
                case Node.LayoutMode.VERTICAL:
                    var verticalLayoutGroup = UnityUiUtils.GetOrAddComponent<VerticalLayoutGroup>(targetLayoutObject);
                    verticalLayoutGroup.childForceExpandWidth= verticalLayoutGroup.childForceExpandHeight = false;
                    layoutGroup = verticalLayoutGroup;
                    layoutGroup.childAlignment = TextAnchor.MiddleCenter;// 初期値設定
                    // Setup alignment according to Figma layout. Primary is Vertical
                    switch (node.primaryAxisAlignItems)
                    {
                        // Upper Alignment
                        case Node.PrimaryAxisAlignItems.MIN:
                            layoutGroup.childAlignment = node.counterAxisAlignItems switch
                            {
                                Node.CounterAxisAlignItems.MIN => TextAnchor.UpperLeft,
                                Node.CounterAxisAlignItems.CENTER => TextAnchor.UpperCenter,
                                Node.CounterAxisAlignItems.MAX => TextAnchor.UpperRight,
                                _ => layoutGroup.childAlignment
                            };
                            break;
                        // Center alignment
                        case Node.PrimaryAxisAlignItems.CENTER:
                            layoutGroup.childAlignment = node.counterAxisAlignItems switch
                            {
                                Node.CounterAxisAlignItems.MIN => TextAnchor.MiddleLeft,
                                Node.CounterAxisAlignItems.CENTER => TextAnchor.MiddleCenter,
                                Node.CounterAxisAlignItems.MAX => TextAnchor.MiddleRight,
                                _ => layoutGroup.childAlignment
                            };
                            break;
                        // Lower alignment
                        case Node.PrimaryAxisAlignItems.MAX:
                            layoutGroup.childAlignment = node.counterAxisAlignItems switch
                            {
                                Node.CounterAxisAlignItems.MIN => TextAnchor.LowerLeft,
                                Node.CounterAxisAlignItems.CENTER => TextAnchor.LowerCenter,
                                Node.CounterAxisAlignItems.MAX => TextAnchor.LowerRight,
                                _ => layoutGroup.childAlignment
                            };
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    break;
                case Node.LayoutMode.HORIZONTAL:
                {
                    // 折り返しがある場合はGridとする
                    if (node.layoutWrap == Node.LayoutWrap.WRAP)
                    {
                        var gridLayoutGroup = UnityUiUtils.GetOrAddComponent<GridLayoutGroup>(targetLayoutObject);
                        layoutGroup = gridLayoutGroup;
                        gridLayoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
                        // スペース設定
                        gridLayoutGroup.spacing = new Vector2(node.itemSpacing, node.counterAxisSpacing);
                        var cellSize = Vector2.zero;
                        // 子のサイズからセルサイズを取得
                        if (node.children != null && node.children.Length > 0)
                        {
                            var childSize = node.children[0].size;
                            cellSize.x = childSize.x;
                            cellSize.y = childSize.y;
                        }
                        gridLayoutGroup.cellSize = cellSize;
                        // 列数、行数を指定しない
                        gridLayoutGroup.constraint = GridLayoutGroup.Constraint.Flexible;
                        
                        // グリッド基準位置調整
                        gridLayoutGroup.startCorner = node.primaryAxisAlignItems switch
                        {
                            Node.PrimaryAxisAlignItems.MIN => GridLayoutGroup.Corner.UpperLeft,
                            Node.PrimaryAxisAlignItems.MAX => GridLayoutGroup.Corner.UpperRight,
                            _ => GridLayoutGroup.Corner.UpperLeft
                        };
                    }
                    else
                    {
                        var horizontalLayoutGroup =
                            UnityUiUtils.GetOrAddComponent<HorizontalLayoutGroup>(targetLayoutObject);
                        horizontalLayoutGroup.childForceExpandWidth = horizontalLayoutGroup.childForceExpandHeight = false;
                        layoutGroup = horizontalLayoutGroup;
                    }
                    
                    // Setup alignment according to Figma layout. Primary is Horizontal
                    layoutGroup.childAlignment = TextAnchor.MiddleCenter; // 初期値設定
                    layoutGroup.childAlignment = node.primaryAxisAlignItems switch
                    {
                        // Left Alignment
                        Node.PrimaryAxisAlignItems.MIN => node.counterAxisAlignItems switch
                        {
                            Node.CounterAxisAlignItems.MIN => TextAnchor.UpperLeft,
                            Node.CounterAxisAlignItems.CENTER => TextAnchor.MiddleLeft,
                            Node.CounterAxisAlignItems.MAX => TextAnchor.LowerLeft,
                            _ => layoutGroup.childAlignment
                        },
                        // Center alignment
                        Node.PrimaryAxisAlignItems.CENTER => node.counterAxisAlignItems switch
                        {
                            Node.CounterAxisAlignItems.MIN => TextAnchor.UpperCenter,
                            Node.CounterAxisAlignItems.CENTER => TextAnchor.MiddleCenter,
                            Node.CounterAxisAlignItems.MAX => TextAnchor.LowerCenter,
                            _ => layoutGroup.childAlignment
                        },
                        // Right alignment
                        Node.PrimaryAxisAlignItems.MAX => node.counterAxisAlignItems switch
                        {
                            Node.CounterAxisAlignItems.MIN => TextAnchor.UpperRight,
                            Node.CounterAxisAlignItems.CENTER => TextAnchor.MiddleRight,
                            Node.CounterAxisAlignItems.MAX => TextAnchor.LowerRight,
                            _ => layoutGroup.childAlignment
                        },
                        _ => throw new ArgumentOutOfRangeException()
                    };
                }
                    break;
                // 例外は以降何もしない
                default:
                    return;
                    break;
            }

            if (layoutGroup is HorizontalOrVerticalLayoutGroup horizontalOrVerticalLayoutGroup)
            {
                horizontalOrVerticalLayoutGroup.childControlHeight = false;
                horizontalOrVerticalLayoutGroup.childControlWidth = false;
                horizontalOrVerticalLayoutGroup.childScaleHeight = true;
                horizontalOrVerticalLayoutGroup.childScaleWidth = true;
                horizontalOrVerticalLayoutGroup.childForceExpandHeight = false;
                horizontalOrVerticalLayoutGroup.childForceExpandWidth = false;
                horizontalOrVerticalLayoutGroup.spacing = node.itemSpacing;
            }
            
            // パディング設定
            layoutGroup.padding = new RectOffset(Mathf.RoundToInt(node.paddingLeft), Mathf.RoundToInt(node.paddingRight),
                Mathf.RoundToInt(node.paddingTop), Mathf.RoundToInt(node.paddingBottom));
        }
    }
}