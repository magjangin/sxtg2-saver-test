# Sixtar Gate: STARTRAIL — Version & Network Bypass

[MelonLoader](https://github.com/LavaGang/MelonLoader) 기반 모드. 오프라인/서버 미구동 환경에서 *Sixtar Gate: STARTRAIL*이 정상 작동하도록 버전 체크 및 서버 통신 경로를 우회합니다.

## 📌 주요 기능 & 환경

- **대상 환경**: Sixtar Gate: STARTRAIL | Unity 2020.3.9f1 (Mono) | MelonLoader 0.7.3 | 모드 DLL은 .NET Framework 4.7.2로 빌드
- **목표 호환 버전**: **2022년 2월 2일 ~ 2022년 8월 4일** 기간 내에 출시/업데이트된 게임 버전과의 호환성을 전제로 제작되었습니다.
- **핵심 우회**: 서버 통신 차단 및 더미 응답 처리, 로컬 데이터/언락 캐시 대체, 크리티컬 팝업/업데이트 체커 우회
- **업데이트 내성**:
  - 동적 리플렉션(`ReflectionUtils`)으로 메서드·타입·필드 탐색 (결과 캐시, 상속 계층 탐색)
  - Harmony 격리 패처(`PatchRegistry`)로 개별 패치 실패 시 전체 중단 방지
  - 방어적 패치는 폴백(`Finalizer`) 구조를 적용하여 원본 동작 유지
  - 버전에 따라 없는 메서드를 막는 패치는 `[OptionalPatch]`로 표시 → 실패가 아니라 `N/A`로 보고
  - 게임 어셈블리(MVID)가 이전 실행과 달라지면 경고

## 🚀 설치

1. [Releases](https://github.com/magjangin/sxtg2-saver-test/releases)에서 `SixtarBypassMod.dll`을 받습니다.
2. 게임 폴더에 [MelonLoader 0.7.3](https://github.com/LavaGang/MelonLoader/releases)을 설치합니다.
3. `SixtarBypassMod.dll`을 게임 폴더의 `Mods\`에 넣습니다.
4. 게임을 실행하고 `MelonLoader\Latest.log`에서 `Patch Report`를 확인합니다.

> 같은 폴더에 이 모드의 이전 이름(`sxtg2.dll` 등)이 남아 있으면 패치가 두 번 적용될 수 있습니다. 하나만 남기세요.

## 🔍 패치 리포트 읽는 법

부팅 시 콘솔(`Latest.log`)과 `UserData\SixtarBypass.report.txt`에 패치별 결과가 남습니다.

| 표시 | 의미 | 조치 |
| --- | --- | --- |
| `OK` | 적용됨 | - |
| `N/A` | 이 게임 버전에는 대상이 없는 선택 패치 | 정상. 무시 |
| `SKIP` | 필수 패치의 대상을 못 찾음 | 게임이 업데이트되었을 가능성. 이름 확인 |
| `FAIL` | 패치 적용 중 예외 | 로그의 예외 이름과 함께 이슈로 제보 |

버그 제보 시 `SixtarBypass.report.txt`와 `Latest.log`를 첨부해 주세요. 패치별 대상과 버전 차이는 [패치 세부 사항](./docs/patches-and-gotchas.md)을 참고하세요.

## 📁 프로젝트 구조

```
sxtg2 saver test/
├── SixtarBypassMod.cs        # 모드 진입점 (MelonMod) + ModInfo(이름/버전 단일 출처)
├── Helpers/
│   ├── ReflectionUtils.cs    # 리플렉션 헬퍼 (타입/메서드/필드 탐색, 캐시)
│   ├── PatchRegistry.cs      # 격리 패치 적용, 리포트, MVID 추적, [OptionalPatch]
│   └── PatchHelpers.cs       # 서버 콜백을 빈 응답으로 호출하는 공용 도우미
├── Patches/
│   ├── NetworkPatches.cs     # ManagerWebRequest 서버 통신 차단
│   ├── DataPatches.cs        # CSV 역직렬화, 곡 목록, 언락 캐시, 즐겨찾기
│   └── UIPatches.cs          # 업데이트 체커, 크리티컬 에러 팝업, 튜토리얼
└── Properties/AssemblyInfo.cs
build.bat                     # 빌드 + Mods\ 배포
docs/                         # 아키텍처, 패치 세부, 함정 모음
CHANGELOG.md
```

## 🛠️ 빌드

요구사항: .NET SDK (`dotnet`) 또는 Visual Studio MSBuild, 그리고 **게임이 설치된 폴더** (참조 어셈블리를 거기서 읽습니다).

```powershell
# 기본 게임 폴더(H:\Sixtar Gate STARTRAIL server)로 빌드 + Mods\ 배포
.\build.bat

# 게임 폴더 지정
.\build.bat "D:\Games\Sixtar Gate STARTRAIL"

# 배포 없이 빌드만
dotnet build "sxtg2 saver test\sxtg2 saver test.csproj" -c Release /p:GameDir="D:\Games\Sixtar Gate STARTRAIL"
```

- 산출물: `sxtg2 saver test\bin\Release\SixtarBypassMod.dll`
- 참조 경로는 csproj의 `GameDir` 속성 하나로 결정됩니다. `HintPath`를 개별 수정할 필요가 없습니다.
- 버전은 `SixtarBypassMod.cs`의 `ModInfo.Version` 한 곳에서만 올립니다 (MelonInfo/AssemblyInfo가 함께 따라옵니다).

> **참고**: 게임 디컴파일 소스(`Assembly-CSharp/`)는 원저작권 보호를 위해 저장소에 포함되지 않습니다(`.gitignore`).

## 📚 상세 문서 (Documentation)

자세한 아키텍처 및 트러블슈팅 내역은 [`docs/`](./docs/index.md) 디렉토리를 참조하세요.

- [📖 시스템 아키텍처 및 설계 구조](./docs/architecture.md)
- [⚠️ 패치 세부 사항 및 알려진 함정](./docs/patches-and-gotchas.md)
- [📝 변경 이력](./CHANGELOG.md)

## 👥 개발자 (Developers)

- **화평 (화영왕)** - Lead Modder / Mod Developer
- **Claude (클로드)** - AI Assistant
- **Antigravity (안티그래비티)** - AI Assistant
