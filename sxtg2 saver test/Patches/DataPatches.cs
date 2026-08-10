using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Inventory;
using MelonLoader;
using RhythmGame;
using sxtg2_saver_test.Helpers;

namespace sxtg2_saver_test.Patches
{
    public static class DataPatches
    {
        // 1. Dynamic Resilient Patch for LoadUnlockCache
        [HarmonyPatch]
        public static class LoadUnlockCachePatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ItemHelper), typeof(UserAccountModule), Type.GetType("ManagerUnlock") },
                    new[] { "LoadUnlockCache" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(ref SXGT_UnlockCache __result)
            {
                __result = new SXGT_UnlockCache
                {
                    cachedHashList = new List<string>()
                };
                return false;
            }
        }

        // 2. Dynamic Resilient Patch for SaveUnlockCache / CheckItemsUnlock
        [HarmonyPatch]
        public static class SaveUnlockCachePatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(ItemHelper), typeof(UserAccountModule), Type.GetType("ManagerUnlock") },
                    new[] { "CheckItemsUnlock", "SaveUnlockCache", "CheckUnlockCondition" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix()
            {
                return false;
            }
        }

        // 3. UserAccountModule: LoadFavorites CryptographicException 방지 안전 패치
        [HarmonyPatch(typeof(UserAccountModule), nameof(UserAccountModule.LoadFavorites))]
        public static class LoadFavoritesPatch
        {
            public static bool Prefix(ref SXGT_Favorites __result)
            {
                MelonLogger.Msg("[Bypass] LoadFavorites intercepted! Returning safe SXGT_Favorites object.");
                __result = new SXGT_Favorites();
                return false;
            }
        }

        // 4. Dynamic Resilient Patch for GetTrackDataListFromCSV
        [HarmonyPatch]
        public static class GetTrackDataListFromCSVPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(RhythmGameUtils), typeof(Util) },
                    new[] { "GetTrackDataListFromCSV" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(ref List<TrackData> __result)
            {
                MelonLogger.Msg("[Bypass] Resilient GetTrackDataListFromCSV starting.");
                if (RhythmGameUtils.trackDatas != null)
                {
                    __result = RhythmGameUtils.trackDatas;
                    return false;
                }

                List<TrackData> list = new List<TrackData>();
                try
                {
                    string path = Constants.GetSerializedDataDir("Track");
                    CSV_Dictionary dict = CSVParser.ReadByDictionaryInFile(path);
                    if (dict == null || dict.ItemDic == null)
                    {
                        MelonLogger.Error($"[Bypass] CSVParser returned null dict for path: {path}");
                        __result = list;
                        RhythmGameUtils.trackDatas = list;
                        return false;
                    }

                    int num = 0;
                    List<string> artistKeys = new List<string> { "Vocal by", "Illustrated by", "Visualized by", "Motion Graphics by", "Jacket Designed by" };
                    foreach (Dictionary<string, string> value in dict.ItemDic.Values)
                    {
                        if (num <= 1)
                        {
                            num++;
                            continue;
                        }
                        float.TryParse(value["BGAOffset"], out var result);
                        if (string.IsNullOrEmpty(value["ID"]))
                        {
                            continue;
                        }
                        TrackData trackData = new TrackData();
                        trackData.ID = value["ID"];
                        trackData.Level = value["Level_STD"]?.Split('/');
                        trackData.Level_LITE = value["Level_LITE"]?.Split('/');
                        trackData.DisplayName = string.IsNullOrEmpty(value["DisplayName"]) ? value["ID"] : value["DisplayName"];
                        trackData.Composer = value["Composer"];
                        trackData.ComposerEN = value["ComposerEN"];

                        TrackCategory cat = TrackCategory.Original;
                        if (value.ContainsKey("Category") && !string.IsNullOrEmpty(value["Category"]))
                        {
                            string catStr = value["Category"];
                            if (int.TryParse(catStr, out int catNum))
                            {
                                cat = (TrackCategory)catNum;
                            }
                            else if (Enum.TryParse<TrackCategory>(catStr, true, out var parsedCat))
                            {
                                cat = parsedCat;
                            }
                        }
                        trackData.Category = cat;
                        trackData.visibilityOnList = true;
                        trackData.BGAOffset = result;
                        if (value.ContainsKey("UnlockID"))
                        {
                            trackData.unlockID = value["UnlockID"]?.Trim();
                        }
                        if (value.ContainsKey("DisplayName_EN") && !string.IsNullOrEmpty(value["DisplayName_EN"]))
                        {
                            trackData.SetAlphabetName(value["DisplayName_EN"]);
                        }
                        foreach (string key in artistKeys)
                        {
                            if (value.ContainsKey(key))
                            {
                                trackData.artistInfoDic[key] = value[key];
                            }
                        }
                        list.Add(trackData);
                    }
                    MelonLogger.Msg($"[Bypass] Resilient GetTrackDataListFromCSV completed! Total {list.Count} tracks loaded.");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[Bypass] Exception in GetTrackDataListFromCSV: {ex}");
                }
                __result = list;
                RhythmGameUtils.trackDatas = list;
                return false;
            }
        }

        // 5. Dynamic Resilient Patch for Inventory Item Deserialization
        [HarmonyPatch]
        public static class DeserializeInventoryItemFromCSVPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(Util), typeof(ItemHelper) },
                    new[] { "DeserializeInventoryItemFromCSV", "DeserializeItemInfoFromCSV" }
                );
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix(ref Dictionary<string, List<InventoryItemInfo>> __result)
            {
                var dict = new Dictionary<string, List<InventoryItemInfo>>();
                try
                {
                    string path = Constants.GetSerializedDataDir("InventoryItem");
                    CSV_Dictionary csvDict = CSVParser.ReadByDictionaryInFile(path);
                    if (csvDict != null && csvDict.ItemDic != null)
                    {
                        foreach (var value in csvDict.ItemDic.Values)
                        {
                            string text = value["Type"];
                            InventoryItemInfo item = new InventoryItemInfo
                            {
                                itemID = value["ID"],
                                itemType = text
                            };
                            if (!dict.ContainsKey(text))
                            {
                                dict.Add(text, new List<InventoryItemInfo>());
                            }
                            dict[text].Add(item);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[Bypass] Error in DeserializeInventoryItemFromCSV: {ex}");
                }
                __result = dict;

                // 원본은 결과를 private static 캐시(Util.inventoryItems)에도 넣는다.
                // Util.FindInventoryItemInfoByID / GetInventoryItemIndexByID 는 반환값을 버리고
                // 이 캐시가 채워졌다고 가정하므로, 여기서 같이 채워주지 않으면 NRE가 난다.
                // (인벤토리 팝업 ESC가 안 먹던 원인)
                var cache = AccessTools.Field(typeof(Util), "inventoryItems");
                if (cache != null)
                {
                    cache.SetValue(null, dict);
                }
                else
                {
                    MelonLogger.Warning("[Bypass] Util.inventoryItems 필드를 찾지 못함. 인벤토리 조회가 NRE를 낼 수 있음.");
                }

                return false;
            }
        }
    }
}
