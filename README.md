# Sixtar Gate: STARTRAIL — Version & Network Bypass

[MelonLoader](https://github.com/LavaGang/MelonLoader) 모드. 서버가 닫혔거나 오프라인인 환경에서
Sixtar Gate: STARTRAIL 이 정상적으로 구동되도록 버전 체크와 서버 통신 경로를 우회한다.

## 동작 환경

| 항목 | 값 |
|---|---|
| 게임 | Sixtar Gate: STARTRAIL (Lyrebird Studio) |
| Unity | 2020.3.9f1 (Mono / MonoBleedingEdge, x64) |
| MelonLoader | 0.7.3 (net472) |
| 대상 프레임워크 | .NET Framework 4.7.2 |

## 구성

```
sxtg2 saver test/
├── SixtarBypassMod.cs        모드 진입점 (MelonMod)
├── Helpers/
│   ├── ReflectionUtils.cs    메서드/타입 이름이 바뀌어도 견디는 리플렉션 헬퍼
│   └── PatchRegistry.cs      패치 격리 적용 + 적용 결과 보고
└── Patches/
    ├── NetworkPatches.cs     ManagerWebRequest 계열 서버 요청 차단 및 더미 응답
    ├── DataPatches.cs        CSV 역직렬화 / 언락 캐시 등 로컬 데이터 경로 대체
    └── UIPatches.cs          업데이트 체커, 크리티컬 에러 팝업, 뮤직셀렉트 초기화
```

## 게임 업데이트 내성

게임이 갱신돼서 내부 구조가 바뀌어도 모드 전체가 무너지지 않도록 네 겹으로 방어한다.

**1. 대상 메서드를 런타임에 탐색**

각 패치는 `ReflectionUtils.FindMethod` 로 후보 타입/이름 목록을 훑어 대상을 찾고
`Prepare()` / `TargetMethod()` 로 Harmony 에 넘긴다. 못 찾으면 그 패치만 비활성화된다.
오버로드가 늘어나 `AmbiguousMatchException` 이 나면 파라미터가 가장 적은 시그니처를 고른다.

**2. 패치 격리 적용**

Harmony 의 `PatchAll` 은 패치 클래스를 순회하다 하나라도 예외가 나면 그 자리에서 멈춘다.
타입이 사라졌거나(`TypeLoadException`) 파라미터 이름이 바뀌면 뒤에 있던 멀쩡한 패치들까지
전부 유실된다. 그래서 `[assembly: HarmonyDontPatchAll]` 로 자동 패치를 끄고
`PatchRegistry` 가 `CreateClassProcessor` 로 하나씩 `try`/`catch` 하며 적용한다.

**3. 적용 결과 보고**

부팅 시 어떤 패치가 붙었고 어떤 게 실패했는지 표로 남긴다. 실패는 `ERROR`,
건너뜀은 `WARNING` 레벨이라 로그만 봐도 바로 눈에 띈다.
게임 어셈블리의 MVID 도 같이 찍어서 게임이 갱신됐는지 판별할 수 있다.

```
---------------- Patch Report ----------------
  [OK  ] NetworkPatches.FetchServerPatch  ManagerWebRequest.FetchServer
  [SKIP] DataPatches.SaveUnlockCachePatch  대상 메서드를 찾지 못함
  적용 21 / 건너뜀 1 / 실패 0
----------------------------------------------
```

**4. 대체가 아니라 폴백**

순수 방어 목적의 패치(`DeserializeInventoryItemFromCSV`, `LoadFavorites`)는 원본을
`Prefix` 로 갈아치우지 않고 `Finalizer` 로 감싼다. 원본을 먼저 실행하고, 예외가 나거나
빈 결과일 때만 모드 구현으로 대체한다. 게임이 바뀌어도 원본이 살아있는 한 원본이 쓰이고,
원본의 부수 효과(정적 캐시 채우기 등)도 자연히 유지된다.

의도적 우회가 필요한 패치(서버 요청 차단, 트랙 가시성 강제 해제, 언락 캐시)는
폴백이 아니라 그대로 완전 대체다. 원본이 성공하면 곤란한 쪽이기 때문이다.

## 빌드

`sxtg2 saver test.csproj` 의 `HintPath` 가 로컬 설치 경로로 하드코딩돼 있다.
환경에 맞게 고쳐야 한다.

```
H:\steam\steamapps\common\Sixtar Gate STARTRAIL\
```

필요한 참조:

- `MelonLoader\net472\MelonLoader.dll`
- `MelonLoader\net472\0Harmony.dll`
- `Sixtar Gate STARTRAIL_Data\Managed\Assembly-CSharp.dll`
- `Sixtar Gate STARTRAIL_Data\Managed\UnityEngine*.dll`

빌드 후 `bin\Debug\SixtarBypassMod.dll` 을 게임 폴더의 `Mods\` 에 복사한다.

## 참고

게임 어셈블리를 디컴파일한 결과(`Assembly-CSharp/`)는 원저작자의 저작물이므로
저장소에 포함하지 않는다. 타입 참조가 필요하면 각자 로컬에서 디컴파일해 사용할 것.

## 알려진 함정

원본 메서드를 Harmony `Prefix` 로 통째로 대체할 때, 원본이 채우던 **private static 캐시**까지
같이 채워야 한다. 반환값을 버리고 그 필드만 읽는 호출자가 있기 때문이다.

예를 들어 `Util.FindInventoryItemInfoByID` 는 `DeserializeInventoryItemFromCSV()` 의 반환값을
쓰지 않고 `Util.inventoryItems` 정적 필드를 직접 읽는다. 대체 패치가 `__result` 만 채우고
이 필드를 비워두면, 한참 뒤 `InventoryPopup.Close()` 안에서
`PlayOptionViewer.ShowPlayerOption()` 이 `NullReferenceException` 으로 죽는다.
InputSystem 이 액션 콜백의 예외를 삼키기 때문에 콘솔에는 아무것도 안 뜨고
"ESC 키가 안 먹는" 증상으로만 보인다.
