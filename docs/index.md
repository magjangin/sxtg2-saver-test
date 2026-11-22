# Sixtar Gate: STARTRAIL — Version & Network Bypass Documentation

본 디렉토리는 **Sixtar Gate: STARTRAIL — Version & Network Bypass** 모드의 상세 기술 문서 및 개발 가이드를 포함합니다. 본 모드는 **2022년 2월 2일 ~ 2022년 8월 4일** 기간 동안 배포된 게임 버전을 전용 호환 대상으로 작성되었습니다.

## 📚 문서목록 (Table of Contents)

1. [📖 시스템 아키텍처 및 설계 구조 (Architecture)](./architecture.md)
   - 모드 진입점 및 초기화 순서
   - 업데이트 내성 설계 (리플렉션/캐시, 격리 패치, `[OptionalPatch]`, MVID 추적, 폴백 전략)
   - 새 패치를 추가하는 법
2. [⚠️ 패치 세부 사항 및 알려진 함정 (Patches & Gotchas)](./patches-and-gotchas.md)
   - 25개 패치 목록과 게임 버전별 적용 여부(서버 사본 DLL 정적 확인 결과)
   - Private Static 캐시 미동기화 문제 및 ESC 키 먹통 트러블슈팅
   - 부분 결과 캐시, 정적 캐시 필드 이름 차이, Prefix 파라미터 바인딩, 디컴파일 폴더 신뢰 문제
   - 실제 게임 DLL 시그니처를 확인하는 PowerShell 스니펫
3. [📝 변경 이력 (Changelog)](../CHANGELOG.md)

---

## 👥 개발팀 (Developers)

- **화평 (화영왕)** — Lead Modder / Dev
- **Claude (클로드)** — AI Assistant
- **Antigravity (안티그래비티)** — AI Assistant
