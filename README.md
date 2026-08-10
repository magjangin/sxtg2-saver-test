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
│   └── ReflectionUtils.cs    메서드/필드 이름이 바뀌어도 견디는 리플렉션 헬퍼
└── Patches/
    ├── NetworkPatches.cs     ManagerWebRequest 계열 서버 요청 차단 및 더미 응답
    ├── DataPatches.cs        CSV 역직렬화 / 언락 캐시 등 로컬 데이터 경로 대체
    └── UIPatches.cs          업데이트 체커, 크리티컬 에러 팝업, 뮤직셀렉트 초기화
```

패치는 `ReflectionUtils.FindMethod` 로 대상 메서드를 런타임에 찾은 뒤
`Prepare()` / `TargetMethod()` 로 Harmony 에 넘긴다. 대상이 없으면 해당 패치만 조용히
비활성화되므로 게임이 업데이트돼도 모드 전체가 죽지는 않는다.

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
