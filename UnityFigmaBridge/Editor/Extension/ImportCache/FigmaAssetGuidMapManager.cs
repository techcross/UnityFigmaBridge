using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityFigmaBridge.Editor.Utils;

namespace UnityFigmaBridge.Editor.Extension.ImportCache
{
    public static class FigmaAssetGuidMapManager
    {
        private static string ComponentCacheDataDir = "/Custom/Cache/";
        public enum AssetType
        {
            Component,
            ImageFill,
        }

        public static Dictionary<AssetType, FigmaAssetGuidMapData> _mapContenar = new Dictionary<AssetType, FigmaAssetGuidMapData>();

        public static FigmaAssetGuidMapData CreateMap(AssetType assetType)
        {
            
            if (_mapContenar.TryGetValue(assetType, out var map))
            {
                return map;
            }

            var path = MakePath(assetType);
            map = AssetDatabase.LoadAssetAtPath<FigmaAssetGuidMapData>(path);
            if (map)
            {
                map.Initialize(); // 作成済みのファイルをロードした時だけ初期化
                _mapContenar.TryAdd(assetType, map);
                return map;
            }
            
            map = ScriptableObject.CreateInstance<FigmaAssetGuidMapData>();
            _mapContenar.Add(assetType, map);
            
            var cashDir = FigmaPaths.FigmaFileRootFolder + ComponentCacheDataDir;
            // ディレクトリなければ生成
            if (!Directory.Exists(cashDir))
            {
                Directory.CreateDirectory(cashDir);
            }
            
            AssetDatabase.CreateAsset(map, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);

            return map;
        }

        public static FigmaAssetGuidMapData GetMap(AssetType assetType)
        {
            return _mapContenar.GetValueOrDefault(assetType);
        }

        public static void SaveAllMap()
        {
            foreach (var map in _mapContenar)
            {
                var value = map.Value;
                value.FinalizeMap();
                // イメージは強制更新
                if (map.Key == AssetType.ImageFill)
                {
                    value.SetDirty();
                }
            }
            AssetDatabase.SaveAssets();
            _mapContenar.Clear();
        }


        private static string MakePath(AssetType assetType)
        {
            return ComponentCacheDataDir + assetType.ToString() + ".asset";
        }
    }
}