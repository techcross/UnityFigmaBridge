using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityFigmaBridge.Editor.Extension;
using UnityFigmaBridge.Editor.Extension.ImportCache;
using UnityFigmaBridge.Editor.FigmaApi;
using UnityFigmaBridge.Editor.Nodes;
using UnityFigmaBridge.Editor.Nodes.DataMarker;
using UnityFigmaBridge.Editor.PrototypeFlow;
using UnityFigmaBridge.Editor.Utils;
using UnityFigmaBridge.Runtime.UI;
using Component = UnityEngine.Component;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

namespace UnityFigmaBridge.Editor.Components
{
    public static class ComponentManager
    {
       /// <summary>
       /// Remove component placeholders that are used to mark instantiation locations
       /// </summary>
       /// <param name="figmaImportProcessData"></param>
        public static void RemoveAllTemporaryNodeComponents(FigmaImportProcessData figmaImportProcessData)
       {
           // Remove from components (nested)
            foreach (var componentPrefab in figmaImportProcessData.ComponentData.AllComponentPrefabs)
                RemoveTemporaryNodeComponents(componentPrefab);
            
            // Remove from screens
            foreach (var framePrefab in figmaImportProcessData.ScreenPrefabs.Where(framePrefab => framePrefab!=null))
            {
                RemoveTemporaryNodeComponents(framePrefab);
            }
            // Remove from pages
            foreach (var pagePrefab in figmaImportProcessData.PagePrefabs.Where(pagePrefab => pagePrefab!=null))
            {
                RemoveTemporaryNodeComponents(pagePrefab);
            }
       }

        /// <summary>
        /// Remove all component placeholders from a given prefab object (could be flowScreen or component)
        /// </summary>
        /// <param name="sourcePrefab"></param>
        private static void RemoveTemporaryNodeComponents(GameObject sourcePrefab)
        {
            var assetPath = AssetDatabase.GetAssetPath(sourcePrefab);
            var prefabContents = PrefabUtility.LoadPrefabContents(assetPath);

            // 差分Syncに使う FigmaNodeObject は残し、プレースホルダーマーカーだけ削除する。
            // 再生成ではなく差分反映するため、NodeId と NodeName は維持する。
            var allComponentMarkers = prefabContents.GetComponentsInChildren<FigmaComponentNodeMarker>(true);
            foreach (var marker in allComponentMarkers)
            {
                Debug.Log($"=====Removing 差分Syncに使う {marker.name} from prefab {sourcePrefab.name}");
                Object.DestroyImmediate(marker);
            }

            var allSwapMarkers = prefabContents.GetComponentsInChildren<InstanceSwapMarker>(true);
            foreach (var swapMarker in allSwapMarkers)
            {
                Debug.Log($"=====Removing 差分Syncに使う swap marker {swapMarker.name} from prefab {sourcePrefab.name}");
                Object.DestroyImmediate(swapMarker);
            }

            // Save
            PrefabUtility.SaveAsPrefabAsset(prefabContents, assetPath);
            // Unload
            PrefabUtility.UnloadPrefabContents(prefabContents);
        }
        
        
        /// <summary>
        /// Creates a component prefab from a given generated node
        /// </summary>
        /// <param name="node"></param>
        /// <param name="nodeGameObject"></param>
        /// <param name="figmaImportProcessData"></param>
        public static void GenerateComponentAssetFromNode(Node node, Node parentNode, GameObject nodeGameObject, FigmaImportProcessData figmaImportProcessData)
        {
            // 外部コンポーネントだった場合は無視
            if(ImportSessionCache.remoteComponentFlagMap.Contains(node.id)) return;
            
            // If this is part of a component set (eg a variant), append the name of the component set to the component name
            var nodeName=parentNode is { type: NodeType.COMPONENT_SET } ? $"{parentNode.name}-{node.name}" : node.name;
            var componentCount = figmaImportProcessData.ComponentData.GetComponentNameCount(nodeName);
            figmaImportProcessData.ComponentData.IncrementComponentNameCount(nodeName,1);

            // ここですでにキャッシュされたファイルが存在する場合はその場所に生成する
            var cacheMap = FigmaAssetGuidMapManager.CreateMap(FigmaAssetGuidMapManager.AssetType.Component);
            var prefabAssetPath = cacheMap.GetAssetPath(node.id);
            if (string.IsNullOrEmpty(prefabAssetPath))
            {
                prefabAssetPath = FigmaPaths.GetPathForComponentPrefab(nodeName,componentCount);
            }
        
            // 既存Prefabがある場合は、Unity側変更を今回生成物へ差分マージする
            if (File.Exists(prefabAssetPath))
            {
                var existingPrefabContents = PrefabUtility.LoadPrefabContents(prefabAssetPath);
                try
                {
                    Debug.Log($"==== 既存Prefabとの差分マージ開始: {prefabAssetPath}");

                    SyncComponentsAndChildren(existingPrefabContents, nodeGameObject, node);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"既存Prefabとの差分マージに失敗: {prefabAssetPath}\n{e}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(existingPrefabContents);
                }
            }

            
            var componentPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(nodeGameObject, prefabAssetPath, InteractionMode.UserAction);
            figmaImportProcessData.ComponentData.RegisterComponentPrefab(node.id,componentPrefab);
            var guid = AssetDatabase.AssetPathToGUID(prefabAssetPath);
            cacheMap.Add(node.id, guid, nodeName);
        }
        
        /// <summary>
        /// Instantiates all component prefabs in screens and components (for nested component support)
        /// </summary>
        /// <param name="figmaImportProcessData"></param>
        public static void InstantiateAllComponentPrefabs(FigmaImportProcessData figmaImportProcessData)
        {

            // Instantiate components "within" components (nested components)
            InstantiateComponentsInPrefabSet(figmaImportProcessData.ComponentData.AllComponentPrefabs,figmaImportProcessData,"Connecting nested components");
            // Instantiate components within screens
            InstantiateComponentsInPrefabSet(figmaImportProcessData.ScreenPrefabs,figmaImportProcessData,"Connecting screen components");
            // Instantiate components within pages
            InstantiateComponentsInPrefabSet(figmaImportProcessData.PagePrefabs,figmaImportProcessData,"Connecting page components");
        }

        /// <summary>
        /// Connects a set of components and provides feedback on progress
        /// </summary>
        /// <param name="prefabSet"></param>
        /// <param name="figmaImportProcessData"></param>
        /// <param name="progressTitle"></param>
        private static void InstantiateComponentsInPrefabSet(List<GameObject> prefabSet,FigmaImportProcessData figmaImportProcessData, string progressTitle)
        {
            for (var i = 0; i < prefabSet.Count; i++)
            {
                var targetPrefab = prefabSet[i];
                if (targetPrefab==null) continue;
                EditorUtility.DisplayProgressBar(UnityFigmaBridgeImporter.PROGRESS_BOX_TITLE, $"{progressTitle} {i}/{prefabSet.Count} ", (float)i/prefabSet.Count);
                InstantiateComponentPrefabs(targetPrefab, figmaImportProcessData);
            }
        }
        
        
        
        /// <summary>
        /// Instantiates prefabs within a given prefab
        /// 指定プレハブ内のネストしたプレハブを生成する
        /// </summary>
        /// <param name="sourcePrefab"></param>
        /// <param name="figmaImportProcessData"></param>
        private static void InstantiateComponentPrefabs(GameObject sourcePrefab, FigmaImportProcessData figmaImportProcessData)
        {
            var assetPath = AssetDatabase.GetAssetPath(sourcePrefab);
            var prefabContents = PrefabUtility.LoadPrefabContents(assetPath);
            // Get all placeholders within this prefab - these will be replaced
            // プレハブ置き換え用のマーカーを全て取得する
            var allPlaceholderComponents = prefabContents.GetComponentsInChildren<FigmaComponentNodeMarker>();
            
            // Filter out any that are replacements in prefab instances (we want to skip these)
            var targetPlaceHolderComponents = new List<FigmaComponentNodeMarker>();
            foreach (var t in allPlaceholderComponents)
            {
                var prefabInstanceRoot=PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject);
                if (prefabInstanceRoot==null) targetPlaceHolderComponents.Add(t);
                else
                {
                    // Debug.Log($"Prefab instance root found for object {t.gameObject.name}, skipping");
                }
            }


            // Track a list of placed and modified components, to allow effective saving
            var modifiedPrefabInstances = new List<GameObject>();
            foreach (var placeholder in targetPlaceHolderComponents)
            {
                var sourceComponentPrefab = figmaImportProcessData.ComponentData.GetComponentPrefab(placeholder.ComponentId);

                if (sourceComponentPrefab == null) continue;
                
                // Instantiate
                var addedReplacementComponent = (GameObject)PrefabUtility.InstantiatePrefab(sourceComponentPrefab,placeholder.transform.parent);
                
                // サイズ調整
                var instanceNode = figmaImportProcessData.NodeLookupDictionary[placeholder.NodeId];
                bool componentIs9Slice = false;
                var componentSize = Vector2.zero;
                if (ImportSessionCache.remoteComponentKeyDataMap.TryGetValue(placeholder.ComponentId, out var data))
                {
                    componentIs9Slice = data.componentName.Is9Slice();
                    componentSize = data.size;
                }
                else if(figmaImportProcessData.NodeLookupDictionary.TryGetValue(placeholder.ComponentId, out var componentNode))
                {
                    componentIs9Slice = componentNode.customCondition.Is9Slice();
                    componentSize = new Vector2(componentNode.size.x, componentNode.size.y);
                }
                
                // 先頭オブジェクトに関してはTransformのコピーではなくて、インスタンスのサイズ諸々から、Scaleの決定を行う
                var nodeRectTransform = placeholder.transform as RectTransform;
                NodeTransformManager.ApplyFigmaInstanceTopSize(
                    nodeRectTransform,
                    instanceNode,
                    componentIs9Slice,
                    componentSize,
                    nodeRectTransform.parent as RectTransform);
                
                LayoutElement layoutElement = addedReplacementComponent.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = addedReplacementComponent.AddComponent<LayoutElement>();
                }
                layoutElement.minWidth = layoutElement.preferredWidth = instanceNode.size.x;
                layoutElement.minHeight = layoutElement.preferredHeight = instanceNode.size.y;
                
                // Copy transform data
                UnityUiUtils.CloneTransformData(placeholder.transform as RectTransform, addedReplacementComponent.transform as RectTransform);
                // Copy name
                addedReplacementComponent.name = placeholder.name; // Copy original name
                
                // Change node Id to match instantiated version
                var figmaNodeComponent = addedReplacementComponent.GetComponent<FigmaNodeObject>();
                if (figmaNodeComponent == null)
                {
                    Debug.LogWarning("No FigmaNodeObject on component prefab");
                }
                else
                {
                    figmaNodeComponent.Initialise(placeholder.NodeId, placeholder.name);
                }
                
                // Copy transform order
                addedReplacementComponent.transform.SetSiblingIndex(placeholder.transform.GetSiblingIndex()); // Put at same order
                // Get the Node data for this component
                var nodeData = figmaImportProcessData.NodeLookupDictionary[placeholder.NodeId]; 
                // Get parent node data for the original node
                var parentNodeData =  figmaImportProcessData.NodeLookupDictionary[placeholder.ParentNodeId];
                if (nodeData != null)
                {
                    ApplyComponentProperties(nodeData, addedReplacementComponent, figmaImportProcessData);
                    
                    // Recursively apply all properties for this node object (such as text, image fills etc)
                    ApplyFigmaProperties(nodeData, addedReplacementComponent, parentNodeData, figmaImportProcessData);
                }

                // We want to attempt to link this newly placed item to any parent MonoBehaviours that might need it as a field
                // TODO - Optimise (Right now this is called way more often than needed)
                var parentMonoBehaviours = placeholder.transform.parent.gameObject.GetComponents<MonoBehaviour>();
                foreach (var monoBehaviour in parentMonoBehaviours)
                {
                    BehaviourBindingManager.BindFieldsForComponent(placeholder.transform.parent.gameObject,
                        monoBehaviour);
                }

                // Mark as modified for later saving
                modifiedPrefabInstances.Add(addedReplacementComponent);
                Object.DestroyImmediate(placeholder.gameObject); // Remove the placeholder

            }
            // Save prefab and all changes
            try
            {
                // We might have issue with nested elements so need try catch loop
                // TODO - Check for recurisve nested components
                PrefabUtility.SaveAsPrefabAsset(prefabContents, assetPath);
                
                // Apply changes to the instance as modifications
                foreach (var modifiedPrefabInstance in modifiedPrefabInstances)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(modifiedPrefabInstance);
                }
            }
            catch (Exception e)
            {
                Debug.Log($"Issue saving prefab: {e.ToString()}");
            }

            PrefabUtility.UnloadPrefabContents(prefabContents);
        }

        /// <summary>
        /// Recursively apply properties from a node to a given existing GameObject. Used to apply changes to component instances (including nested elements) 
        /// </summary>
        /// <param name="node"></param>
        /// <param name="nodeObject"></param>
        /// <param name="parentNode"></param>
        /// <param name="figmaImportProcessData"></param>
        private static void ApplyFigmaProperties(Node node, GameObject nodeObject,Node parentNode, FigmaImportProcessData figmaImportProcessData)
        {
            // サーバーレンダー画像による置き換えが行われた場合
            var isSubstitution = node.customCondition.IsServerRenderNode();
            // インスタンスの場合、コンポーネントも確認する
            if (node.type == NodeType.INSTANCE)
            {
                var componentNode = figmaImportProcessData.NodeLookupDictionary[node.componentId];
                isSubstitution |= componentNode.customCondition.IsServerRenderNode();
            }
            if (!isSubstitution)
            {
                try
                {
                    // Apply properties for this node Object (eg characters to text). Not needed if this is a substitution
                    FigmaNodeManager.ApplyUnityComponentPropertiesForNode(nodeObject, node, figmaImportProcessData);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        $"Exception applying properties for node '{FigmaDataUtils.GetFullPathForNode(node, figmaImportProcessData.SourceFile)}' - {e}",
                        nodeObject);
                }
            }

            // Apply prototype elements for this node as required (such as buttons etc
            PrototypeFlowManager.ApplyPrototypeFunctionalityToNode(node, nodeObject, figmaImportProcessData);
            
            // Apply layout properties to this node as required (eg vertical layout groups etc)
            FigmaLayoutManager.ApplyLayoutPropertiesForNode(nodeObject,node,figmaImportProcessData,out var scrollContentGameObject);
            
            // If this is a substitution, ignore children (as they wont exist) and apply absolute bounds transform (as rotation already applied)
            if (isSubstitution)
            {
                NodeTransformManager.ApplyAbsoluteBoundsFigmaTransform(nodeObject.transform as RectTransform,node,parentNode,true);
                return;
            }
            
            // Apply recursively for all children
            if (node.children == null) return;
            
            // Cycle through each Figma child node data for this node
            foreach (var childNode in node.children)
            {
                var matchingChildGameObject = FindMatchingChildForFigmaNode(childNode, nodeObject.transform);
                if (matchingChildGameObject != null)
                {
                    ApplyFigmaProperties(childNode, matchingChildGameObject, node, figmaImportProcessData);

                }
                else
                    Debug.Log($"Applying properties - Could not find child object {childNode.id} name {childNode.name} from parent node id {node.id} in parent transform {nodeObject.name}");
            }
        }

        /// <summary>
        /// Finds a child node with a specific figma node id
        /// </summary>
        /// <param name="childNode"></param>
        /// <param name="parentNodeTransform"></param>
        /// <returns></returns>
        private static GameObject FindMatchingChildForFigmaNode(Node childNode, Transform parentNodeTransform)
        {
            var nodeTransformChildrenCount =parentNodeTransform.childCount;
            var childNodeIdComponentRefId = childNode.id.Split(';').Last();
                    
            for (var childTestIndex = 0; childTestIndex < nodeTransformChildrenCount; childTestIndex++)
            {
                var childTransform = parentNodeTransform.transform.GetChild(childTestIndex);
                var childNodeObject = childTransform.GetComponent<FigmaNodeObject>();
                if (childNodeObject != null && childNodeObject.NodeId == childNodeIdComponentRefId)
                {
                    return childTransform.gameObject;
                }
            }

            return null;
        }

        private static void ApplyComponentProperties(Node nodeData, GameObject obj, FigmaImportProcessData figmaImportProcessData)
        {
            if (nodeData.componentId == null) return;

            var nodeComponentProperties = nodeData.componentProperties;
            if (nodeComponentProperties == null || nodeComponentProperties.Count <= 0) return;

            var componentData = figmaImportProcessData.NodeLookupDictionary[nodeData.componentId];
            if (componentData == null) return;

            var componentPropertyDefinitions = componentData.componentPropertyDefinitions;
            if (componentPropertyDefinitions == null || componentPropertyDefinitions.Count <= 0) return;


            foreach (var componentProperty in nodeComponentProperties)
            {
                var key = componentProperty.Key;
                var property = componentProperty.Value;

                switch (property.type)
                {
                    // インスタンス入れ替え
                    case ComponentPropertyType.INSTANCE_SWAP:
                        if (!componentPropertyDefinitions.TryGetValue(key, out var value))
                        {
                            break;
                        }

                        var nodeLookup = figmaImportProcessData.NodeLookupDictionary;
                        var markTargetName = "";

                        if (nodeLookup.TryGetValue(value.defaultValue, out var swapDefaultNode))
                        {
                            markTargetName = swapDefaultNode.name;
                        }
                        // 読み込み対象にデフォルトノードが存在していない場合は、マーカーを参照して置き換え対象を取得する
                        else
                        {
                            var componentMarkers = obj.GetComponentsInChildren<FigmaComponentNodeMarker>(true);
                            foreach (var componentMarker in componentMarkers)
                            {
                                if (componentMarker.ComponentId == value.defaultValue)
                                {
                                    markTargetName = componentMarker.name;
                                    break;
                                }
                            }
                        }
                        var replacementNode = figmaImportProcessData.NodeLookupDictionary[property.value];

                        var marker = obj.AddComponent<InstanceSwapMarker>();
                        var swapComponentPrefab = figmaImportProcessData.ComponentData.GetComponentPrefab(replacementNode.id);
                        marker.targetName = markTargetName;
                        marker.replacementPrefab = swapComponentPrefab;

                        break;
                }
            }
        }
        
        /// <summary>
        /// コンポ―ネントと子を同期する
        /// </summary>
        private static void SyncComponentsAndChildren(GameObject source, GameObject target, Node node)
        {
            Debug.Log($"==== コンポ―ネントと子を同期する SyncComponentsAndChildren called for source {source.name} and target {target.name} with node {node.name}");
            // Figma のノード情報を最新に保つ。
            // 差分Syncの一致判定に使うため、最初にメタデータを更新する。
            SyncNodeMetadata(source, target);
            SyncComponents(source, target);
            MergeNodeRecursive(source, target, node);
        }
        
         /// <summary>
         /// targetに存在しないコンポーネントを追加(マーカー系を除く)、
         /// 既に存在するコンポーネントはデータをコピー(CopySerialized)する
         /// </summary>
        public static void SyncComponents(GameObject source, GameObject target)
        {
            Debug.Log($"==== コンポーネントを追加 SyncComponents called for source {source.name} and target {target.name}");
            List<Component> sourceComponents = new List<Component>(
                source.GetComponents<Component>()
                    .Where(c => !SkipCopyComponentTypes.Contains(c.GetType())));// コピー対象でないコンポーネントを除く
            List<Component> targetComponents = new List<Component>(target.GetComponents<Component>());

            foreach (var comp in targetComponents)
            {
                Component deleteItem = null;
                var type1 = comp.GetType();
                
                foreach (var comp2 in sourceComponents)
                {
                    // 合致するコンポーネントがあったら、データをコピーして終了
                    if (type1 == comp2.GetType())
                    {
                        deleteItem = comp2;
                        CopyComponent(comp2,comp);
                        break;
                    }
                }

                // 合致したものはリストから削除する
                if (deleteItem != null)
                {
                    sourceComponents.Remove(deleteItem);
                }
            }
            // 全て見た後に残っているものがあれば追加する
            foreach (var comp2 in sourceComponents)
            {
                Type type = comp2.GetType();
                var component = target.AddComponent(type);
                if (component == null)
                {
                    if ((type == typeof(FigmaImage) || type == typeof(Image)) &&
                        target.GetComponent<Image>() is { } img)//nullチェック
                    {
                        img.CopyImage((Image)comp2, false);// SourceImageはFigmaが正なのでコピーしない
                        continue;
                    }
                }
                CopyComponent(comp2,component);
            }
        }

         /// <summary>
         /// コンポーネントのコピー処理
         /// 基本 EditorUtility.CopySerialized を利用
         /// 例外はこの関数内で定義
         /// </summary>
        private static void CopyComponent(Component source, Component target)
        {
            if(source == null || target == null) return;
            // imageの場合、画像は最新のものに更新する
            if (target is Image img)
            {
                var sprite = img.sprite;
                EditorUtility.CopySerialized(source,target);
                img.sprite = sprite;
                return;
            }
            EditorUtility.CopySerialized(source,target);
        }
        
        /// <summary>
        /// Figmaノードのメタデータを target に反映する。
        /// NodeId / NodeName を更新して次回Sync時の一致精度を上げる。
        /// </summary>
        private static void SyncNodeMetadata(GameObject source, GameObject target)
        {
            var sourceNodeObject = source.GetComponent<FigmaNodeObject>();
            if (sourceNodeObject == null)
            {
                return;
            }

            var targetNodeObject = target.GetComponent<FigmaNodeObject>();
            if (targetNodeObject == null)
            {
                targetNodeObject = target.AddComponent<FigmaNodeObject>();
            }

            targetNodeObject.Initialise(sourceNodeObject.NodeId, sourceNodeObject.NodeName);
        }
        
         /// <summary>
        /// 既存の子からNodeId/NodeNameベースの検索辞書を作る。
        /// 名前の重複にも対応するため値は複数保持する。
        /// </summary>
        private static Dictionary<string, List<Transform>> BuildChildNodeMap(Transform parent)
        {
            var map = new Dictionary<string, List<Transform>>();
            foreach (Transform child in parent)
            {
                var childNodeObject = child.GetComponent<FigmaNodeObject>();
                if (childNodeObject == null)
                {
                    continue;
                }

                var key = GetNodeSearchKey(childNodeObject.NodeId, childNodeObject.NodeName);
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (!map.TryGetValue(key, out var childList))
                {
                    childList = new List<Transform>();
                    map[key] = childList;
                }
                Debug.Log($" ==== 既存の子からNodeId/NodeNameベースの検索辞書を作る Adding child {child.name} to map with key {key}");
                childList.Add(child);
            }

            return map;
        }

        /// <summary>
        /// NodeId を優先し、取得できない場合のみ NodeName を使う。
        /// id + name の複合キーだと名前変更で一致しなくなるので一旦ID優先で見る
        /// 前方一致の誤判定を避けるため prefix を固定する。
        /// </summary>
        private static string GetNodeSearchKey(string nodeId, string nodeName)
        {
            if (!string.IsNullOrEmpty(nodeId))
            {
                return $"id:{nodeId}";
            }

            if (!string.IsNullOrEmpty(nodeName))
            {
                return $"name:{nodeName}";
            }

            return string.Empty;
        }

         /// <summary>
         /// 存在しない子があれば追加
         /// 存在していればコンポーネントのコピーを実施する
         /// </summary>
        private static void MergeNodeRecursive(GameObject source, GameObject target, Node node)
        {
            Debug.Log($" ==== 存在しない子があれば追加 Syncing children for source {source.name} and target {target.name} with node {node.name}");
            // 対象かソースが無効なら
            if(!target || !source)return;
            
            // コンポーネントノードの場合は追加しない
            var componentNodeMarker = target.GetComponent<FigmaComponentNodeMarker>();
            if (componentNodeMarker)
            {
                return;
            }
            
            var targetChildNodeMap = BuildChildNodeMap(target.transform);
            foreach (Transform sourceChild in source.transform)
            {
                var sourceChildNodeObject = sourceChild.GetComponent<FigmaNodeObject>();
                var sourceNodeId = sourceChildNodeObject != null ? sourceChildNodeObject.NodeId : string.Empty;
                var sourceNodeName = sourceChildNodeObject != null ? sourceChildNodeObject.NodeName : sourceChild.name;
                var nodeSearchKey = GetNodeSearchKey(sourceNodeId, sourceNodeName);

                var nodeChildren = node.children;
                var nodeChild = nodeChildren?.FirstOrDefault(n => n.id == sourceNodeId);
                if (nodeChild == null)
                {
                    nodeChild = nodeChildren?.FirstOrDefault(n => n.name == sourceNodeName);
                }

                // Nodeデータに存在しない場合は削除されたものとして無視する
                if (nodeChild == null)
                {
                    continue;
                }

                Transform targetChild = null;
                if (!string.IsNullOrEmpty(nodeSearchKey)
                    && targetChildNodeMap.TryGetValue(nodeSearchKey, out var targetChildList)
                    && targetChildList.Count > 0)
                {
                    targetChild = targetChildList[0];
                    targetChildList.RemoveAt(0);
                }

                if (targetChild == null)
                {
                    Debug.Log($" ==== 子が存在しなければコピーして追加する。 {sourceChild.name} with NodeId {sourceNodeId} and NodeName {sourceNodeName} under parent {target.name}. Adding as new child.");
                    // 子が存在しなければコピーして追加する。
                    // 追加時も NodeId / NodeName を持つので次回の差分Syncで再利用できる。
                    var copied = Object.Instantiate(sourceChild.gameObject, target.transform, false);
                    copied.name = sourceChild.name;
                    continue;
                }
                // すでに合致する子があれば再帰的にマージする。
                // ここでNodeメタデータも同期して一致判定の精度を維持する。
                Debug.Log($" ==== 子が存在すればコンポーネントをコピーしてプロパティを同期する。 {sourceChild.name} with NodeId {sourceNodeId} and NodeName {sourceNodeName} under parent {target.name}. Syncing properties and components.");
                SyncComponentsAndChildren(sourceChild.gameObject, targetChild.gameObject, nodeChild);
            }
        }

        /// <summary>
        /// コンポーネントコピー時に除外するタイプ (マーカー系のコンポーネントが主)
        /// </summary>
        private static readonly HashSet<Type> SkipCopyComponentTypes = new HashSet<Type>()
        {
            typeof(FigmaNodeObject),
            typeof(FigmaComponentNodeMarker),
            typeof(InstanceSwapMarker),
            typeof(FontMarker),
            typeof(RemoteComponentMarker),
            typeof(ButtonMarker),
            typeof(ToggleMarker),
            
            // 以下は常にFigmaの設定の方が正なので上書きしない
            typeof(RectTransform),
            typeof(TMP_Text),
            typeof(LayoutElement),
            typeof(LayoutGroup),
        };
    }
}