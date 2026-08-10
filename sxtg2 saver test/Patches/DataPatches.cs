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
                    // Type.GetType 은 어셈블리 한정 이름이 없으면 다른 어셈블리 타입을 못 찾아서
                    // Assembly-CSharp 타입에 대해 항상 null 이었다. FindType 은 로드된 어셈블리를 뒤진다.
                    new[] { typeof(ItemHelper), typeof(UserAccountModule), ReflectionUtils.FindType("ManagerUnlock") },
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
                    // Type.GetType 은 어셈블리 한정 이름이 없으면 다른 어셈블리 타입을 못 찾아서
                    // Assembly-CSharp 타입에 대해 항상 null 이었다. FindType 은 로드된 어셈블리를 뒤진다.
                    new[] { typeof(ItemHelper), typeof(UserAccountModule), ReflectionUtils.FindType("ManagerUnlock") },
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

        // 3. UserAccountModule: LoadFavorites 안전망
        //    원본을 먼저 돌리고, 터지거나(CryptographicException 등) null 을 주면 빈 객체로 대체한다.
        //    원본이 멀쩡하면 사용자의 즐겨찾기가 그대로 살아있다.
        [HarmonyPatch(typeof(UserAccountModule), nameof(UserAccountModule.LoadFavorites))]
        public static class LoadFavoritesPatch
        {
            public static Exception Finalizer(Exception __exception, ref SXGT_Favorites __result)
            {
                if (__exception == null && __result != null)
                {
                    return null;
                }

                MelonLogger.Msg($"[Bypass] LoadFavorites 실패 -> 빈 즐겨찾기로 대체 " +
                                $"({(__exception == null ? "null 반환" : __exception.GetType().Name)})");
                __result = new SXGT_Favorites();
                return null; // 예외를 삼켜서 호출자로 전파하지 않는다
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

            // 원본 구현과 로직이 동일한 순수 방어용 패치이므로, 통째로 대체하지 않고
            // 원본을 먼저 실행한다. 원본이 성공하면 Util.inventoryItems 정적 캐시도 원본이 채운다.
            // 원본이 터지거나 빈 결과를 줄 때만 아래 폴백으로 대체한다.
            public static Exception Finalizer(Exception __exception, ref Dictionary<string, List<InventoryItemInfo>> __result)
            {
                if (__exception == null && __result != null && __result.Count > 0)
                {
                    return null;
                }

                MelonLogger.Warning($"[Bypass] DeserializeInventoryItemFromCSV 원본 실패 -> 폴백 사용 " +
                                    $"({(__exception == null ? "빈 결과" : __exception.GetType().Name)})");
                __result = BuildFallback();
                return null; // 예외를 삼켜서 호출자로 전파하지 않는다
            }

            private static Dictionary<string, List<InventoryItemInfo>> BuildFallback()
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
                    MelonLogger.Error($"[Bypass] Error in DeserializeInventoryItemFromCSV fallback: {ex}");
                }

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

                return dict;
            }
        }
    }
}
