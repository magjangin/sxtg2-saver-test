# Sixtar Gate: STARTRAIL — Version & Network Bypass Documentation

본 디렉토리는 **Sixtar Gate: STARTRAIL — Version & Network Bypass** 모드의 상세 기술 문서 및 개발 가이드를 포함합니다. 본 모드는 **2 February 2022 ~ 4 August 2022** 기간 동안 배포된 게임 버전을 전용 호환 대상으로 작성되었습니다.

## 📚 문서목록 (Table of Contents)

1. [📖 시스템 아키텍처 및 설계 구조 (Architecture)](./architecture.md)
   - 모드 진입점 및 초기화 순서
   - 4단계 게임 업데이트 내성 설계 (리플렉션, 격리 패치, MVID 추적, 폴백 전략)
2. [⚠️ 패치 세부 사항 및 알려진 함정 (Patches & Gotchas)](./patches-and-gotchas.md)
   - `NetworkPatches`, `DataPatches`, `UIPatches` 모듈별 세부 역할
   - Private Static 캐시 미동기화 문제 및 ESC 키 먹통 트러블슈팅
   - Unity InputSystem 예외 삼킴 대처 가이드

---

## 👥 개발팀 (Developers)

- **화평 (화영왕)** — Lead Modder / Dev
- **Claude (클로드)** — AI Assistant
- **Antigravity (안티그래비티)** — AI Assistant
