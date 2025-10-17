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


            var name = nodeGameObject.name;
            
            // ボタン
            if (name.StartsWith("Btn"))
            {
                ButtonAttach(nodeGameObject, name, figmaImportProcessData.SourceFile.name);
            }
            // トグル
            else if (name.StartsWith("Toggle"))
            {
                ToggleAttach(nodeGameObject, name, figmaImportProcessData.SourceFile.name);
            }
            // チェックボックス
            else if (name.StartsWith("CheckboxGroup"))
            {
                var checkBoxFlagManager = UnityUiUtils.GetOrAddComponent<CheckBoxFlagManager>(nodeGameObject);
                checkBoxFlagManager.AutoAssignee();
            }
            // ラジオボタン
            else if (name.StartsWith("RadioGroup"))
            {
                var radioButtonManager = UnityUiUtils.GetOrAddComponent<RadioButtonManager>(nodeGameObject);
                radioButtonManager.AutoAssignee();
            }
            // タブ
            else if (name.StartsWith("TabGroup"))
            {
                TabAttach(nodeGameObject);
            }
            // ドロップダウン
            else if (name.StartsWith("Dropdown"))
            {
                DropdownAttach(nodeGameObject);
            }
            // 入力フィールド(一行)
            else if (name.StartsWith("TextAreaOne"))
            {
                InputField(nodeGameObject, isOneLine: true);
            }
            // 入力フィールド(複数行)
            else if (name.StartsWith("TextAreaMulti"))
            {
                InputField(nodeGameObject, isOneLine: false);
            }

            // TODO:ここにコンポーネントアタッチの仕組みを記載する

            if (!figmaImportProcessData.Settings.BuildPrototypeFlow) return;

            // Implement button if it has a prototype connection attached
            if (string.IsNullOrEmpty(node.transitionNodeID)) return;

            var prototypeFlowButton = nodeGameObject.GetComponent<FigmaPrototypeFlowButton>();
            if (prototypeFlowButton == null)
                prototypeFlowButton = nodeGameObject.AddComponent<FigmaPrototypeFlowButton>();
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

        private static void ButtonAttach(GameObject gameObject, string objName, string figmaFileName)
        {
            var buttonMarker = UnityUiUtils.GetOrAddComponent<ButtonMarker>(gameObject);
            // ファイル名.ボタン名 (Btnをのぞく)
            var commandKey = MakeCommandKey(figmaFileName, objName.Substring(3));
            ImportSessionCache.AddCommandKey(commandKey);
            buttonMarker.commandKey = commandKey;
            foreach (Transform child in gameObject.transform)
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
        }

        private static void ToggleAttach(GameObject gameObject, string objName, string figmaFileName)
        {
            var toggleMarker = UnityUiUtils.GetOrAddComponent<ToggleMarker>(gameObject);
            // ファイル名.ボタン名 (Toggleをのぞく)
            var commandKey = MakeCommandKey(figmaFileName, objName.Substring(6));
            ImportSessionCache.AddCommandKey(commandKey);
            toggleMarker.commandKey = commandKey;
        }

        private static void TabAttach(GameObject gameObject)
        {
            var toggleGroup = UnityUiUtils.GetOrAddComponent<ToggleGroup>(gameObject);
            toggleGroup.allowSwitchOff = false;
            var children = gameObject.GetComponentsInChildren<Transform>(true);
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
        }


        private static void DropdownAttach(GameObject gameObject)
        {
            var dropdown = UnityUiUtils.GetOrAddComponent<TMP_Dropdown>(gameObject);
            UnityUiUtils.GetOrAddComponent<DropDownArrowController>(gameObject);
            // ドロップダウンの要素初期化
            dropdown.options.Clear();
            dropdown.targetGraphic = gameObject.GetComponent<Image>();
            var basePos = dropdown.transform.position;

            foreach (RectTransform child in gameObject.transform)
            {
                switch (child.name)
                {
                    case "Label":
                        dropdown.captionText = child.GetComponent<TMP_Text>();
                        break;
                    case "PanelList":
                        dropdown.template = child;
                        var scrollRect = UnityUiUtils.GetOrAddComponent<ScrollRect>(child);
                        var panelListChildren = child.GetComponentsInChildren<RectTransform>(true);
                        bool isFirstItem = true;
                        foreach (var grandChild in panelListChildren)
                        {
                            if (!grandChild) continue;

                            switch (grandChild.name)
                            {
                                // 展開時の背面画像
                                case "ContentBackGround":
                                    grandChild.ApplyAnchorPreset(UnityUiUtils.AnchorPreset.StretchFull);
                                    // 親サイズに合わせる
                                    grandChild.anchoredPosition = Vector2.zero;
                                    grandChild.sizeDelta = Vector2.zero;
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
        }

        private static void InputField(GameObject gameObject, bool isOneLine)
        {
            var inputField = UnityUiUtils.GetOrAddComponent<TMP_InputField>(gameObject);
            UnityUiUtils.GetOrAddComponent<RectMask2D>(gameObject);

            foreach (RectTransform child in gameObject.transform)
            {
                switch (child.name)
                {
                    // テキストエリア
                    case "TextArea":
                        inputField.textViewport = child;
                        foreach (Transform grandChild in child)
                        {
                            var text = grandChild.GetComponent<TMP_Text>();
                            // 初期テキスト
                            if (grandChild.name.Equals("SampleText"))
                            {
                                inputField.placeholder = text;
                                continue;
                            }

                            // 入力テキスト
                            if (grandChild.name.Equals("InputText"))
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
                if (isOneLine)
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
    }
}