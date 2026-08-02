# 코드 규약 (Convention) — Luna & Stella's Potion Shop

> 코드 작성·수정 전 필독. CLAUDE.md 요약과 충돌 시 **이 문서가 SoT**.

---

## §1. 네이밍 & 설정값 분리

- `MonoBehaviour`: 역할 명사 (`GridController`, `TileController`, `ScoreManager`)
- `ScriptableObject`: `~Data` 접미사 (`IngredientData`), 이벤트 채널은 `~EventChannel` 접미사
- 인터페이스: `I` 접두사 (`ISwappable` 등, 필요 시)
- **하드코딩 금지 대상**(반드시 ScriptableObject 또는 상수 설정 클래스로 분리):
  - 그리드 크기(기본 6x6)
  - 타일 종류 수(기본 6종)
  - 이동 횟수 제한(기본 20회)

## §2. 필드/접근 규칙

- 필드는 `[SerializeField] private`만 사용 (public 필드 금지)
- 매직 넘버 금지 — 전부 ScriptableObject 참조 또는 명명된 상수

## §3. 보드/매치 로직 계약

- `GridController`
  - 보드 상태는 `IngredientData[,]` 또는 이에 준하는 2차원 배열(순수 데이터)로 관리
  - 물리 엔진(Rigidbody2D/Collider) 사용 금지 — 좌표는 배열 인덱스 기준
  - 매치 탐색: 가로/세로 각각 연속 3개 이상 동일 `IngredientData` 탐색
  - 캐스케이드: 매치된 타일 제거 후 위 칸에서 낙하 채움, 빈 칸은 신규 타일로 보충
- `TileController`
  - 스왑 입력을 받아 `GridController`에 매치 판정을 **요청**만 함 (`SwapRequested`/`SpecialActivationRequested`/`TileSelected` 순수 C# 이벤트로 `Match3Controller`에 알림 — `MatchEvent` 같은 채널은 없음)
  - `GameManager`/점수 매니저를 직접 호출하지 않음 — 결과는 `GridController`가 `IntEventChannel`/`OrderProgressEventChannel` 등으로 노출
  - 매치 실패(스왑 후 매치 無) 시 원위치로 되돌리고 이동 횟수 미소모

## §4. 이벤트 채널 (ScriptableObject Event Channel 패턴)

시스템 간 결합은 이벤트 채널로만. `GameManager`/`GridController`/UI 컴포넌트가 서로 직접 참조하지 않음.

실제 채널 (제네릭 `IntEventChannel`/`VoidEventChannel` + 도메인 전용 `OrderProgressEventChannel`):
- `IntEventChannel` — 점수 변경, 잔여 이동 횟수 변경
- `OrderProgressEventChannel` — 주문(Order) 진행 상황 (`IReadOnlyList<OrderProgressEntry>`)
- `VoidEventChannel` — 스테이지 클리어, 게임 오버, 다음 스테이지 요청, 스테이지 시작

※ 콤보(연쇄 매치 카운트)는 별도 이벤트 채널 없이 `GridController.MaxCombo` 프로퍼티를 직접 읽는 방식으로 구현됨 (`04-score-combo.md` 참고) — 실시간 갱신이 필요한 값(점수/이동 횟수/주문 진행도)만 채널을 사용

## §5. 오브젝트 풀링

- 타일 생성/소멸(캐스케이드 신규 타일 포함)은 반드시 목적별 풀 경유. `new`/`Destroy` 남발 금지.
- 풀은 도메인별로 분리 (`TileViewPool`, `BoardSnapshotPool` 등) — 범용 단일 풀 매니저 두지 않기.
- 각 풀이 자기 도메인의 rent/return만 책임짐.
- 목표: 1분 플레이 GC Alloc 0 (Profiler로 검증, README에 수치 근거 기록).

## §6. 애니메이션/트윈

- 타일 교환·낙하(캐스케이드) 연출은 DOTween 또는 Coroutine 기반 Tween만 사용.
- 물리 시뮬레이션(Rigidbody 힘/충돌)로 연출하지 않음.
- 마녀/스텔라(Stella, 검은고양이) 캐릭터: Cozy Fantasy Asset Pack의 Idle/Walking(4방향)/Sitting 애니메이션만 사용 — 공격 등 신규 애니메이션 제작하지 않음(에셋에 없음, 필요 시 이펙트로 대체).

## §7. 입력

- 신규 **Input System** 패키지(`com.unity.inputsystem`) 사용. 레거시 Input Manager 금지.
- 마우스 클릭(또는 터치)로 타일 선택/교환. 키보드 입력 불필요(매치3 특성상).

## §8. 렌더/레이어

- URP 2D Renderer. 타일/캐릭터/UI는 Sorting Layer로 구분 (`Background` < `Board` < `Character` < `VFX` < `UI`).
- 물리 충돌 매트릭스 불필요 (물리 미사용, §3).

## §9. 데이터-코드 계약

- `IngredientData : ScriptableObject` 필드: 스프라이트(`sprite`)만 존재. **등급/승급 필드 없음** (매치3는 동일 타일 매치이므로 불필요 — Luna & Stella 초안 원칙 유지). 색상 태그(`colorTag`)는 렌더링/로직 어디서도 쓰이지 않는 죽은 필드였기에 제거됨.
- 매치당 기본 점수는 별도 SO/설정 클래스로 분리돼 있지 않고 `GridController`의 상수(`PointsPerTile`)로만 존재함 — `GameConfig`/`ScoreConfig` 같은 SO는 없음.
