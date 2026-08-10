# Sixtar Gate: STARTRAIL — Version & Network Bypass

[MelonLoader](https://github.com/LavaGang/MelonLoader) 기반 모드. 오프라인/서버 미구동 환경에서 *Sixtar Gate: STARTRAIL*이 정상 작동하도록 버전 체크 및 서버 통신 경로를 우회합니다.

## 📌 주요 기능 & 환경

- **대상 환경**: Sixtar Gate: STARTRAIL | Unity 2020.3.9f1 (Mono) | MelonLoader 0.7.3 (.NET Framework 4.7.2)
- **핵심 우회**: 서버 통신 차단 및 더미 응답 처리, 로컬 데이터/언락 캐시 대체, 크리티컬 팝업/업데이트 체커 우회
- **업데이트 내성**: 
  - 동적 리플렉션(`ReflectionUtils`)으로 메서드 탐색
  - Harmony 격리 패처(`PatchRegistry`)로 개별 패치 실패 시 전체 중단 방지
  - 방어적 패치는 폴백(`Finalizer`) 구조를 적용하여 원본 동작 유지

## 📁 프로젝트 구조

```
sxtg2 saver test/
├── SixtarBypassMod.cs        # 모드 진입점 (MelonMod)
├── Helpers/
│   ├── ReflectionUtils.cs    # 리플렉션 헬퍼
│   └── PatchRegistry.cs      # 격리 패치 적용 및 리포트 출력
└── Patches/
    ├── NetworkPatches.cs     # ManagerWebRequest 서버 통신 차단
    ├── DataPatches.cs        # CSV 역직렬화 및 언락 캐시 대체
    └── UIPatches.cs          # 크리티컬 에러 및 네트워크 팝업 우회
```

## 🛠️ 빌드 & 설치

1. `sxtg2 saver test.csproj`의 `HintPath`를 사용자의 로컬 게임 경로로 수정합니다.
2. 프로젝트를 빌드한 후 `bin/Debug/SixtarBypassMod.dll`을 게임 폴더의 `Mods/` 경로에 복사합니다.

> **참고**: 게임 디컴파일 소스는 원저작권 보호를 위해 저장소에 포함되지 않습니다.

## 📚 상세 문서 (Documentation)

자세한 아키텍처 및 트러블슈팅 내역은 [`docs/`](./docs/index.md) 디렉토리를 참조하세요.

- [📖 시스템 아키텍처 및 설계 구조](./docs/architecture.md)
- [⚠️ 패치 세부 사항 및 알려진 함정](./docs/patches-and-gotchas.md)

## 👥 개발자 (Developers)

- **화평 (화영왕)** - Lead Modder / Mod Developer
- **Claude (클로드)** - AI Assistant
- **Antigravity (안티그래비티)** - AI Assistant


