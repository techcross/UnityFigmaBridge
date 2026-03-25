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
        /// <returns>
        /// 既存Prefabが見つかり、差分マージを実行した場合は true。
        /// 対応するPrefabが存在しない、またはマージに失敗した場合は false。
        /// </returns>
        /// <param name="node">対応するPrefabを検索するためのFigmaノード</param>
        /// <param name="nodeGameObject">今回生成されたGameObject</param>
        /// <param name="path">既存オブジェクト読み込み先のパス</param>
        public static bool TryMergeWithExistingPrefab(Node node, GameObject nodeGameObject,string path)
        {
            if (node == null || nodeGameObject == null) return false;
            return TryMergeFromPrefabPath(path, node, nodeGameObject);
        }
        
        /// <summary>
        /// 生成済みのノードからコンポーネントPrefabを作成する
        /// </summary>
        /// <param name="node">元となるFigmaノード</param>
        /// <param name="parentNode">親ノード</param>
        /// <param name="nodeGameObject">生成されたGameObject</param>
        /// <param name="figmaImportProcessData">インポート処理で使用するデータ</param>
        public static void GenerateComponentAssetFromNode(Node node, Node parentNode, GameObject nodeGameObject, FigmaImportProcessData figmaImportProcessData)
        {
            if (ImportSessionCache.remoteComponentFlagMap.Contains(node.id)) return;

            var nodeName = parentNode is { type: NodeType.COMPONENT_SET }
                ? $"{parentNode.name}-{node.name}"
                : node.name;

            var componentCount = figmaImportProcessData.ComponentData.GetComponentNameCount(nodeName);
            figmaImportProcessData.ComponentData.IncrementComponentNameCount(nodeName, 1);

            var cacheMap = FigmaAssetGuidMapManager.CreateMap(FigmaAssetGuidMapManager.AssetType.Component);
            var prefabAssetPath = cacheMap.GetAssetPath(node.id);
            if (string.IsNullOrEmpty(prefabAssetPath))
            {
                prefabAssetPath = FigmaPaths.GetPathForComponentPrefab(nodeName, componentCount);
            }

            TryMergeFromPrefabPath(prefabAssetPath, node, nodeGameObject);

            var componentPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(nodeGameObject, prefabAssetPath, InteractionMode.UserAction);
            figmaImportProcessData.ComponentData.RegisterComponentPrefab(node.id, componentPrefab);

            var guid = AssetDatabase.AssetPathToGUID(prefabAssetPath);
            cacheMap.Add(node.id, guid, nodeName);
        }
        
        private static bool TryMergeFromPrefabPath(string prefabAssetPath, Node node, GameObject nodeGameObject)
        {
            if (string.IsNullOrEmpty(prefabAssetPath)) return false;
            if (!File.Exists(prefabAssetPath)) return false;
            if (node == null || nodeGameObject == null) return false;

            var existingPrefabContents = PrefabUtility.LoadPrefabContents(prefabAssetPath);
            try
            {
                Debug.Log($"[PrefabMerge] merge existing prefab path={prefabAssetPath}, node={node.name}, type={node.type}, id={node.id}");
                
                // 参照remapの基準rootはマージ対象のてっぺんで固定する。
                // source=既存Prefabルート / target=今回生成ルートを必ず渡す。
                SyncComponentsAndChildren(
                    existingPrefabContents,
                    nodeGameObject,
                    node,
                    existingPrefabContents.transform,
                    nodeGameObject.transform);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"既存Prefabとの差分マージに失敗: {prefabAssetPath}\n{e}");
                return false;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(existingPrefabContents);
            }
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
                    figmaNodeComponent = addedReplacementComponent.AddComponent<FigmaNodeObject>();
                }
                figmaNodeComponent.Initialise(placeholder.NodeId, placeholder.name);


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
                if (string.IsNullOrEmpty(node.componentId))
                {
                    Debug.LogWarning($"[Instance] componentId が null/empty node={node.name}");
                }
                else if (!figmaImportProcessData.NodeLookupDictionary.TryGetValue(node.componentId, out var componentNode))
                {
                    Debug.LogWarning($"[Instance] componentNode が見つからない id={node.componentId} node={node.name}");
                }
                else if (componentNode == null)
                {
                    Debug.LogWarning($"[Instance] componentNode が null id={node.componentId} node={node.name}");
                }
                else if (componentNode.customCondition == null)
                {
                    Debug.LogWarning($"[Instance] customCondition が null id={node.componentId} node={node.name}");
                }
                else
                {
                    Debug.Log($"[Instance] substitution check id={node.componentId} node={node.name}");
                    isSubstitution |= componentNode.customCondition.IsServerRenderNode();
                }
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
        /// <param name="source">既存Prefabのオブジェクト</param>
        /// <param name="target">今回生成されたオブジェクト</param>
        /// <param name="node">Figmaノード情報</param>
        private static void SyncComponentsAndChildren(GameObject source, GameObject target, Node node)
        {
            if (source == null || target == null)
            {
                return;
            }

            // 差分同期の基準rootは最初の呼び出し時に固定する。
            // 子の再帰に入ってもこのrootを使い続けることで参照先のズレを防ぐ。
            SyncComponentsAndChildren(source, target, node, source.transform, target.transform);
        }

        /// <summary>
        /// 参照remapに使うrootを固定したまま再帰同期する。
        /// </summary>
        private static void SyncComponentsAndChildren(
            GameObject source,
            GameObject target,
            Node node,
            Transform fixedSourceRoot,
            Transform fixedTargetRoot)
        {
            if (source == null || target == null)
            {
                return;
            }
            Debug.Log($"==== コンポ―ネントと子を同期する SyncComponentsAndChildren called for source {source.name} and target {target.name} with node {node.name}");
            // Figma のノード情報を最新に保つ。
            // 既存オブジェクト(target)に source 側の NodeId / NodeName を反映する。
            SyncSelf(source, target, fixedSourceRoot, fixedTargetRoot);
            // 子同期でもrootは固定のまま渡す。
            MergeNodeRecursive(source, target, node, fixedSourceRoot, fixedTargetRoot);
        }
        
         /// <summary>
         /// targetに存在しないコンポーネントを追加(マーカー系を除く)、
         /// 既に存在するコンポーネントはデータをコピー(CopySerialized)する
         /// </summary>
         public static void SyncComponents(GameObject source, GameObject target, Transform fixedSourceRoot, Transform fixedTargetRoot)
         {
             var sourceComponents = source.GetComponents<Component>()
                 .Where(c => c != null && !SkipCopyComponentTypes.Contains(c.GetType()))
                 .ToList();

             var targetComponents = target.GetComponents<Component>()
                 .Where(c => c != null)
                 .ToList();

             foreach (var sourceComponent in sourceComponents)
             {
                 var type = sourceComponent.GetType();
                 var targetComponent = targetComponents.FirstOrDefault(c => c.GetType() == type);

                 if (targetComponent != null)
                 {
                     CopyComponent(sourceComponent, targetComponent, fixedSourceRoot, fixedTargetRoot);
                 }
                 else
                 {
                     var added = target.AddComponent(type);
                     CopyComponent(sourceComponent, added, fixedSourceRoot, fixedTargetRoot);
                 }
             }
         }

         /// <summary>
         /// 既存呼び出し互換用。root指定なしの場合は自身をrootとして同期する。
         /// </summary>
         public static void SyncComponents(GameObject source, GameObject target)
         {
             if (source == null || target == null)
             {
                 return;
             }

             SyncComponents(source, target, source.transform, target.transform);
         }
         
         
        /// <summary>
        /// コンポーネントのコピー処理
        /// 既存PrefabのComponentをDLしたオブジェクトへ上書きする
        /// 基本 EditorUtility.CopySerialized を利用
        /// 例外はこの関数内で定義
        /// </summary>
        private static void CopyComponent(Component source, Component target, Transform sourceRoot, Transform targetRoot)
        {
            if (source == null || target == null) return;

            // TMP_Text は文字列だけFigmaの内容を優先する。
            if (target is TMP_Text targetText)
            {
                var message = targetText.text;
                var material = targetText.material;
                EditorUtility.CopySerialized(source, target);
                RemapInternalReferences(source, target, sourceRoot, targetRoot);
                targetText.text = message;
                targetText.material = material;
                return;
            }
            // imageの場合、画像は最新のものに更新する
            if (target is Image img)
            {
                var sprite = img.sprite;
                var material = img.material;
                EditorUtility.CopySerialized(source, target);
                RemapInternalReferences(source, target, sourceRoot, targetRoot);
                img.sprite = sprite;
                img.material = material;
                return;
            }

            //sourceをTargetにコピー
            EditorUtility.CopySerialized(source, target);
            // コピー後、source 側 subtree 内を指している参照を target 側 subtree の対応オブジェクトへ張り替える
            RemapInternalReferences(source, target, sourceRoot, targetRoot);
        }

        /// <summary>
        /// CopySerialized 後、source 側 subtree 内を指している参照を
        /// target 側 subtree の対応オブジェクトへ張り替える
        /// </summary>
        private static void RemapInternalReferences(Component source, Component target, Transform sourceRoot, Transform targetRoot)
        {
            var so = new SerializedObject(target);
            var prop = so.GetIterator();

            var enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (prop.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                var refObj = prop.objectReferenceValue;
                if (refObj == null)
                    continue;

                // prefab内の子参照だけを再マップ対象にする
                if (!(refObj is Component) && !(refObj is GameObject))
                    continue;

                var sourceTransform = GetReferencedTransform(refObj);
                if (sourceTransform == null)
                    continue;

                // 今同期している source subtree 外の参照は触らない
                if (!IsChildOf(sourceTransform, sourceRoot))
                    continue;

                var remapped = FindMatchingObjectInTarget(sourceTransform, sourceRoot, targetRoot, refObj.GetType());
                if (remapped != null)
                {
                    prop.objectReferenceValue = remapped;
                }
                else
                {
                    Debug.LogWarning($"[Remap] failed: prop={prop.propertyPath}, ref={refObj.name}, type={refObj.GetType().Name}");
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
        
        private static Object FindMatchingObjectInTarget(Transform sourceRef, Transform sourceRoot, Transform targetRoot, Type refType)
        {
            var relativePath = GetRelativePath(sourceRoot, sourceRef);
            var targetTransform = FindByRelativePath(targetRoot, relativePath);

            if (targetTransform == null)
            {
                Debug.LogWarning($"[Remap] target not found by path: {relativePath}");
                return null;
            }

            if (refType == typeof(GameObject))
                return targetTransform.gameObject;

            if (refType == typeof(Transform))
                return targetTransform;

            if (refType == typeof(RectTransform))
                return targetTransform as RectTransform;

            if (typeof(Component).IsAssignableFrom(refType))
                return targetTransform.GetComponent(refType);

            return null;
        }
        
        private static string GetRelativePath(Transform root, Transform target)
        {
            if (target == root) return string.Empty;

            var stack = new Stack<string>();
            var current = target;

            while (current != null && current != root)
            {
                stack.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", stack);
        }

        private static Transform FindByRelativePath(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path)) return root;
            return root.Find(path);
        }

        private static Transform GetReferencedTransform(Object obj)
        {
            if (obj is GameObject go) return go.transform;
            if (obj is Component comp) return comp.transform;
            return null;
        }

        private static bool IsChildOf(Transform child, Transform root)
        {
            var current = child;
            while (current != null)
            {
                if (current == root) return true;
                current = current.parent;
            }
            return false;
        }

        private static void SyncSelf(GameObject source, GameObject target, Transform fixedSourceRoot, Transform fixedTargetRoot)
        {
            SyncNodeMetadataComponent(source, target);
            SyncComponents(source, target, fixedSourceRoot, fixedTargetRoot);
        }
        
        private static void SyncNodeMetadataComponent(GameObject source, GameObject target)
        {
            var sourceNodeObject = EnsureNodeObject(source.transform);
            var targetNodeObject = EnsureNodeObject(target.transform);
            sourceNodeObject.Initialise(targetNodeObject.NodeId, targetNodeObject.NodeName);
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
        /// 新規Figma に存在せず 既存オブジェクト にだけ存在する子は削除する
        /// </summary>
        private static void MergeNodeRecursive(GameObject source, GameObject target, Node node, Transform fixedSourceRoot, Transform fixedTargetRoot)
        {
            Debug.Log($"[Merge] {source.name} -> {target.name}");

            if (!source || !target) return;
            if (target.GetComponent<FigmaComponentNodeMarker>()) return;
            if (node.children == null) return;

            var sourceInfos = BuildSourceChildInfos(source, node);
            var remainingTargets = GetChildren(target);

            // ① IDで全件マッチ
            MatchById(sourceInfos, remainingTargets);

            // ② Nameで未マッチをマッチ
            MatchByName(sourceInfos, remainingTargets);

            // ③ 既存と同じを同期 or 新規追加
            ApplyOrCreate(sourceInfos, target, fixedSourceRoot, fixedTargetRoot);

            // ④ 余った既存オブジェクト削除
            RemoveUnusedTargets(remainingTargets);
        }
        
        private static List<SourceChildInfo> BuildSourceChildInfos(GameObject source,  Node node)
        {
            var list = new List<SourceChildInfo>();            
            //子を登録
            foreach (Transform child in source.transform)
            {
                var nodeObj = EnsureNodeObject(child);

                var id = nodeObj.NodeId;
                var name = string.IsNullOrEmpty(nodeObj.NodeName) ? child.name : nodeObj.NodeName;

                var nodeChild = FindNodeChild(node, id, name);
                if (nodeChild == null) continue;

                list.Add(new SourceChildInfo
                {
                    Source = child,
                    Node = nodeChild,
                    Id = id,
                    Name = name
                });
            }

            return list;
        }
        
        private static void MatchById(List<SourceChildInfo> sources, List<Transform> targets)
        {
            foreach (var s in sources)
            {
                if (string.IsNullOrEmpty(s.Id)) continue;

                var match = targets.FirstOrDefault(t => GetNodeId(t) == s.Id);
                if (match == null) continue;

                s.Target = match;
                targets.Remove(match);

                Debug.Log($"[Match-ID] {s.Name}");
            }
        }
        
        private static void MatchByName(List<SourceChildInfo> sources, List<Transform> targets)
        {
            foreach (var s in sources)
            {
                if (s.Target != null) continue;
                if (string.IsNullOrEmpty(s.Name)) continue;

                var match = targets.FirstOrDefault(t => GetNodeName(t) == s.Name);
                if (match == null) continue;

                s.Target = match;
                targets.Remove(match);

                Debug.Log($"[Match-Name] {s.Name}");
            }
        }

        private static void ApplyOrCreate(List<SourceChildInfo> sources, GameObject target, Transform fixedSourceRoot, Transform fixedTargetRoot)
        {
            foreach (var s in sources)
            {
                if (s.Target != null)
                {
                    Debug.Log($"[既存と同期] {s.Source.name}");
                    SyncComponentsAndChildren(s.Source.gameObject, s.Target.gameObject, s.Node);
                    SyncComponentsAndChildren(s.Source.gameObject, s.Target.gameObject, s.Node, fixedSourceRoot, fixedTargetRoot);
                }
                else
                {
                    var copy = Object.Instantiate(s.Source.gameObject, target.transform, false);
                    copy.name = s.Source.name;
                    Debug.Log($"[既存にないので作成 Create] {copy.name}");
                }
            }
        }
        
        private static void RemoveUnusedTargets(List<Transform> targets)
        {
            foreach (var t in targets)
            {
                Debug.Log($"[余った既存を削除 Delete] {t.name}");
                Object.DestroyImmediate(t.gameObject);
            }
        }
        
        private static List<Transform> GetChildren(GameObject obj)
        {
            return obj.GetComponentsInChildren<Transform>()
              .Where(t => t != obj.transform)
              .ToList();
        }

        private static FigmaNodeObject EnsureNodeObject(Transform t)
        {
            var node = t.GetComponent<FigmaNodeObject>();
            if (node != null) return node;

            Debug.Log("既存にないのでFigmaNodeObject新規" + t.name);
            node = t.gameObject.AddComponent<FigmaNodeObject>();
            node.Initialise("", t.name);
            return node;
        }

        private static Node FindNodeChild(Node parent, string id, string name)
        {
            if (!string.IsNullOrEmpty(id))
            {
                var byId = parent.children.FirstOrDefault(n => n.id == id);
                if (byId != null) return byId;
            }

            if (!string.IsNullOrEmpty(name))
            {
                return parent.children.FirstOrDefault(n => n.name == name);
            }

            return null;
        }

        private static string GetNodeId(Transform t)
        {
            return t.GetComponent<FigmaNodeObject>()?.NodeId;
        }

        private static string GetNodeName(Transform t)
        {
            var node = t.GetComponent<FigmaNodeObject>();
            return !string.IsNullOrEmpty(node?.NodeName) ? node.NodeName : t.name;
        }
        
        private class SourceChildInfo
        {
            public Transform Source;
            public Transform Target;
            public Node Node;
            public string Id;
            public string Name;
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
            typeof(LayoutElement),
            typeof(LayoutGroup),
        };
        
    }
}