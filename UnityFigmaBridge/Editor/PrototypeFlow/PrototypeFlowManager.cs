using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityFigmaBridge.Editor.Extension.ImportCache;
using UnityFigmaBridge.Editor.FigmaApi;
using UnityFigmaBridge.Editor.Nodes.DataMarker;
using UnityFigmaBridge.Editor.Utils;
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

            // デフォルトボタン
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
                
                #region トグル系 (チェックボックス・ラジオボタン・タブなど)
                
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
                
                // チェックボックス
                if (name.StartsWith("CheckboxGroup"))
                {
                    var checkBoxFlagManager = UnityUiUtils.GetOrAddComponent<CheckBoxFlagManager>(nodeGameObject);
                    checkBoxFlagManager.AutoAssignee();
                    break;
                }

                // ラジオボタン
                if (name.StartsWith("RadioGroup"))
                {
                    var radioButtonManager = UnityUiUtils.GetOrAddComponent<RadioButtonManager>(nodeGameObject);
                    radioButtonManager.AutoAssignee();
                    break;
                }
                
                // タブ
                if (name.StartsWith("TabGroup"))
                {
                    var toggleGroup = UnityUiUtils.GetOrAddComponent<ToggleGroup>(nodeGameObject);
                    toggleGroup.allowSwitchOff = false;
                    var children = nodeGameObject.GetComponentsInChildren<Transform>(true);
                    foreach (var child in children)
                    {
                        var childName = child.name;
                        if (childName.StartsWith("Tab"))
                        {
                            var toggle = UnityUiUtils.GetOrAddComponent<Toggle>(child);
                            toggle.group = toggleGroup;
                            toggle.graphic = child.GetComponentInChildren<Image>();
                        }
                    }

                    break;
                }
                
                #endregion // トグル系 (チェックボックス・ラジオボタン・タブなど)

                #region ドロップダウン

                if (name.StartsWith("Dropdown"))
                {
                    var dropdown = UnityUiUtils.GetOrAddComponent<TMP_Dropdown>(nodeGameObject);
                    UnityUiUtils.GetOrAddComponent<DropDownArrowController>(nodeGameObject);
                    // ドロップダウンの要素初期化
                    dropdown.options.Clear();
                    dropdown.targetGraphic = nodeGameObject.GetComponent<Image>();
                    var basePos = dropdown.transform.position;

                    foreach (RectTransform child in nodeGameObject.transform)
                    {
                        switch (child.name)
                        {
                            case "Label":
                                dropdown.captionText = child.GetComponent<TMP_Text>();
                                break;
                            case "PanelList":
                                dropdown.template = child;
                                var scrollRect = UnityUiUtils.GetOrAddComponent<ScrollRect>(child);
                                var baseToList = child.position - basePos;
                                var sizeUnitY = child.rect.height * child.pivot.y;
                                // 下方向展開の場合
                                if (baseToList.y < 0)
                                {
                                    child.pivot = new Vector2(child.pivot.x, 1.0f);
                                    child.localPosition += new Vector3(0, sizeUnitY, 0);
                                }
                                // 上方向展開の場合
                                else
                                {
                                    child.pivot = new Vector2(child.pivot.x, 0.0f);
                                    child.localPosition += new Vector3(0, -sizeUnitY, 0);
                                }

                                var grandChildren = child.GetComponentsInChildren<RectTransform>(true);
                                bool isFirstItem = true;
                                foreach (var grandChild in grandChildren)
                                {
                                    if (!grandChild) continue;

                                    switch (grandChild.name)
                                    {
                                        // 展開時の背面画像
                                        case "ContentBackGround":
                                            grandChild.ApplyAnchorPreset(UnityUiUtils.AnchorPreset.StretchFull);
                                            break;
                                        // 展開部分表示領域
                                        case "ViewPort":
                                            UnityUiUtils.GetOrAddComponent<RectMask2D>(grandChild);
                                            scrollRect.viewport = grandChild;
                                            break;
                                        // コンテンツ部分
                                        case "Content":
                                            scrollRect.content = grandChild;
                                            break;
                                        // スクロールバー(縦)
                                        case "ScrollbarVertical":
                                            var scrollbar = UnityUiUtils.GetOrAddComponent<Scrollbar>(grandChild);
                                            scrollRect.verticalScrollbar = scrollbar;

                                            foreach (RectTransform scrollbarChild in grandChild)
                                            {
                                                if (scrollbarChild.name.Equals("Handle"))
                                                {
                                                    scrollbar.targetGraphic = scrollbarChild.GetComponent<Image>();
                                                    scrollbar.handleRect = scrollbarChild;
                                                    break;
                                                }
                                            }

                                            break;
                                        default:
                                            if (!grandChild.name.StartsWith("Item"))
                                            {
                                                break;
                                            }

                                            // アイテム

                                            // 項目名を収集
                                            var itemLabel = grandChild.GetComponentInChildren<TMP_Text>(true);
                                            if (itemLabel != null)
                                            {
                                                var optionData = new TMP_Dropdown.OptionData(itemLabel.text);
                                                dropdown.options.Add(optionData);
                                            }

                                            // 最初のアイテムだった場合、テンプレ用にトグルを設定
                                            if (isFirstItem)
                                            {
                                                isFirstItem = false;
                                                var toggle = UnityUiUtils.GetOrAddComponent<Toggle>(grandChild);

                                                foreach (RectTransform itemChild in grandChild)
                                                {
                                                    switch (itemChild.name)
                                                    {
                                                        // チェックマーク
                                                        case "ItemCheckmark":
                                                            itemChild.gameObject.SetActive(true);
                                                            var checkMarkImage = itemChild.GetComponent<Image>();
                                                            toggle.graphic = checkMarkImage;
                                                            break;
                                                        // ホバー画像
                                                        case "ItemBackGround":
                                                            itemChild.gameObject.SetActive(true);
                                                            var hoverImage = itemChild.GetComponent<Image>();
                                                            if (hoverImage)
                                                            {
                                                                toggle.targetGraphic = itemChild.GetComponent<Image>();
                                                                toggle.transition = Selectable.Transition.ColorTint;
                                                                var colorBlock = toggle.colors;
                                                                // ホバー画像なので、通常時透明、選択時白にする
                                                                colorBlock.normalColor = Color.clear;
                                                                colorBlock.highlightedColor = Color.clear;
                                                                colorBlock.pressedColor = Color.white;
                                                                colorBlock.selectedColor = Color.white;
                                                                colorBlock.disabledColor = Color.clear;
                                                                toggle.colors = colorBlock;
                                                            }

                                                            break;
                                                    }
                                                }

                                                break;
                                            }

                                            // 最初以外のアイテムはテンプレートにはいらないので、削除
                                            Object.DestroyImmediate(grandChild.gameObject);
                                            break;
                                    }
                                }

                                break;
                        }
                    }

                    break;
                }

                #endregion // ドロップダウン
                
                #region 入力フィールド

                bool isTextAreaOne = name.StartsWith("TextAreaOne");
                if (isTextAreaOne || name.StartsWith("TextAreaMulti"))
                {
                    var inputField = UnityUiUtils.GetOrAddComponent<TMP_InputField>(nodeGameObject);
                    UnityUiUtils.GetOrAddComponent<RectMask2D>(nodeGameObject);
                    
                    foreach (Transform child in nodeGameObject.transform)
                    {
                        switch (child.name)
                        {
                            // テキストエリア
                            case "TextArea":
                                inputField.textViewport = child as RectTransform;
                                foreach (Transform grandChild in child)
                                {
                                    var text = grandChild.GetComponent<TMP_Text>();
                                    // 初期テキスト
                                    if(grandChild.name.Equals("SampleText"))
                                    {
                                        inputField.placeholder = text;
                                        continue;
                                    }
                                    
                                    // 入力テキスト
                                    if(grandChild.name.Equals("InputText"))
                                    {
                                        inputField.textComponent = text;
                                        continue;
                                    }
                                }
                                break;
                            // 背景
                            case "Base":
                                inputField.targetGraphic = child.GetComponent<Image>();
                                break;
                        }
                        
                        // 一行テキストの場合
                        if (isTextAreaOne)
                        {
                            inputField.lineType = TMP_InputField.LineType.SingleLine;
                        }
                        // 複数行テキストの場合
                        else
                        {
                            inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
                        }

                        break;
                    }
                }

                #endregion // 入力フィールド

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