using System;
using System.Collections.Generic;
using System.Globalization;
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
        /// <summary>CSV 한 행에서 값을 안전하게 읽는다. 컬럼이 없거나 null 이면 빈 문자열.</summary>
        private static string Cell(Dictionary<string, string> row, string key)
        {
            string v;
            return row != null && row.TryGetValue(key, out v) && v != null ? v : "";
        }

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
                    new[] { ReflectionUtils.FindType("ItemHelper"), typeof(UserAccountModule), ReflectionUtils.FindType("ManagerUnlock") },
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
        //    주의: 후보 중 처음 발견되는 메서드 하나만 막는다 (타입 순서 → 이름 순서).
        //    구버전은 UserAccountModule.SaveUnlockCache(void), 신버전은 ItemHelper.CheckItemsUnlock(void) 가 걸린다.
        //    반환값이 있는 메서드가 걸리면 default 가 반환되므로, 후보를 늘릴 때는 반환 타입을 확인할 것.
        [HarmonyPatch]
        public static class SaveUnlockCachePatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { ReflectionUtils.FindType("ItemHelper"), typeof(UserAccountModule), ReflectionUtils.FindType("ManagerUnlock") },
                    new[] { "CheckItemsUnlock", "SaveUnlockCache", "CheckUnlockCondition" }
                );

                MethodInfo mi = _target as MethodInfo;
                if (mi != null && mi.ReturnType != typeof(void))
                {
                    // Prefix 에서 __result 를 채우지 않으므로 반환값이 있는 메서드는 막지 않는다.
                    MelonLogger.Warning($"[Bypass] {mi.DeclaringType?.Name}.{mi.Name} 은 반환값({mi.ReturnType.Name})이 있어 SaveUnlockCachePatch 대상에서 제외합니다.");
                    _target = null;
                }
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            public static bool Prefix() => false;
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
        //    원본을 통째로 대체한다(Prefix). 원본 대비 달라지는 점: 모든 곡을 목록에 보이게(visibilityOnList=true) 하고,
        //    CSV 를 Resources 대신 파일에서 읽는다. (원본을 막는 정확한 이유는 인게임 미확인)
        //    대체 구현이라 게임이 CSV 컬럼을 바꾸면 따라가지 못하므로, 컬럼 누락/불량 행에 죽지 않게 방어적으로 읽는다.
        [HarmonyPatch]
        public static class GetTrackDataListFromCSVPatch
        {
            /// <summary>CSV 앞의 설명/헤더 행 수. 원본도 처음 두 행을 건너뛴다.</summary>
            private const int HeaderRows = 2;

            private static readonly string[] ArtistKeys =
                { "Vocal by", "Illustrated by", "Visualized by", "Motion Graphics by", "Jacket Designed by" };

            // 곡 목록 캐시가 들어있는 정적 필드. 구버전은 Util.tracks, 신버전은 RhythmGameUtils.trackDatas.
            // 컴파일 타임에 Util.tracks 를 직접 참조하면 필드가 사라진 버전에서 패치 전체가 JIT 에 실패하므로 리플렉션으로 접근한다.
            private static readonly string[] CacheFieldNames = { "tracks", "trackDatas" };

            private static MethodBase _target;
            private static Type _cacheOwner;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { ReflectionUtils.FindType("RhythmGameUtils"), typeof(Util) },
                    new[] { "GetTrackDataListFromCSV" }
                );
                _cacheOwner = _target?.DeclaringType;
                return _target != null;
            }

            public static MethodBase TargetMethod() => _target;

            private static List<TrackData> GetCache()
            {
                return ReflectionUtils.GetFieldValue<List<TrackData>>(null, _cacheOwner, CacheFieldNames);
            }

            private static void SetCache(List<TrackData> list)
            {
                ReflectionUtils.TrySetStaticCache(_cacheOwner, list, CacheFieldNames);
            }

            public static bool Prefix(ref List<TrackData> __result)
            {
                // 이미 로드했다면 조용히 캐시를 돌려준다. 이 메서드는 곡 목록을 쓰는 곳마다 불리는 핫패스라
                // 로그/파싱을 캐시 확인보다 먼저 하면 매 호출마다 비용이 든다.
                List<TrackData> cached = GetCache();
                if (cached != null)
                {
                    __result = cached;
                    return false;
                }

                MelonLogger.Msg("[Bypass] Resilient GetTrackDataListFromCSV starting.");
                List<TrackData> list = new List<TrackData>();
                try
                {
                    string path = Constants.GetSerializedDataDir("Track");
                    CSV_Dictionary dict = CSVParser.ReadByDictionaryInFile(path);
                    if (dict == null || dict.ItemDic == null)
                    {
                        MelonLogger.Error($"[Bypass] CSVParser returned null dict for path: {path}");
                        __result = list;
                        return false; // 비어 있는 결과는 캐시하지 않는다 (다음 호출에서 재시도)
                    }

                    int row = 0;
                    int skipped = 0;
                    foreach (Dictionary<string, string> value in dict.ItemDic.Values)
                    {
                        if (row++ < HeaderRows)
                        {
                            continue;
                        }

                        try
                        {
                            TrackData track = ParseTrack(value);
                            if (track != null)
                            {
                                list.Add(track);
                            }
                        }
                        catch (Exception ex)
                        {
                            // 불량 행 하나 때문에 곡 목록 전체를 잃지 않는다.
                            skipped++;
                            MelonLogger.Warning($"[Bypass] Track CSV 행 {row} 건너뜀: {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                    MelonLogger.Msg($"[Bypass] Resilient GetTrackDataListFromCSV completed! Total {list.Count} tracks loaded" +
                                    (skipped > 0 ? $" ({skipped} rows skipped)." : "."));
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[Bypass] Exception in GetTrackDataListFromCSV: {ex}");
                }

                if (list.Count > 0)
                {
                    SetCache(list);
                }
                __result = list;
                return false;
            }

            private static TrackData ParseTrack(Dictionary<string, string> value)
            {
                string id = Cell(value, "ID");
                if (string.IsNullOrEmpty(id))
                {
                    return null;
                }

                TrackData trackData = new TrackData();
                trackData.ID = id;
                trackData.Level = SplitLevels(value, "Level_STD");
                trackData.Level_LITE = SplitLevels(value, "Level_LITE");
                string displayName = Cell(value, "DisplayName");
                trackData.DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName;
                trackData.Composer = Cell(value, "Composer");
                trackData.ComposerEN = Cell(value, "ComposerEN");
                trackData.Category = ParseCategory(Cell(value, "Category"));
                trackData.visibilityOnList = true;
                trackData.BGAOffset = ParseFloat(Cell(value, "BGAOffset"));

                if (value.ContainsKey("UnlockID"))
                {
                    ReflectionUtils.SetFieldValue(trackData, typeof(TrackData), Cell(value, "UnlockID").Trim(), "unlockID");
                }

                string alphabetName = Cell(value, "DisplayName_EN");
                if (!string.IsNullOrEmpty(alphabetName))
                {
                    trackData.SetAlphabetName(alphabetName);
                }

                foreach (string key in ArtistKeys)
                {
                    if (value.ContainsKey(key))
                    {
                        trackData.artistInfoDic[key] = Cell(value, key);
                    }
                }
                return trackData;
            }

            private static string[] SplitLevels(Dictionary<string, string> value, string key)
            {
                string raw;
                return value.TryGetValue(key, out raw) ? raw?.Split('/') : null;
            }

            private static TrackCategory ParseCategory(string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return TrackCategory.Original;
                }

                int number;
                if (int.TryParse(text, out number))
                {
                    return (TrackCategory)number;
                }

                TrackCategory parsed;
                return Enum.TryParse(text, true, out parsed) ? parsed : TrackCategory.Original;
            }

            /// <summary>
            /// BGAOffset 은 항상 '.' 소수점으로 적혀 있다. 현재 로케일(쉼표 소수점 등)에 맡기면
            /// 일부 환경에서 조용히 0 이 되므로 InvariantCulture 를 먼저 시도한다.
            /// </summary>
            private static float ParseFloat(string text)
            {
                float result;
                if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result)) return result;
                if (float.TryParse(text, out result)) return result;
                return 0f;
            }
        }

        // 5. Dynamic Resilient Patch for Inventory Item Deserialization (Newer versions: InventoryItemInfo)
        [OptionalPatch("반환 타입이 Dictionary<string, List<InventoryItemInfo>> 인 버전 전용")]
        [HarmonyPatch]
        public static class DeserializeInventoryItemFromCSVPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(Util), ReflectionUtils.FindType("ItemHelper") },
                    new[] { "DeserializeInventoryItemFromCSV", "DeserializeItemInfoFromCSV" }
                );
                MethodInfo mi = _target as MethodInfo;
                return mi != null && mi.ReturnType == typeof(Dictionary<string, List<InventoryItemInfo>>);
            }

            public static MethodBase TargetMethod() => _target;

            public static Exception Finalizer(Exception __exception, ref Dictionary<string, List<InventoryItemInfo>> __result)
            {
                if (__exception == null && __result != null && __result.Count > 0)
                {
                    return null;
                }

                MelonLogger.Warning($"[Bypass] DeserializeInventoryItemFromCSV 원본 실패 -> 폴백 사용 " +
                                    $"({(__exception == null ? "빈 결과" : __exception.GetType().Name)})");
                __result = BuildFallback();
                return null;
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
                            string text = Cell(value, "Type");
                            if (text.Length == 0) continue;

                            InventoryItemInfo item = new InventoryItemInfo
                            {
                                itemID = Cell(value, "ID"),
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

                // 원본이 채워야 했던 private static 캐시도 같이 채운다 (docs/patches-and-gotchas.md 의 ESC 먹통 사례).
                ReflectionUtils.TrySetStaticCache(typeof(Util), dict, "inventoryItems");
                return dict;
            }
        }

        // 6. Dynamic Resilient Patch for Item Info Deserialization (Older versions: UnlockableItemInfo)
        [OptionalPatch("반환 타입이 Dictionary<string, List<UnlockableItemInfo>> 인 버전 전용")]
        [HarmonyPatch]
        public static class DeserializeUnlockableItemInfoFromCSVPatch
        {
            private static MethodBase _target;

            public static bool Prepare()
            {
                _target = ReflectionUtils.FindMethod(
                    new[] { typeof(Util), ReflectionUtils.FindType("ItemHelper") },
                    new[] { "DeserializeItemInfoFromCSV", "DeserializeInventoryItemFromCSV" }
                );
                MethodInfo mi = _target as MethodInfo;
                return mi != null && mi.ReturnType == typeof(Dictionary<string, List<UnlockableItemInfo>>);
            }

            public static MethodBase TargetMethod() => _target;

            public static Exception Finalizer(Exception __exception, ref Dictionary<string, List<UnlockableItemInfo>> __result)
            {
                if (__exception == null && __result != null && __result.Count > 0)
                {
                    return null;
                }

                MelonLogger.Warning($"[Bypass] DeserializeItemInfoFromCSV 원본 실패 -> 폴백 사용 " +
                                    $"({(__exception == null ? "빈 결과" : __exception.GetType().Name)})");
                __result = BuildFallback();
                return null;
            }

            private static Dictionary<string, List<UnlockableItemInfo>> BuildFallback()
            {
                var dict = new Dictionary<string, List<UnlockableItemInfo>>();
                try
                {
                    string path = Constants.GetSerializedDataDir("InventoryItem");
                    CSV_Dictionary csvDict = CSVParser.ReadByDictionaryInFile(path);
                    if (csvDict != null && csvDict.ItemDic != null)
                    {
                        foreach (var value in csvDict.ItemDic.Values)
                        {
                            string text = Cell(value, "Type");
                            if (text.Length == 0) continue;

                            UnlockableItemInfo item = new UnlockableItemInfo
                            {
                                itemID = Cell(value, "ID"),
                                itemType = text,
                                unlockString = Cell(value, "UnlockString"),
                                comparison = Cell(value, "Comparison"),
                                visibility = Cell(value, "Visibility") != "FALSE"
                            };
                            if (!dict.ContainsKey(text))
                            {
                                dict.Add(text, new List<UnlockableItemInfo>());
                            }
                            dict[text].Add(item);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[Bypass] Error in DeserializeUnlockableItemInfoFromCSV fallback: {ex}");
                }

                // 타입이 맞는 필드에만 쓴다 ("items" 라는 이름의 다른 타입 필드를 덮어쓰지 않도록).
                ReflectionUtils.TrySetStaticCache(typeof(Util), dict, "items", "inventoryItems");
                return dict;
            }
        }
    }
}
