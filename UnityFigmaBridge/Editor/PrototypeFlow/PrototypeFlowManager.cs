using UnityEngine;
using UnityEngine.UI;
using UnityFigmaBridge.Editor.FigmaApi;
using UnityFigmaBridge.Editor.Nodes.DataMarker;
using UnityFigmaBridge.Runtime.UI;
using Color = UnityEngine.Color;

namespace UnityFigmaBridge.Editor.PrototypeFlow
{
    public static class PrototypeFlowManager
    {
        /// <summary>
        /// Add in prototype flow functionality for this node, if required
        /// </summary>
        /// <param name="node"></param>
        /// <param name="nodeGameObject"></param>
        /// <param name="figmaImportProcessData"></param>
        public static void ApplyPrototypeFunctionalityToNode(Node node, GameObject nodeGameObject,
            FigmaImportProcessData figmaImportProcessData)
        {

            if (CheckAddButtonBehaviour(node, figmaImportProcessData))
            {
                if (nodeGameObject.GetComponent<Button>() == null)
                {
                    var newButtonComponent = nodeGameObject.AddComponent<Button>();

                    // Find target graphic if appropriate for showing selected state
                    for (var i = 0; i < nodeGameObject.transform.childCount; i++)
                    {
                        var child = nodeGameObject.transform.GetChild(i);
                        if (child.name.ToLower().Contains("selected"))
                        {
                            newButtonComponent.targetGraphic = child.GetComponent<Graphic>();
                            newButtonComponent.transition = Selectable.Transition.ColorTint;
                            newButtonComponent.colors = new ColorBlock
                            {
                                disabledColor = new Color(0, 0, 0, 0),
                                normalColor = new Color(0, 0, 0, 0),
                                highlightedColor = Color.white,
                                pressedColor = Color.white,
                                selectedColor = Color.white,
                                colorMultiplier = 1,
                            };
                        }
                    }
                }
            }

            do
            {
                var name = nodeGameObject.name;

                #region ボタン

                if (name.StartsWith("Btn"))
                {
                    var buttonMarker = UnityUiUtils.GetOrAddComponent<ButtonMarker>(nodeGameObject);
                    // ファイル名.ボタン名 (Btnをのぞく)
                    var commandKey = MakeCommandKey(figmaImportProcessData.SourceFile.name, name.Substring(3));
                    ImportSessionCache.AddCommandKey(commandKey);
                    buttonMarker.commandKey = commandKey;
                    foreach (Transform child in nodeGameObject.transform)
                    {
                        var childName = child.name;
                        switch (childName)
                        {
                            case "Icon":
                                buttonMarker.iconObj = child.gameObject;
                                break;
                            case "Label":
                                if (child.GetComponent<TMP_Text>() is TMP_Text label)
                                {
                                    buttonMarker.labelText = label;
                                    buttonMarker.labelName = label.text;
                                }

                                break;
                            // case "Base":
                            //     break;
                        }
                    }
                    break;
                }

                #endregion // ボタン
                // トグル
                if (name.StartsWith("Toggle"))
                {
                    var toggleMarker = UnityUiUtils.GetOrAddComponent<ToggleMarker>(nodeGameObject);
                    // ファイル名.ボタン名 (Toggleをのぞく)
                    var commandKey = MakeCommandKey(figmaImportProcessData.SourceFile.name, name.Substring(6));
                    ImportSessionCache.AddCommandKey(commandKey);
                    toggleMarker.commandKey = commandKey;
                    break;
                }
            } while (false);
            // TODO:ここにコンポーネントアタッチの仕組みを記載する

            if (!figmaImportProcessData.Settings.BuildPrototypeFlow) return;
            
            // Implement button if it has a prototype connection attached
            if (string.IsNullOrEmpty(node.transitionNodeID)) return;
            
            var prototypeFlowButton = nodeGameObject.GetComponent<FigmaPrototypeFlowButton>();
            if (prototypeFlowButton == null) prototypeFlowButton = nodeGameObject.AddComponent<FigmaPrototypeFlowButton>();
            prototypeFlowButton.TargetScreenNodeId = node.transitionNodeID;
            // Future options to add transition information
        }

        private static bool CheckAddButtonBehaviour(Node node, FigmaImportProcessData figmaImportProcessData)
        {
            // Apply rules
            if (node.name.ToLower().Contains("button")) return true;
            if (figmaImportProcessData.Settings.BuildPrototypeFlow && !string.IsNullOrEmpty(node.transitionNodeID))
                return true;
            return false;
        }
        
        private static Transform[] GetAllChildTransform(GameObject gameObject)
        {
            return gameObject.GetComponentsInChildren<Transform>(true);
        }

        private static string MakeCommandKey(string fileName, string command)
        {
            var commandKey = $"{fileName}.{command}";
            commandKey = commandKey.Replace("/", "_")
                .Replace(" ", "_")
                .Replace("　", "_");
            return commandKey;
        }
    }
}