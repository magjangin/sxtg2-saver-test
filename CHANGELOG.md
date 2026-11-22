# 변경 이력 (Changelog)

형식은 [Keep a Changelog](https://keepachangelog.com/ko/1.1.0/)를 따르고, 버전은 `ModInfo.Version`(SixtarBypassMod.cs)과 같습니다.

## [1.9.3] — 2026-11-22

> 정적 검증(빌드 + 게임 DLL 시그니처 대조)만 거쳤고 **인게임 검증은 아직 하지 않았습니다.**

### 수정
- **곡 목록 CSV 패치**: 컬럼이 없거나 행이 깨져도 목록 전체를 잃지 않도록 행 단위로 격리하고, 불완전한 결과는 정적 캐시에 저장하지 않도록 변경 (이전에는 예외 시 일부 곡만 담긴 목록이 영구 캐시될 수 있었음).
- `BGAOffset` 파싱이 쉼표 소수점 로케일에서 0이 되던 문제 (`InvariantCulture` 우선).
- 인벤토리/언락 폴백이 같은 이름의 다른 타입 정적 필드를 덮어쓸 수 있던 문제 (필드 타입 검사 추가) 및 `Finalizer` 밖으로 예외가 새던 문제.
- `InvokeMethod`가 인자와 무관하게 파라미터 최소 오버로드를 고르던 문제 (인자 타입에 맞는 오버로드 선택).
- 서버 콜백(`GetNewsList` 등) 호출 중 예외가 게임 코루틴으로 새던 문제 (격리 + 로그).
- `SaveUnlockCachePatch`가 반환값이 있는 메서드를 잡으면 제외하도록 방어.

### 성능
- 곡 목록 패치가 캐시 확인보다 먼저 로그를 찍던 핫패스 제거 (호출마다 로그/비용 발생).
- `ReflectionUtils.FindType` 결과 캐시 (어셈블리 전체 `GetTypes()` 순회 반복 제거), `Assembly-CSharp` 우선 검색.

### 업데이트 내성
- `[OptionalPatch]` 도입: 특정 버전에만 있는 패치는 `FAIL/SKIP` 대신 `N/A`로 보고.
- 패치 리포트를 `UserData\SixtarBypass.report.txt`로도 저장, 게임 어셈블리 MVID 변경 감지 경고.
- 곡 목록/업데이트 체커 패치의 컴파일 타임 필드 참조(`Util.tracks`, `runtimeCheck`)를 리플렉션으로 대체 → 필드가 없는 버전에서도 패치 JIT 실패 방지.
- `FindType`/`GetFieldValue`/`SetFieldValue`가 부모 클래스의 private 멤버까지 탐색, 실패는 조용히 삼키지 않고 1회 경고.
- 대상 게임이 아니면(`ManagerWebRequest` 없음) 패치를 시도하지 않음 (`MelonGame(null, null)` 보완).

### 빌드 / 문서
- `GameDir` 속성 한 곳으로 모든 참조 경로 통일, 소스 파일 와일드카드 포함, `build.bat`이 게임 폴더 인자/환경변수 지원.
- 버전을 `ModInfo.Version` 한 곳으로 통일 (MelonInfo, AssemblyInfo, 로그).
- README/아키텍처/패치 문서를 실제 코드와 맞추고 패치별 버전 적용 표 추가.

## [1.9.2] 이전

`git log` 참고.
