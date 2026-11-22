# 패치 세부 사항 및 알려진 함정 (Patches & Gotchas)

본 문서는 **Sixtar Gate: STARTRAIL — Version & Network Bypass** 모드의 각 패치별 구현 세부 사항과 모딩 중 발견된 트러블슈팅 및 함정 사례를 기록합니다.

---

## 1. 패치 목록과 게임 버전별 적용 여부

총 25개. **N/A** = `[OptionalPatch]` (해당 버전에 대상이 없는 것이 정상).

"서버 사본" 열은 `H:\Sixtar Gate STARTRAIL server`의 `Assembly-CSharp.dll`(549,888 bytes)을 PowerShell 리플렉션으로 열어 **메서드 존재 여부와 시그니처를 확인한 정적 결과**(2026-11-22)입니다. 인게임 동작 검증이 아닙니다.

### `NetworkPatches.cs` (서버 통신 우회, `ManagerWebRequest`)

| 패치 | 대상 메서드 | 동작 | 서버 사본 |
| --- | --- | --- | --- |
| `FetchServerPatch` | `FetchServer(Action<bool>)` | `true`로 즉시 완료 | 있음 |
| `GetUnlockDataPatch` | `GetUnlockData()` | 빈 해시 목록으로 `InitializeUnlockData` 호출 | 있음 |
| `RefreshRequestPatch` | `RefreshRequest()` | 차단 | 있음 |
| `RequestItemUnlockPatch` | `RequestItemUnlock(string, Action<bool>)` | `true`로 즉시 완료 | 있음 |
| `GetUserArkkPatch` | `GetUserArkk(Action<int>)` | 로컬 유저 데이터의 arkk 반환 | 있음 |
| `CheckServerConnectionPatch` | `CheckServerConnection(string, Action<bool>)` | `true` | 있음 |
| `GetRemoteVersionPatch` | `GetRemoteVersion(string, Action<int>)` | 고정값 `207` 반환 (아래 참고) | 있음 |
| `PostHighScoreDataPatch` | `PostHighScoreData(PlayResult)` | 차단 | 있음 |
| `GetHighScoreListPatch` | `GetHighScoreList(TrackWithLevel, Action<ScoreList>)` | 빈 `ScoreList` | 있음 |
| `GetUnityQuestServerPatch` | `GetUnityQuestServer(Action<URLPort>)` (오버로드 2개 중 이것) | 빈 `URLPort` | 있음 |
| `CommonErrorActionPatch` | `CommonErrorAction` | 오버레이 해제 후 빈 코루틴 | N/A |
| `GetAttendanceEventTablePatch` | `GetAttendanceEventTable` | 즉시 완료 | N/A |
| `GetAttendanceRewardPatch` | `GetAttendanceReward` | `gotReward=false` 응답 | N/A |
| `GetNewsListPatch` | `GetNewsList` | 빈 목록 | N/A |
| `GetUserLicenseLevelPatch` | `GetUserLicenseLevel` | 기본 응답 | N/A |
| `GetUnityQuestProcessPatch` | `GetUnityQuestProcess` | 기본 응답 | N/A |

- **`GetRemoteVersion` = 207**: 게임이 이 값과 로컬 에셋번들 버전을 비교해 다운로드를 결정하므로, 로컬 번들을 "최신"으로 취급시키려는 고정값입니다. 값의 정확한 기준은 **인게임 미확인**이며, 번들 업데이트 팝업이 다시 뜨면 이 값부터 의심하세요.

### `DataPatches.cs` (데이터 역직렬화 & 언락 캐시)

| 패치 | 방식 | 대상 | 서버 사본 |
| --- | --- | --- | --- |
| `LoadUnlockCachePatch` | Prefix 대체 (빈 캐시) | `LoadUnlockCache` | `UserAccountModule`에 있음 |
| `SaveUnlockCachePatch` | Prefix 차단 | `CheckItemsUnlock`/`SaveUnlockCache`/`CheckUnlockCondition` 중 처음 발견되는 **void** 메서드 | `UserAccountModule.SaveUnlockCache(void)` |
| `LoadFavoritesPatch` | Finalizer 폴백 | `UserAccountModule.LoadFavorites` | 있음 |
| `GetTrackDataListFromCSVPatch` | Prefix 대체 | `Util.GetTrackDataListFromCSV` (신버전: `RhythmGameUtils`) | `Util`에 있음 |
| `DeserializeInventoryItemFromCSVPatch` | Finalizer 폴백, N/A | 반환 타입이 `Dictionary<string, List<InventoryItemInfo>>` 인 메서드 | N/A (대상 없음) |
| `DeserializeUnlockableItemInfoFromCSVPatch` | Finalizer 폴백, N/A | 반환 타입이 `Dictionary<string, List<UnlockableItemInfo>>` 인 메서드 | `Util.DeserializeItemInfoFromCSV`가 해당 |

- 두 `Deserialize*` 패치는 같은 후보 이름을 찾지만 **반환 타입으로 서로 배타적**이라, 어느 버전에서든 한쪽은 `N/A`입니다.
- 서버 사본 기준 예상 리포트는 **적용 18 / 해당 없음 7 / 건너뜀 0 / 실패 0**입니다 (정적 대조에 따른 예상이며 인게임 로그로 확인되지 않았습니다).
- `SaveUnlockCachePatch`는 후보 중 처음 발견되는 메서드 하나만 막습니다. 구버전은 `UserAccountModule.SaveUnlockCache`, 신버전은 `ItemHelper.CheckItemsUnlock`이 걸립니다. 반환값이 있는 메서드가 걸리면 제외됩니다.
- `GetTrackDataListFromCSVPatch`는 대체 구현입니다. 곡을 전부 목록에 노출(`visibilityOnList=true`)하고 CSV를 파일에서 읽습니다. 컬럼 누락/불량 행은 건너뛰며, **불완전하거나 빈 결과는 정적 캐시에 저장하지 않습니다**(예전에는 예외 시 일부 곡만 담긴 목록이 영구 캐시될 수 있었음).

### `UIPatches.cs` (UI & 크리티컬 팝업 우회)

| 패치 | 동작 | 서버 사본 |
| --- | --- | --- |
| `UpdateCheckerPatch` | `UpdateChecker.Check`를 막고 오브젝트를 비활성화, `runtimeCheck.isUpdateChecked=true` 후 `onLatestVersion` 호출 | `Check(Action, Action)` (다른 사본은 `onDisconnected` 추가 3개 인자) |
| `ShowCriticalErrorPopupPatch` | 크리티컬 에러 팝업 차단 | `ManagerPopup.ShowCriticalErrorPopup(PopupInfo)` |
| `CheckTutorialBypass` | `CheckTutorial` → `false` | `UserAccountModule.CheckTutorial` |

---

## 2. 모딩 시 알려진 함정 (Known Gotchas)

### ⚠️ [중요] Private Static 캐시 미동기화 문제 (ESC 키 먹통 현상)

#### 현상
- 인벤토리 팝업을 닫을 때(`InventoryPopup.Close()`), `PlayOptionViewer.ShowPlayerOption()` 메서드에서 `NullReferenceException`이 발생하며 UI 진행이 멈춤.
- Unity `InputSystem`이 액션 콜백 내부에서 발생한 예외를 내부적으로 수용(Swallow)하여 **MelonLoader 콘솔에 아무런 로그도 남지 않음**.
- 사용자는 단순 **"ESC 키 입력 먹통"** 현상으로 인식하게 됨.

#### 원인 분석
- `DeserializeInventoryItemFromCSV()` 메서드를 Harmony `Prefix`로 통째로 대체할 때, 반환값(`__result`)만 채우고 클래스 내부의 **private static 캐시 필드 (`Util.inventoryItems`)**를 업데이트하지 않고 비워둠.
- 후속 로직인 `Util.FindInventoryItemInfoByID()`는 `DeserializeInventoryItemFromCSV()`의 반환값을 직접 쓰지 않고 `Util.inventoryItems` 정적 필드를 참조함.
- 정적 필드가 `null` 또는 빈 목록으로 남아있어 `NullReferenceException`이 발생함.

#### 해결책
- 원본 메서드를 `Prefix`로 완전 대체할 경우 반환값 뿐만 아니라 원본이 갱신하던 private static 필드나 캐시 객체까지 Reflection을 이용해 동일하게 채워주어야함.
- 또는 `Prefix` 대신 `Finalizer` 방식(폴백)을 채택하여 원본이 부수 효과(Static Cache 채우기)를 수행하도록 유도.
- 현재 구현: `Finalizer` 폴백 + `ReflectionUtils.TrySetStaticCache`로 캐시를 채움 (필드 타입이 값과 호환될 때만 씀).

### ⚠️ 부분 결과를 정적 캐시에 저장하지 말 것

곡 목록 같은 "한 번 만들고 캐시하는" 데이터를 대체 구현할 때, 예외로 중간에 끊긴 **불완전한 목록**을 캐시하면 게임이 끝날 때까지 그 목록을 씁니다. 불완전/빈 결과는 반환만 하고 캐시하지 말아야 다음 호출에서 재시도됩니다.

### ⚠️ 정적 캐시 필드 이름은 버전마다 다르다

곡 목록 캐시는 구버전 `Util.tracks`, 신버전 `RhythmGameUtils.trackDatas`입니다. 컴파일 타임에 `Util.tracks`를 직접 참조하면 필드가 없는 버전에서 패치 전체가 JIT 단계에서 실패하므로 리플렉션(`CacheFieldNames`)으로 접근합니다.

### ⚠️ Harmony `Prefix` 파라미터 이름 바인딩

`Prefix`의 파라미터 이름이 원본 메서드와 다르면 해당 패치만 `FAIL`로 보고됩니다. 게임이 파라미터 이름을 바꿨다면 이 경우입니다 (리포트의 예외 메시지에 이름이 나옵니다).

### ⚠️ 로컬 디컴파일 폴더(`Assembly-CSharp/`)를 시그니처 근거로 쓰지 말 것

저장소 밖 로컬 디컴파일 폴더에는 신구 버전 소스가 섞여 있는 것으로 보입니다 (예: `ManagerUnlock.cs`는 `UserAccountModule.SaveUnlockCache`를 부르는데 `UserAccountModule.cs`에는 그 메서드가 없음). 시그니처는 **실제 게임 DLL을 리플렉션으로 열어** 확인하세요:

```powershell
$dir = 'H:\Sixtar Gate STARTRAIL server\Sixtar Gate STARTRAIL_Data\Managed'
[AppDomain]::CurrentDomain.add_AssemblyResolve({ param($s,$a) $p = Join-Path $dir ($a.Name.Split(',')[0] + '.dll'); if (Test-Path $p) { [Reflection.Assembly]::LoadFrom($p) } })
$asm = [Reflection.Assembly]::LoadFrom("$dir\Assembly-CSharp.dll")
$types = try { $asm.GetTypes() } catch [Reflection.ReflectionTypeLoadException] { $_.Exception.Types | Where-Object { $_ } }
$types | ForEach-Object { $_.GetMethods('Public,NonPublic,Static,Instance,DeclaredOnly') } |
  Where-Object Name -eq 'FetchServer' |
  ForEach-Object { "$($_.DeclaringType.FullName).$($_.Name)($(($_.GetParameters() | % { $_.ParameterType.Name + ' ' + $_.Name }) -join ', ')) -> $($_.ReturnType.Name)" }
```

> 일부 타입이 `ReflectionTypeLoadException`으로 로드되지 않으면 그 타입의 메서드는 목록에서 빠집니다. "없음"은 "로드 실패"일 수 있으니 확정하기 전에 한 번 더 확인하세요.

### ⚠️ 같은 폴더에 두 개의 모드 DLL

게임 폴더 `Mods\`에 이 모드의 옛 이름 DLL과 새 DLL이 함께 있으면 `MelonInfo`가 중복되어 패치가 두 번 적용될 수 있습니다. 빌드 산출물 이름은 `SixtarBypassMod.dll`입니다.

---

## 3. Unity InputSystem 예외 삼킴(Swallowing) 대처법

1. **Inspector / UnityExplorer 활용**: 콘솔 로그에 출력이 없더라도 UnityExplorer의 Object Explorer 또는 Hook을 활용해 현장에서 예외 및 정적 필드 값을 추적.
2. **Key Event Callback Wrapping**: ESC 키나 특정 입력 이벤트 콜백에 `try-catch` 및 로깅을 직접 주입하여 예외 발생 지점 탐지.
