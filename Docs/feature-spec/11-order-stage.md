# 11. 주문 & 스테이지 진행 (Order & Stage Sequence)

> 2026-08-02 정식 스코프 편입. `game-design.md` §8 참고 (기존 "스테이지 진행/맵 선택 비스코프" 방침 대체).

## 개념
- 하나의 "스테이지"는 `LevelData`(ScriptableObject) 하나로 정의된다: 그리드 크기, 이동 횟수 제한, 등장 재료(`IngredientData[]`), 장애물 셀
- 여러 스테이지를 순서대로 담은 `StageSequence`(ScriptableObject)를 `GameManager`가 참조하며, 플레이어가 진행함에 따라 스테이지를 하나씩 넘겨준다
- 씬 전환 없이 하나의 퍼즐 씬(`Main.unity`) 안에서 `LevelData`만 교체하는 방식으로 스테이지가 넘어간다

## 주문 (Order)
- `OrderRequirement { typeIndex, requiredCount }` — "이 재료(typeIndex) 타일을 requiredCount개 모으기"
- 매치/캐스케이드/특수 타일 폭발로 제거되는 타일마다 해당 재료의 수집 개수가 1씩 증가 (`GridController` 내부 카운터), `requiredCount`를 넘어서지 않도록 상한 처리됨(`Mathf.Min`)
- 모든 `OrderRequirement`의 수집 개수가 목표치에 도달하면 스테이지 클리어(`GridController.IsCleared`)
- 진행 상황은 `OrderProgressEventChannel`을 통해 매 캐스케이드 스텝마다 실시간으로 노출됨 (§`06-ui.md`의 미션 패널)

### 랜덤 생성
- 미션은 스테이지를 시작할 때마다(마지막 스테이지를 반복 플레이할 때도 포함) `GridController`
  생성자에서 매번 새로 무작위 생성한다 (`GridController.GenerateRandomRequirements`) — 디자이너가
  고정값으로 미리 정해두는 필드는 없음
- 그 스테이지의 재료(`LevelData.ingredients`) 중 **1~3종**을 중복 없이 무작위로 고르고, 각각
  **3~8개** 수집을 요구한다

## 스테이지 종료 판정
- 자세한 순서는 `05-move-limit-flow.md` 참고. 요약: 주문 완료가 이동 횟수 소진보다 우선 판정된다

## 스테이지 전환
- 클리어 시 "클리어!" 배너의 "다음 스테이지로" 버튼 → `GameManager.AdvanceStage()` → 다음 `LevelData`로 보드 재구성
- 마지막 스테이지 클리어 후에는 같은 스테이지에 머물러 반복 플레이 가능 (별도의 "캠페인 완료" 연출은 비스코프, `game-design.md` §8)
- `GameManager`는 씬을 넘나들며 유지되는 진행 상태(`DontDestroyOnLoad`)이며, 클리어한 스테이지 제목 목록(`CompletedStageTitles`)도 누적 보유
- **점수는 스테이지가 바뀌어도 초기화되지 않고 캠페인 전체에 걸쳐 누적된다** (`Match3Controller.OnAdvanceRequested`가 직전 스테이지의 `GridController.Score`를 다음 `GridController` 생성자의 시작값으로 전달) — 이동 횟수/주문 진행도는 스테이지마다 새로 시작되는 것과 대비됨

## 데이터 계약
- `LevelData`: `title`, `flavorText`, `rows`, `columns`, `moveLimit`(0=무제한), `ingredients[]`, `blockedCells[]`
- `StageSequence`: `LevelData[] stages`
- 신규 스테이지 추가는 `LevelData`/`StageSequence` 에셋을 만들고 값만 채우는 것으로 끝남 — 코드 변경 불필요 (`convention.md` §1 하드코딩 금지 원칙과 일치)

## 비스코프
- 스테이지/맵 선택 화면 (순서대로만 진행, 플레이어가 임의로 스테이지를 고르는 기능 없음)
- 전체 캠페인 클리어 시의 별도 엔딩 연출
