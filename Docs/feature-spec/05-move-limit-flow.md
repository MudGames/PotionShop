# 5. 이동 횟수 제한 & 스테이지 종료 (Move Limit & Stage End)

## 이동 횟수
- 스테이지별 설정값 `LevelData.moveLimit` (기본 20회, 0이면 무제한 — 하드코딩 금지, §`11-order-stage.md`)
- **유효 교환(매치 성립, 또는 특수 타일 활성화/콤보)만 1회 소모.** 매치 불성립 교환은 소모하지 않음(`01-input.md`)
- 보드 옆 미션 패널에 잔여 이동 횟수 표시 (§`06-ui.md`)

## 스테이지 종료 판정 (우선순위)
캐스케이드가 **완전히 끝난 뒤**(매치 애니메이션 잘림 방지) 다음 순서로 판정한다:
1. **주문 완료** (모든 `OrderRequirement` 충족) → 스테이지 클리어. 이동 횟수가 남아있어도 클리어가 우선한다.
2. 주문 미완료 상태로 **이동 횟수 소진** → 게임 오버.
- 판정 후에는 더 이상 스왑을 받지 않는다(`GridController.CanAcceptSwaps`).

## 상태 흐름
```
[스테이지 로드] → [Playing] → (주문 완료) → [Clear 배너] → (버튼) → 다음 스테이지 [Playing]
                            → (이동 횟수 소진, 주문 미완료) → [Game Over 배너] → (§06-ui.md, 결과 화면 미구현)
```
- `GridController`가 `IsCleared`/`IsGameOver` 프로퍼티로 상태 노출, `Match3Controller`가 캐스케이드 애니메이션 종료 시점에 `OrderClearedChannel`/`GameOverChannel`을 Raise
- `GameManager`가 현재 스테이지 인덱스를 보유하며, 씬 전환 없이 같은 퍼즐 씬 안에서 `LevelData`만 교체

## 비스코프
- 타이머 기반 종료, 일시정지 메뉴
