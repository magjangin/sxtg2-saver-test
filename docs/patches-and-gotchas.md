# 패치 세부 사항 및 알려진 함정 (Patches & Gotchas)

본 문서는 **Sixtar Gate: STARTRAIL — Version & Network Bypass** 모드의 각 패치 모듈별 구현 세부 사항과 모딩 중 발견된 트러블슈팅 및 함정 사례를 기록합니다.

---

## 1. 패치 모듈별 역할

### `NetworkPatches.cs` (서버 네트워크 우회)
- **`ManagerWebRequest.FetchServer`**: 모든 서버 API 요청을 인터셉트하여 로컬 응답을 반환하도록 제어.
- **오프라인 응답 구조체 구성**: 계정 인증, 프로필 정보, 해금 상태 등을 더미 응답 데이터로 대체하여 서버 비활성화 상태에서도 게임 진행이 막히지 않도록 지원.

### `DataPatches.cs` (데이터 역직렬화 & 언락 캐시)
- **`DeserializeInventoryItemFromCSV`**: CSV 기반 인벤토리 항목 직렬화 데이터 파싱을 오프라인 환경에 맞게 보정.
- **`SaveUnlockCachePatch` & `LoadFavorites`**: 로컬에 저장된 곡 언락 캐시 및 즐겨찾기 데이터를 불러오거나 보존.

### `UIPatches.cs` (UI & 크리티컬 팝업 우회)
- **업데이트 체커 팝업 우회**: 최신 버전 미확인 팝업이나 서버 연결 실패 에러 팝업을 억제하여 타이틀 화면 진입을 보장.
- **뮤직 셀렉트 초기화**: 서버 통신 실패로 인해 곡 선택 메뉴에서 발생하는 프리징 현상을 방지.

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

---

## 3. Unity InputSystem 예외 삼킴(Swallowing) 대처법

1. **Inspector / UnityExplorer 활용**: 콘솔 로그에 출력이 없더라도 UnityExplorer의 Object Explorer 또는 Hook을 활용해 현장에서 예외 및 정적 필드 값을 추적.
2. **Key Event Callback Wrapping**: ESC 키나 특정 입력 이벤트 콜백에 `try-catch` 및 로깅을 직접 주입하여 예외 발생 지점 탐지.
