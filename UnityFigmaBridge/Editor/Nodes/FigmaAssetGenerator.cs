using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityFigmaBridge.Editor.Components;
using UnityFigmaBridge.Editor.Extension;
using UnityFigmaBridge.Editor.Extension.ImportCache;
using UnityFigmaBridge.Editor.FigmaApi;
using UnityFigmaBridge.Editor.Nodes.DataMarker;
using UnityFigmaBridge.Editor.PrototypeFlow;
using UnityFigmaBridge.Editor.Utils;
using UnityFigmaBridge.Runtime.UI;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

namespace UnityFigmaBridge.Editor.Nodes
{
    /// <summary>
    /// Root generator for figma file/document. Constructs a native UI system and all assets
    /// </summary>
    public static class FigmaAssetGenerator
    {
        /// <summary>
        /// Builds a native unity UI given input figma data
        /// </summary>
        /// <param name="rootCanvas">Root canvas for generation</param>
        /// <param name="figmaImportProcessData"></param>
        /// <param name="saveFigmaPageAsPrefab"></param>
        public static void BuildFigmaFile(Canvas rootCanvas, FigmaImportProcessData figmaImportProcessData, bool saveFigmaPageAsPrefab)
        {
            // コンポーネントアタッチ設定をマージ前処理で使えるよう早期に初期化する
            CustomComponentAttachManager.OnStart();

            // Save prefab for each page
            var downloadPageIdList = figmaImportProcessData.SelectedPagesForImport.Select(p => p.id).ToList();
            
            // Cycle through all pages and create
            var createdPages = new List<(Node,GameObject)>();
            foreach (var figmaCanvasNode in figmaImportProcessData.SourceFile.document.children)
            {
                bool includedPageObject = downloadPageIdList.Contains(figmaCanvasNode.id);
                EditorUtility.DisplayProgressBar(UnityFigmaBridgeImporter.PROGRESS_BOX_TITLE, $"Generating Page {figmaCanvasNode.name} ", 0);
                var pageGameObject = BuildFigmaPage(figmaCanvasNode, rootCanvas.transform as RectTransform, figmaImportProcessData,includedPageObject);
                createdPages.Add((figmaCanvasNode,pageGameObject));
            }
            
            // 保存する場合だけ保存
            if (saveFigmaPageAsPrefab)
            {
                // Save prefab for each page
                for (var i = 0; i < createdPages.Count; i++)
            // ページを保存する場合
                {
                    // if (!downloadPageIdList.Contains(createdPages[i].Item1.id)) continue;
                    SaveFigmaPageAsPrefab(createdPages[i].Item1, createdPages[i].Item2, figmaImportProcessData);
                }
            }

            // Destroy all page objects
            foreach (var createdPage in createdPages)
            {
                Object.DestroyImmediate(createdPage.Item2);
            }
            
            // Instantiate all components
            ComponentManager.InstantiateAllComponentPrefabs(figmaImportProcessData);

            // Remove all temporary components that were created along the way
            ComponentManager.RemoveAllTemporaryNodeComponents(figmaImportProcessData);
            
            // At the very end, we want to apply figmaNode behaviour where required
            BehaviourBindingManager.BindBehaviours(figmaImportProcessData);
        }


        /// <summary>
        /// Builds an individual page (Canvas object in Figma API)
        /// </summary>
        /// <param name="pageNode"></param>
        /// <param name="parentTransform"></param>
        /// <param name="figmaImportProcessData"></param>
        /// <param name="includedPageObject"></param>
        /// <returns></returns>
        private static GameObject BuildFigmaPage(Node pageNode, RectTransform parentTransform,
            FigmaImportProcessData figmaImportProcessData, bool includedPageObject)
        {
           
            var pageGameObject = new GameObject(pageNode.name, typeof(RectTransform));
            var pageTransform = pageGameObject.transform as RectTransform;
            pageTransform.SetParent(parentTransform, false);
            
            // Setup transform for page
            pageTransform.pivot = new Vector2(0, 1);
            pageTransform.anchorMin = pageTransform.anchorMax = new Vector2(0, 1); // Top left
            
            // Generate all child nodes. 
            foreach (var childNode in pageNode.children)
            {
               
                if (CheckNodeValidForGeneration(childNode,figmaImportProcessData))
                    BuildFigmaNode(childNode, pageTransform, pageNode, 0, figmaImportProcessData, includedPageObject, false, false );
            }

            return pageGameObject;
        }

        private static bool CheckNodeValidForGeneration(Node node, FigmaImportProcessData figmaImportProcessData)
        {
            return figmaImportProcessData.Settings.GenerateNodesMarkedForExport || node.exportSettings == null || node.exportSettings.Length == 0;
        }


        /// <summary>
        /// Build an individual Figma Node - this can be of any type, eg FRAME, RECTANGLE, ELLIPSE, TEXT. Frames at depth 0 are treated as screens 
        /// </summary>
        /// <param name="figmaNode">The source figma node</param>
        /// <param name="parentTransform">The parent transform figmaNode</param>
        /// <param name="parentFigmaNode">The parent figma node</param>
        /// <param name="nodeRecursionDepth">Depth of recursion</param>
        /// <param name="figmaImportProcessData"></param>
        /// <param name="includedPageObject"></param>
        /// <param name="withinComponentDefinition"></param>
        /// <param name="isInnerInstance">インスタンス内部かどうか</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private static GameObject BuildFigmaNode(Node figmaNode, RectTransform parentTransform,  Node parentFigmaNode,
            int nodeRecursionDepth, FigmaImportProcessData figmaImportProcessData,
            bool includedPageObject, bool withinComponentDefinition, bool isInnerInstance)
        {
            // ダミーオブジェクトは生成しない
            if (figmaNode.IsDummyNode()) return null;
            
            Debug.Log(
                $"[BuildFigmaNode] enter " +
                $"name:{figmaNode.name}, id:{figmaNode.id}, type:{figmaNode.type}, " +
                $"parent:{parentFigmaNode?.name}, depth:{nodeRecursionDepth}, " +
                $"includedPageObject:{includedPageObject}, withinComponentDefinition:{withinComponentDefinition}, isInnerInstance:{isInnerInstance}"
            );

            // このFigmaノード用のGameObjectを作成し、親Transformの子にする
            var nodeGameObject = new GameObject(figmaNode.name, typeof(RectTransform));
            nodeGameObject.transform.SetParent(parentTransform, false);
            var nodeRectTransform = nodeGameObject.transform as RectTransform;
            
            // 場合によってはサーバー描画済みビットマップに置き換える必要があるため、その対象かどうか確認する
            var matchingServerRenderEntry = figmaImportProcessData.ServerRenderNodes.FirstOrDefault((testNode) => testNode.SourceNode.id == figmaNode.id);
            // Transformを適用する。サーバーレンダー対象の場合は absolute bounding box を使う
            if (matchingServerRenderEntry != null)
            {
                NodeTransformManager.ApplyAbsoluteBoundsFigmaTransform(nodeRectTransform, figmaNode, parentFigmaNode,nodeRecursionDepth >0);
            }
            // インスタンス内部ではない場合
            else if (!isInnerInstance)
            {
                NodeTransformManager.ApplyFigmaTransform(nodeRectTransform, figmaNode, parentFigmaNode,nodeRecursionDepth >0);
            }
            
            // NodeId と NodeName を主キーにして再Sync時のマージ対象を見つける。
            var figmaNodeObject = nodeGameObject.AddComponent<FigmaNodeObject>();
            figmaNodeObject.Initialise(figmaNode.id, figmaNode.name);

            // このノードがFigmaのマスクオブジェクトであれば、Maskコンポーネントを追加する（描画はしない）
            if (figmaNode.isMask)
            {
                var mask=nodeGameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
            }
            
            // コンポーネントインスタンスの場合、既存定義があるかを確認する
            // 存在する場合はフルノードを生成せず、「component node marker」コンポーネントを付けておく
            // 後段でPrefabをInstantiateし、プロパティを適用して置き換える
            // インスタンスの場合
            if (figmaNode.type == NodeType.INSTANCE)
            {
                isInnerInstance = true;
                
                // 外部コンポーネントだった場合
                if (ImportSessionCache.remoteComponentKeyDataMap.TryGetValue(figmaNode.componentId, out var data))
                {
                    // 外部コンポーネント用のマーカーをつける
                    var fgmComponentNodeMarker = nodeGameObject.AddComponent<RemoteComponentMarker>();
                    fgmComponentNodeMarker.nodeId = figmaNode.id;
                    fgmComponentNodeMarker.fileName = data.fileName;
                    fgmComponentNodeMarker.componentName = data.componentName;
                    if (data.componentName.Is9Slice())
                    {
                        figmaNode.customCondition |= FigmaNodeCondition.Is9Slice;
                    }
                    return nodeGameObject;
                }

                // 通常コンポーネントだった場合（コンポーネントの取得に成功した場合）
                if (figmaImportProcessData.NodeLookupDictionary.TryGetValue(figmaNode.componentId, out var componentNode))
                {
                    // コンポーネント用のマーカーをつける
                    // プレースホルダーTransformとコンポーネントを付与し、2回目の処理で置き換える
                    nodeGameObject.AddComponent<FigmaComponentNodeMarker>().Initialise(figmaNode.id, parentFigmaNode.id, figmaNode.componentId);
                    if (componentNode.Is9Slice())
                    {
                        figmaNode.customCondition |= FigmaNodeCondition.Is9Slice;
                    }
                    return nodeGameObject;
                }
                
                Debug.Log($"<color=yellow>コンポーネントが存在しなかった\nid:{figmaNode.componentId}, name:{figmaNode.name}</color>");
                // それ以外の場合は定義が見つからないとみなし、通常ノードとして生成する
            }

            if (figmaNode.type == NodeType.COMPONENT) withinComponentDefinition = true;
            
            if (matchingServerRenderEntry!=null)
            {
                // 単純な画像ノードを付与する（カスタムレンダラーは不要）
                nodeGameObject.AddComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FigmaPaths.GetPathForServerRenderedImage(figmaNode.id,figmaImportProcessData.ServerRenderNodes));
                
                // ボタンの可能性があるため、プロトタイプ機能を確認して適用する
                PrototypeFlowManager.ApplyPrototypeFunctionalityToNode(figmaNode, nodeGameObject, figmaImportProcessData);

                // If this node is visible, mark the game object is inactive
                if (!figmaNode.visible) nodeGameObject.SetActive(false);

               // if (ShouldTryMergeExistingPrefab(figmaNode))
               // {
                   // ComponentManager.TryMergeWithExistingPrefab(figmaNode, nodeGameObject);
               // }

                if (ShouldGenerateComponentAsset(figmaNode, parentFigmaNode, figmaImportProcessData))
                {
                    Debug.Log($"[ComponentPrefab] Generate/Merge target: {figmaNode.name} type={figmaNode.type} id={figmaNode.id}");
                    ComponentManager.GenerateComponentAssetFromNode(figmaNode, parentFigmaNode, nodeGameObject, figmaImportProcessData);
                }
                
                return nodeGameObject;
            }
            
            // このfigmaNodeに必要なUnityコンポーネントを生成する
            // プロパティ適用は別メソッドに分ける
            FigmaNodeManager.CreateUnityComponentsForNode(nodeGameObject, figmaNode,figmaImportProcessData);
            
            // このfigmaNodeのプロパティを適用する
            FigmaNodeManager.ApplyUnityComponentPropertiesForNode(nodeGameObject,figmaNode,figmaImportProcessData);
            
            // このfigmaNodeにエフェクトがある場合は適用する（SECTIONなど一部はeffectsを持たない）
            if (figmaNode.effects!=null) EffectManager.ApplyAllFigmaEffectsToUnityNode(nodeGameObject,figmaNode,figmaImportProcessData);
            
            // 必要に応じてレイアウトプロパティを適用する（VerticalLayoutGroup など）。スクロール実装もここで行う
            FigmaLayoutManager.ApplyLayoutPropertiesForNode(nodeGameObject,figmaNode,figmaImportProcessData,out var scrollContentGameObject);
            
            // 子ノードが存在する場合は生成する
            // 9Sliceオブジェクトの子要素は生成しない
            if (figmaNode.children != null && 
                !figmaNode.customCondition.Is9Slice())
            {
                // 子ノード生成中に有効なマスクを追跡する。マスク���象ノードはその配下に親子付けする必要がある
                Mask activeMaskObject=null;
                foreach (var childNode in figmaNode.children)
                {
                    var childGameObject = BuildFigmaNode(childNode, nodeRectTransform, figmaNode,
                        nodeRecursionDepth + 1, figmaImportProcessData,includedPageObject, withinComponentDefinition, isInnerInstance);
                    if (childGameObject == null) continue;
                    // このオブジェクトがMaskコンポーネントを持っていれば、現在のアクティブマスクとして保持する
                    var childGameObjectMask = childGameObject.GetComponent<Mask>();
                    if (childGameObjectMask != null) activeMaskObject = childGameObjectMask;
                    else
                    {
                        // 現在マスクオブジェクトがある場合は、その子に付け替える（現在のTransformは維持）
                        if (activeMaskObject != null) childGameObject.transform.SetParent(activeMaskObject.transform, true);
                        // レイアウトによってScrollContentが生成されている場合は、そちらを親にする
                        if (scrollContentGameObject!=null) childGameObject.transform.SetParent(scrollContentGameObject.transform, true);
                    }
                }
            }
            
            Debug.Log(
                $"[BuildFigmaNode2] enter ");
            if (figmaNode.name == "CharaDetailTop")
            {
                Debug.Log("test");
            }
            
            // スクロールコンテンツ生成の最終処理
            // レイアウト未使用の場合は、生成された子全体が収まるようにサイズを再計算する
            if (scrollContentGameObject != null && figmaNode.layoutMode == Node.LayoutMode.NONE)
            {
                // すべての子ノードの結合Boundsを計算する
                var boundsRect = NodeTransformManager.GetRelativeBoundsForAllChildNodes(figmaNode);
                ((RectTransform)scrollContentGameObject.transform).sizeDelta = new Vector2(boundsRect.xMax, boundsRect.yMax);
            }
            
            // このfigmaNodeのプロトタイプ要素を適用する
            // 一部のボタンバリエーションでは子が必要になるため、子生成後に行う必要がある
            PrototypeFlowManager.ApplyPrototypeFunctionalityToNode(figmaNode ,nodeGameObject, figmaImportProcessData);

            switch (figmaNode.type)
            {
                // 親が canvas または section の場合は flowScreen として扱い、Prefabを作成する
                // ただし生成対象ページ上にある場合に限る
                case NodeType.FRAME:
                    if (includedPageObject && FigmaDataUtils.IsScreenNode(figmaNode,parentFigmaNode))
                    {
                        SaveFigmaScreenAsPrefab(figmaNode, parentFigmaNode, nodeRectTransform, figmaImportProcessData);
                    }
                    break;
                case NodeType.SECTION:
                    RegisterFigmaSection(figmaNode, figmaImportProcessData);
                    break;
            }

            // If this node is visible, mark the game object is inactive
            if (!figmaNode.visible) nodeGameObject.SetActive(false);
            
            // if (ShouldTryMergeExistingPrefab(figmaNode))
            // {
            //     ComponentManager.TryMergeWithExistingPrefab(figmaNode, nodeGameObject);
            // }

            if (ShouldGenerateComponentAsset(figmaNode, parentFigmaNode, figmaImportProcessData))
            {
                Debug.Log($"[ComponentPrefab] Generate/Merge target: {figmaNode.name} type={figmaNode.type} id={figmaNode.id}");
                ComponentManager.GenerateComponentAssetFromNode(figmaNode, parentFigmaNode, nodeGameObject, figmaImportProcessData);
            }

            return nodeGameObject;
        }

        /// <summary>
        /// コンポーネントPrefabを生成・更新するべきノードかどうかを判定する。
        /// 既存Prefabとのマージ対象判定とは分けて管理し、ここでは
        /// 「新規保存や更新対象として扱うノード」を絞り込む。
        /// </summary>
        /// <param name="node">判定対象のFigmaノード</param>
        /// <param name="parentNode">親Figmaノード</param>
        /// <param name="figmaImportProcessData">インポート処理データ</param>
        /// <returns>
        /// コンポーネントPrefabを生成・更新する対象であれば true。
        /// それ以外は false。
        /// </returns>
        private static bool ShouldGenerateComponentAsset(Node node, Node parentNode, FigmaImportProcessData figmaImportProcessData)
        {
            if (node == null) return false;

            // 外部コンポーネントはローカルPrefabを生成しない
            if (ImportSessionCache.remoteComponentFlagMap.Contains(node.id))
                return false;

            // substitution はPrefab生成対象外
            if (FigmaNodeManager.NodeIsSubstitution(node, figmaImportProcessData))
                return false;

            // 基本は component 定義のみPrefab化する
            return node.type == NodeType.COMPONENT;
        }

        /// <summary>
        /// 既存Prefabとのマージを試みるべきノードかどうかを判定する。
        /// ここではノード種別では絞り込まず、既存アセットが存在する可能性があるノードを広く対象にする。
        /// 実際にマージされるかどうかは TryMergeWithExistingPrefab 内で
        /// GUIDマップとPrefabパスが見つかるかどうかで最終判定する。
        /// </summary>
        /// <param name="node">判定対象のFigmaノード</param>
        /// <returns>
        /// 既存Prefabとのマージを試行してよい場合は true。
        /// 外部コンポーネントなど、ローカルPrefabを扱わないノードは false。
        /// </returns>
        private static bool ShouldTryMergeExistingPrefab(Node node)
        {
            if (node == null) return false;

            // 外部コンポーネントはローカルPrefabとマージしない
            if (ImportSessionCache.remoteComponentFlagMap.Contains(node.id))
                return false;

            return true;
        }

        /// <summary>
        /// Create a flowScreen prefab from a generated figma asset
        /// </summary>
        /// <param name="node"></param>
        /// <param name="parentNode"></param>
        /// <param name="screenRectTransform"></param>
        /// <param name="figmaImportProcessData"></param>
        private static void SaveFigmaScreenAsPrefab(Node node, Node parentNode,RectTransform screenRectTransform, FigmaImportProcessData figmaImportProcessData)
        {
            var screenNameCount = figmaImportProcessData.ScreenPrefabNameCounter.TryGetValue(node.name, out var value)
                ? value : 0;

            figmaImportProcessData.ScreenPrefabNameCounter[node.name] = screenNameCount + 1;

            var current = screenRectTransform.anchoredPosition;
            screenRectTransform.anchoredPosition = Vector2.zero;

            var screenPrefabPath = FigmaPaths.GetPathForScreenPrefab(node, screenNameCount);

            // マージ前にコンポーネントアタッチを適用する（RemoteComponentMarker含む）
            // マージ時の RemapInternalReferences が正しく動作するよう、マージより前に呼ぶ必要がある
            BehaviourBindingManager.ApplyPreMergeComponentAttachmentsToNodeAndChildren(screenRectTransform.gameObject, figmaImportProcessData);

            // save前に既存Prefabとマージ
            ComponentManager.TryMergeWithExistingPrefab(node, screenRectTransform.gameObject, screenPrefabPath);

            var screenPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                screenRectTransform.gameObject,
                screenPrefabPath,
                InteractionMode.UserAction);

            screenRectTransform.anchoredPosition = current;

            // save後にGUID map登録
            var componentMap = FigmaAssetGuidMapManager.CreateMap(FigmaAssetGuidMapManager.AssetType.Component);
            var screenPrefabGuid = AssetDatabase.AssetPathToGUID(screenPrefabPath);
            componentMap.Add(node.id, screenPrefabGuid, screenPrefab.name);

            if (figmaImportProcessData.Settings.BuildPrototypeFlow)
            {
                figmaImportProcessData.PrototypeFlowController.RegisterFigmaScreen(new FigmaFlowScreen
                {
                    FigmaScreenPrefab = screenPrefab,
                    FigmaNodeId = node.id,
                    FigmaScreenName = FigmaPaths.GetFileNameForNode(node, screenNameCount),
                    ParentSectionNodeId = parentNode is { type: NodeType.SECTION } ? parentNode.id : string.Empty
                });
            }

            figmaImportProcessData.ScreenPrefabs.Add(screenPrefab);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        /// <param name="pageGameObject"></param>
        /// <param name="figmaImportProcessData"></param>
        private static void SaveFigmaPageAsPrefab(Node node, GameObject pageGameObject, FigmaImportProcessData figmaImportProcessData)
        {
           
            var pageNameCount = figmaImportProcessData.PagePrefabNameCounter.TryGetValue(node.name, out var value)
                ? value : 0;
            
            // Increment count to ensure no naming collisions
            figmaImportProcessData.PagePrefabNameCounter[node.name] = pageNameCount + 1;
            Debug.Log("SaveAsPrefabAssetAndConnect2 : "+pageGameObject.gameObject.name);

            var pagePrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(pageGameObject,
                FigmaPaths.GetPathForPagePrefab(node,pageNameCount),InteractionMode.UserAction);
            figmaImportProcessData.PagePrefabs.Add(pagePrefab);
        }





        /// <summary>
        /// Registers a figma section. This is needed for flow controller to properly transition between sections
        /// </summary>
        /// <param name="node"></param>
        /// <param name="figmaImportProcessData"></param>
        private static void RegisterFigmaSection(Node node, FigmaImportProcessData figmaImportProcessData)
        {
            if (figmaImportProcessData.Settings.BuildPrototypeFlow)
            {
                
                // We want to find the default start point for this section
                var prototypeFlowStartNodeId = string.Empty;
                var prototypeFlowStartNodeName = string.Empty;
                // Search through all start points and see if they are a child of this section
                foreach (var flowStartId in figmaImportProcessData.PrototypeFlowStartPoints)
                {
                    var matchingNode = FigmaDataUtils.GetFigmaNodeInChildren(node, flowStartId);
                    if (matchingNode == null) continue;
                    // We've found a match
                    prototypeFlowStartNodeId = flowStartId;
                    prototypeFlowStartNodeName = matchingNode.name;
                }
                
                figmaImportProcessData.PrototypeFlowController.RegisterFigmaSection(new FigmaSection
                {
                    FigmaNodeId = node.id,
                    FigmaPrototypeFlowStartNodeId = prototypeFlowStartNodeId,
                    FigmaPrototypeFlowStartNodeName= prototypeFlowStartNodeName,
                    FigmaNodeName = node.name,
                });
            }
        }
    }

}