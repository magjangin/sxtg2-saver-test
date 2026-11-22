# sxtg2 saver test — 인수인계 문서 (AGENTS.md)

> 최종 갱신 2026-11-22 (v1.9.3 작업). **빌드 성공과 게임 DLL 시그니처 정적 대조까지만 했고 인게임 검증은 하지 않았다.**
> **"실측"** = 디스크에서 직접 읽은 값, **"미확인"** = 인게임으로 확인해야 하는 항목.

## 0. 프로젝트 개요

- **무엇인가:** Sixtar Gate STARTRAIL 버전·네트워크 우회 모드. 오프라인/서버 미구동 환경에서 버전 체크와 서버 통신을 우회하고 즐겨찾기·언락 등 데이터를 로컬 처리. 현재 버전은 `SixtarBypassMod.cs`의 `ModInfo.Version`(한 곳에서만 관리).
- **종류:** MelonLoader 게임 모드 (Harmony 패치 25개, `NetworkPatches`/`DataPatches`/`UIPatches`)
- **저장소:** GitHub `magjangin/sxtg2-saver-test` (origin), 로컬 미러 `H:\local-repos\sxtg2 saver test.git` (remote 이름 `local`)
- **먼저 읽을 문서:** [README.md](README.md), [docs/architecture.md](docs/architecture.md), [docs/patches-and-gotchas.md](docs/patches-and-gotchas.md) (패치별 버전 적용표 포함), [CHANGELOG.md](CHANGELOG.md)

## 1. 게임 버전 및 고정 여부

두 개의 게임 폴더가 있고 **이 모드는 `server` 사본을 대상으로 빌드·배포한다** (csproj `GameDir` 기본값, `build.bat` 배포 경로).

| 폴더 | 용도 | `Assembly-CSharp.dll` (실측) | `Mods\` 내용 |
| --- | --- | --- | --- |
| `H:\Sixtar Gate STARTRAIL server` | **이 모드의 빌드 참조 + 배포 대상** | 549,888 bytes, 수정 2026-08-15 | `SixtarBypassMod.dll` |
| `H:\Sixtar Gate STARTRAIL custom mode` | 별개 커스텀 모드(`sxtg2.dll`) 사본. 이 저장소 산출물이 아님 | 479,232 bytes, 수정 2025-11-15 | `sxtg2.dll` |

- 둘 다 스팀 라이브러리 밖의 수동 사본이라 스팀 자동 업데이트(Build ID/.acf)의 영향을 받지 않는다. 원본 게임 버전/날짜는 **미확인**.
- 호환 목표: 2022-02-02 ~ 2022-08-04 배포 버전.
- **주의:** 두 폴더의 DLL이 서로 다른 버전이다(예: `UpdateChecker.Check`가 한쪽은 2개, 다른 쪽은 3개 인자). 다른 폴더에서 빌드하려면 `build.bat "<게임 폴더>"` 또는 `/p:GameDir=`.

## 2. 런타임 백엔드 및 디컴파일 경로

- Unity **Mono** / Unity `2020.3.9f1` / x64 / MelonLoader `0.7.3` Open-Beta (실측: custom mode `Latest.log`). 게임 폴더의 `MelonLoader\net472\`의 `MelonLoader.dll`, `0Harmony.dll`을 참조한다.
- 참조 어셈블리: `<GameDir>\Sixtar Gate STARTRAIL_Data\Managed\` (`Assembly-CSharp`, `UnityEngine*`).
- 모드 DLL: `SixtarBypassMod.dll`, 타깃 .NET Framework 4.7.2, **구형 csproj** (SDK-style 아님). `dotnet build`가 그대로 동작함(실측, .NET SDK 10.0.401).
- **로컬 디컴파일 `Assembly-CSharp/`(gitignore, Lyrebird Studio 저작물)은 시그니처 근거로 믿지 말 것.** 신구 버전이 섞여 있다(`ManagerUnlock.cs`가 부르는 `SaveUnlockCache`가 `UserAccountModule.cs`에 없음). 시그니처는 실제 DLL을 PowerShell 리플렉션으로 열어 확인한다 → [docs/patches-and-gotchas.md](docs/patches-and-gotchas.md)의 스니펫.

## 3. 설계 의도와 우회 사유 (Rationale)

- **PatchAll 금지 + 격리 적용 (`PatchRegistry`):** Harmony `PatchAll`은 패치 하나가 예외를 내면 뒤 패치가 전부 적용되지 않는다. 게임 업데이트로 타입/파라미터가 바뀌어도 나머지 패치는 살려야 해서 `[assembly: HarmonyDontPatchAll]` + 개별 `try/catch`.
- **`[OptionalPatch]` → `N/A`:** 버전에 따라 없는 메서드를 막는 패치(신버전 전용 6개, 구/신 중 한쪽만 쓰는 `Deserialize*` 2개 중 1개)를 `FAIL/SKIP`로 보고하면 진짜 문제가 묻힌다. 서버 사본 기준 예상 리포트: 적용 18 / 해당 없음 7 (정적 예상, 인게임 **미확인**).
- **리플렉션 탐색:** `Type.GetType`은 다른 어셈블리 타입을 어셈블리 한정 이름 없이 못 찾아 `Assembly-CSharp` 타입에 항상 null → 로드된 어셈블리 전체를 뒤지는 `FindType`(캐시, `Assembly-CSharp` 우선). 버전별로 사라지는 필드(`Util.tracks`/`RhythmGameUtils.trackDatas`, `runtimeCheck`)는 컴파일 타임 참조 대신 리플렉션 → 없는 버전에서 패치가 JIT 실패하지 않게.
- **Prefix 대체 vs Finalizer 폴백:** 서버 요청 차단은 원본이 돌면 안 되므로 Prefix 대체. 방어용(`LoadFavorites`, `Deserialize*`)은 원본이 정상이면 원본 결과(사용자 데이터)를 살리려고 Finalizer 폴백. 곡 목록(`GetTrackDataListFromCSV`)은 Prefix 대체(모든 곡을 목록에 노출, 파일에서 CSV 읽기). 원본을 막는 정확한 이유는 **미확인**.
- **`GetRemoteVersion`=207:** 원격 에셋번들 버전 고정값. 로컬 번들을 최신으로 취급시키려는 값이며 기준은 **미확인**.
- **`MelonGame(null, null)` 유지:** 개발사/게임명을 지정하면 불일치 시 모드가 아예 로드되지 않을 위험이 있어 인게임 검증 없이 바꾸지 않았다. 대신 `ManagerWebRequest` 타입이 없으면(다른 게임) 패치를 시도하지 않는다.
- **버전 단일 출처:** `ModInfo.Version`을 `MelonInfo`/`AssemblyInfo`/로그가 모두 참조.

## 4. 핵심 훅 시그니처와 주입 파이프라인

- **Melon 특성:** `MelonInfo(typeof(SixtarBypassMod), ModInfo.Name, ModInfo.Version, "Hwa-young-wang")`, `MelonGame(null, null)`, `HarmonyDontPatchAll`
- **주입 파이프라인:** `OnInitializeMelon` → `LogGameAssemblyFingerprint`(MVID 기록/비교) → `IsTargetGameLoaded` → `ApplyContainer(Network/Data/UI)`(컨테이너의 `[HarmonyPatch]` 중첩 클래스를 자동 순회) → `PrintReport`(콘솔 + `UserData\SixtarBypass.report.txt`).
- **패치 구조:** 각 패치 클래스는 `Prepare()`(대상 탐색, 없으면 false) / `TargetMethod()` / `Prefix|Finalizer`. **Prefix 파라미터 이름은 원본과 같아야 바인딩된다.**
- **서버 사본 실측 시그니처(발췌):** `ManagerWebRequest.FetchServer(Action<bool> onComplete)`, `RequestItemUnlock(string _itemID, Action<bool> onComplete)`, `GetHighScoreList(TrackWithLevel twl, Action<ScoreList> callback)`, `GetUnityQuestServer()`/`(Action<URLPort> onComplete)` 오버로드 2개, `UserAccountModule.LoadUnlockCache()->SXGT_UnlockCache`, `SaveUnlockCache(SXGT_UnlockCache uc)`, `LoadFavorites()->SXGT_Favorites`, `CheckTutorial(...)->bool`, `Util.GetTrackDataListFromCSV()`, `Util.DeserializeItemInfoFromCSV()->Dictionary<string,List<UnlockableItemInfo>>`, `UpdateChecker.Check(Action onLatestVersion, Action onRequireUpdate)`. 전체 표는 patches-and-gotchas.md.

## 5. 시도했다가 실패한 것들 (Dead Ends)

- **`HarmonyInstance.PatchAll()`:** 패치 하나 예외로 전체 중단 → 격리 적용으로 대체.
- **`DeserializeInventoryItemFromCSV`를 Prefix로 통째로 대체:** 반환값만 채우고 private static `Util.inventoryItems`를 안 채워서 `NullReferenceException` → Unity InputSystem이 예외를 삼켜 **ESC 키 먹통**으로만 보임(콘솔 로그 없음). Finalizer 폴백 + 정적 캐시 채우기로 해결.
- **`Type.GetType("ItemHelper")`:** 항상 null → `FindType`.
- **곡 목록을 예외 시 부분 결과로 캐시:** 일부 곡만 담긴 목록이 영구 캐시됨 → 불완전/빈 결과는 캐시하지 않도록 수정(v1.9.3).
- **`BGAOffset`을 현재 로케일로 파싱:** 쉼표 소수점 환경에서 0 → `InvariantCulture` 우선(v1.9.3).
- **`Util.tracks`/`runtimeCheck`를 컴파일 타임 직접 참조:** 필드가 없는 버전에서 패치 전체가 JIT 실패 → 리플렉션(v1.9.3).
- **로컬 디컴파일 폴더를 시그니처 근거로 사용:** 신구 버전이 섞여 틀린 결론을 낼 수 있음 → 실제 DLL 리플렉션.
- **`MelonGame`에 개발사/게임명 지정:** 시도하지 않음(로드 실패 위험, 인게임 검증 필요).

## 6. 빌드 명령 및 배포 경로

```powershell
# 빌드 + 배포 (기본 게임 폴더: H:\Sixtar Gate STARTRAIL server)
.\build.bat
# 게임 폴더 지정 / 자동화(pause 생략)
.\build.bat "D:\Games\Sixtar Gate STARTRAIL"
$env:NO_PAUSE=1; .\build.bat
# 배포 없이 빌드만
dotnet build "sxtg2 saver test\sxtg2 saver test.csproj" -c Release
```

- 산출물 `sxtg2 saver test\bin\Release\SixtarBypassMod.dll` → `<GameDir>\Mods\SixtarBypassMod.dll`.
- 참조 경로는 csproj `GameDir` 하나. 새 `.cs`는 와일드카드로 자동 포함.
- 릴리즈: GitHub Releases에 `SixtarBypassMod.dll` 첨부, 태그 `vX.Y.Z` = `ModInfo.Version`. 변경 이력은 `CHANGELOG.md`.
- Git Bash에서 `dotnet`에 `/nologo`를 주면 경로로 변환되므로 `-nologo`를 쓴다.

## 7. 다음 작업자에게 (인게임 검증 체크리스트 — 모두 **미확인**)

1. 게임 실행 후 `MelonLoader\Latest.log`의 `Patch Report`: 서버 사본 기준 `적용 18 / 해당 없음 7 / 건너뜀 0 / 실패 0` 이어야 한다. `UserData\SixtarBypass.report.txt`, `SixtarBypass.fingerprint.txt` 생성 확인.
2. 타이틀 진입, 곡 선택 화면(곡 목록 전부 노출, 프리뷰/BGM), 인벤토리 열고 닫기(ESC 먹통 재발 없는지), 즐겨찾기 저장/불러오기.
3. 확인되면 이 문서의 **미확인** 항목(`GetRemoteVersion`=207 기준, 곡 목록 Prefix 대체 사유, 원본 게임 날짜)을 실제 내용으로 바꾸고 `CHANGELOG.md`의 "인게임 검증 안 됨" 문구를 제거, 릴리즈의 pre-release 표시를 해제한다.
4. 게임을 갱신한 경우 로그의 `[Env] 게임 어셈블리가 이전 실행과 다릅니다` 경고와 SKIP/FAIL 항목부터 본다.
