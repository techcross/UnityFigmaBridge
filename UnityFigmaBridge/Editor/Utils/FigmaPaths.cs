using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityFigmaBridge.Editor.Extension.ImportCache;
using UnityFigmaBridge.Editor.FigmaApi;

namespace UnityFigmaBridge.Editor.Utils
{
    public static class FigmaPaths
    {
        /// <summary>
        /// Assets/Figma
        /// </summary>
        public static readonly string FigmaAssetsRootFolder = "Assets/Figma";

        /// <summary>
        /// フォントは共通管理
        /// Assets/Figma/Font
        /// </summary>
        public static readonly string FigmaFontsFolder = $"{FigmaAssetsRootFolder}/Font";

        /// <summary>
        /// 共有Custom
        /// Assets/Figma/Custom
        /// </summary>
        public static readonly string FigmaSharedCustomFolder = $"{FigmaAssetsRootFolder}/Custom";

        /// <summary>
        /// バックアップを取るのに使用するフォルダ
        /// Assets/Figma/Custom/Backup
        /// </summary>
        private static readonly string FigmaCustomBackupFolder = $"{FigmaSharedCustomFolder}/Backup";

        /// <summary>
        /// 現在処理中のFigmaファイル名
        /// SetCurrentFigmaFileName() で設定する
        /// </summary>
        private static string currentFigmaFileName = "Default";

        /// <summary>
        /// 現在処理中のFigmaファイル名を安全な文字列で設定
        /// </summary>
        public static void SetCurrentFigmaFileName(string figmaFileName)
        {
            if (string.IsNullOrEmpty(figmaFileName))
            {
                currentFigmaFileName = "Default";
                Debug.LogWarning("Figma file name is empty. Using default name 'Default'.");
                return;
            }

            currentFigmaFileName = MakeValidFileName(figmaFileName.Trim());
        }

        /// <summary>
        /// Assets/Figma/{Figmaファイル名}
        /// </summary>
        public static string FigmaFileRootFolder => $"{FigmaAssetsRootFolder}/{currentFigmaFileName}";

        /// <summary>
        /// Assets/Figma/{Figmaファイル名}/Pages
        /// </summary>
        public static string FigmaPagePrefabFolder => $"{FigmaFileRootFolder}/Pages";

        /// <summary>
        /// Assets/Figma/{Figmaファイル名}/Screens
        /// </summary>
        public static string FigmaScreenPrefabFolder => $"{FigmaFileRootFolder}/Screens";

        /// <summary>
        /// Assets/Figma/{Figmaファイル名}/Components
        /// </summary>
        public static string FigmaComponentPrefabFolder => $"{FigmaFileRootFolder}/Components";

        /// <summary>
        /// Assets/Figma/{Figmaファイル名}/ImageFills
        /// </summary>
        public static string FigmaImageFillFolder => $"{FigmaFileRootFolder}/ImageFills";

        /// <summary>
        /// Assets/Figma/{Figmaファイル名}/ServerRenderedImages
        /// </summary>
        public static string FigmaServerRenderedImagesFolder => $"{FigmaFileRootFolder}/ServerRenderedImages";

        /// <summary>
        /// Assets/Figma/{Figmaファイル名}/FontMaterialPresets
        /// </summary>
        public static string FigmaFontMaterialPresetsFolder => $"{FigmaFileRootFolder}/FontMaterialPresets";

        /// <summary>
        /// キャッシュ系CustomはFigmaファイル単位
        /// Assets/Figma/{Figmaファイル名}/Custom
        /// </summary>
        public static string FigmaPerFileCustomFolder => $"{FigmaFileRootFolder}/Custom";

        /// <summary>
        /// スクリーン名スクリプト生成先ファイル名
        /// Assets/Figma/{Figmaファイル名}/ScreenNames.cs
        /// </summary>
        public static string FigmaSceneNameScriptFilePath => $"{FigmaFileRootFolder}/ScreenNames.cs";

        /// <summary>
        /// ImageFillのIDとGUIDを結びつけるデータ
        /// </summary>
        private static FigmaAssetGuidMapData imageAssetGuidMapData;
        private static FigmaAssetGuidMapData ImageAssetGuidMapData
        {
            get
            {
                if (imageAssetGuidMapData == null)
                {
                    imageAssetGuidMapData =
                        FigmaAssetGuidMapManager.GetMap(FigmaAssetGuidMapManager.AssetType.ImageFill);
                }

                return imageAssetGuidMapData;
            }
        }

        public static string GetPathForImageFill(string imageId, string imageName)
        {
            var mapFilePath = ImageAssetGuidMapData?.GetAssetPath(imageId);
            if (string.IsNullOrEmpty(mapFilePath))
            {
                return $"{FigmaImageFillFolder}/{imageName}.png";
            }
            return mapFilePath;
        }

        public static string GetPathForServerRenderedImage(string nodeId,
            List<ServerRenderNodeData> serverRenderNodeData)
        {
            var matchingEntry = serverRenderNodeData.FirstOrDefault((node) => node.SourceNode.id == nodeId);
            switch (matchingEntry.RenderType)
            {
                case ServerRenderType.Export:
                {
                    var mapFilePath = ImageAssetGuidMapData?.GetAssetPath(matchingEntry.SourceNode.name);
                    if (!string.IsNullOrEmpty(mapFilePath))
                    {
                        return mapFilePath;
                    }
                    return $"Assets/{matchingEntry.SourceNode.name}.png";
                }
                default:
                {
                    var mapFilePath = ImageAssetGuidMapData?.GetAssetPath(nodeId);
                    if (!string.IsNullOrEmpty(mapFilePath))
                    {
                        return mapFilePath;
                    }
                    var safeNodeId = FigmaDataUtils.ReplaceUnsafeFileCharactersForNodeId(nodeId);
                    return $"{FigmaServerRenderedImagesFolder}/{safeNodeId}.png";
                }
            }
        }

        public static string GetPathForScreenPrefab(Node node, int duplicateCount)
        {
            return $"{FigmaScreenPrefabFolder}/{GetFileNameForNode(node, duplicateCount)}.prefab";
        }

        public static string GetPathForPagePrefab(Node node, int duplicateCount)
        {
            return $"{FigmaPagePrefabFolder}/{GetFileNameForNode(node, duplicateCount)}.prefab";
        }

        public static string GetPathForComponentPrefab(string nodeName, int duplicateCount)
        {
            // If name already used, create a unique name
            if (duplicateCount > 0) nodeName += $"_{duplicateCount}";
            nodeName = ReplaceUnsafeCharacters(nodeName);
            return $"{FigmaComponentPrefabFolder}/{nodeName}.prefab";
        }

        public static string GetFileNameForNode(Node node, int duplicateCount)
        {
            var safeNodeTitle = ReplaceUnsafeCharacters(node.name);
            // If name already used, create a unique name
            if (duplicateCount > 0) safeNodeTitle += $"_{duplicateCount}";
            return safeNodeTitle;
        }

        private static string ReplaceUnsafeCharacters(string inputFilename)
        {
            // We want to trim spaces from start and end of filename, or we'll throw an error
            // We no longer want to use the final "/" character as this might be used by the user
            var safeFilename = inputFilename.Trim();
            return MakeValidFileName(safeFilename);
        }

        // From https://www.csharp-console-examples.com/general/c-replace-invalid-filename-characters/
        public static string MakeValidFileName(string name)
        {
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            invalidChars += ".";
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
        }

        /// <summary>
        /// バックアップを取るのに使用するフォルダ
        /// Backup は共有Custom配下に作る
        /// </summary>
        public static string MakeBackupPath(string sourceAssetPath)
        {
            return FigmaCustomBackupFolder + '/' + sourceAssetPath;
        }

        public static void CreateRequiredDirectories()
        {
            // ルート
            if (!Directory.Exists(FigmaAssetsRootFolder))
            {
                Directory.CreateDirectory(FigmaAssetsRootFolder);
            }

            // Figmaファイル単位のルート
            if (!Directory.Exists(FigmaFileRootFolder))
            {
                Directory.CreateDirectory(FigmaFileRootFolder);
            }

            // Pages
            if (!Directory.Exists(FigmaPagePrefabFolder))
            {
                Directory.CreateDirectory(FigmaPagePrefabFolder);
            }
            foreach (var file in new DirectoryInfo(FigmaPagePrefabFolder).GetFiles())
            {
                file.Delete();
            }

            // Screens
            if (!Directory.Exists(FigmaScreenPrefabFolder))
            {
                Directory.CreateDirectory(FigmaScreenPrefabFolder);
            }
            foreach (var file in new DirectoryInfo(FigmaScreenPrefabFolder).GetFiles())
            {
                file.Delete();
            }

            // Components
            if (!Directory.Exists(FigmaComponentPrefabFolder))
            {
                Directory.CreateDirectory(FigmaComponentPrefabFolder);
            }

            // ImageFills
            if (!Directory.Exists(FigmaImageFillFolder))
            {
                Directory.CreateDirectory(FigmaImageFillFolder);
            }

            // ServerRenderedImages
            if (!Directory.Exists(FigmaServerRenderedImagesFolder))
            {
                Directory.CreateDirectory(FigmaServerRenderedImagesFolder);
            }

            // FontMaterialPresets
            if (!Directory.Exists(FigmaFontMaterialPresetsFolder))
            {
                Directory.CreateDirectory(FigmaFontMaterialPresetsFolder);
            }

            // Fonts (共通)
            if (!Directory.Exists(FigmaFontsFolder))
            {
                Directory.CreateDirectory(FigmaFontsFolder);
            }

            // Shared Custom
            if (!Directory.Exists(FigmaSharedCustomFolder))
            {
                Directory.CreateDirectory(FigmaSharedCustomFolder);
            }

            // Backup (共通)
            if (!Directory.Exists(FigmaCustomBackupFolder))
            {
                Directory.CreateDirectory(FigmaCustomBackupFolder);
            }

            // Per-file Custom
            if (!Directory.Exists(FigmaPerFileCustomFolder))
            {
                Directory.CreateDirectory(FigmaPerFileCustomFolder);
            }
        }
    }
}