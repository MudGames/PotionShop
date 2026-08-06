# 11. 주문 & 스테이지 진행 (Order & Stage Sequence)

> 2026-08-02 정식 스코프 편입. `game-design.md` §8 참고 (기존 "스테이지 진행/맵 선택 비스코프" 방침 대체).

## 개념
- 하나의 "스테이지"는 `LevelData`(ScriptableObject) 하나로 정의된다: 그리드 크기, 이동 횟수 제한, 등장 재료(`IngredientData[]`), 장애물 셀
- 여러 스테이지를 순서대로 담은 `StageSequence`(ScriptableObject)를 `GameManager`가 참조하며, 플레이어가 진행함에 따라 스테이지를 하나씩 넘겨준다
- 씬 전환 없이 하나의 퍼즐 씬(`Main.unity`) 안에서 `LevelData`만 교체하는 방식으로 스테이지가 넘어간다

## 주문 (Order)
- `OrderRequirement { typeIndex, requiredCount }` — "이 재료(typeIndex) 타일을 requiredCount개 모으기"
- 매치/캐스케이드로 제거되는 **일반** 타일마다, 그리고 특수 타일 폭발에 휩쓸려 제거되는 일반
  타일마다 해당 재료의 수집 개수가 1씩 증가 (`GridController` 내부 카운터), `requiredCount`를
  넘어서지 않도록 상한 처리됨(`Mathf.Min`) — **특수 타일 자신**은 터질 때도 별개의 타일로 취급되어
  수집에 반영되지 않는다(2026-08-04 확정, §`12-special-tiles.md`)
- 모든 `OrderRequirement`의 수집 개수가 목표치에 도달하면 스테이지 클리어(`GridController.IsCleared`)
- 진행 상황은 `OrderProgressEventChannel`을 통해 매 캐스케이드 스텝마다 실시간으로 노출됨 (§`06-ui.md`의 주문 재료 패널)

### 랜덤 생성 + 스테이지별 난이도 (2026-08-04)
- 주문은 스테이지를 시작할 때마다(마지막 스테이지를 반복 플레이할 때도 포함) `GridController`
  생성자에서 매번 새로 무작위 생성한다 (`GridController.GenerateRandomRequirements`) — 디자이너가
  고정값으로 미리 정해두는 필드는 없음
- 그 스테이지의 재료(`LevelData.ingredients`) 중 **1~3종**을 중복 없이 무작위로 고르고, 각각
  일정 개수 수집을 요구한다 — 두 값 모두 `GameManager.CurrentStageNumber`(스테이지 1부터 계속
  증가하는 카운터, 마지막 스테이지 반복 포함)에 따라 점점 어려워진다:
  - **재료 종류 수 상한**: 1스테이지는 최대 1종부터 시작, 3스테이지마다 상한이 1씩 늘어나
    7스테이지부터 최대 3종(하드 상한)에서 멈춘다
  - **요구 개수(최소/최대)**: 1스테이지 기준 3~8개에서 시작해 스테이지당 1씩 늘어나되, 개수
    자체가 20을 넘지 않도록 상한 처리된다(끝없이 반복되는 마지막 스테이지에서도 무한정
    어려워지지 않게 하기 위함)
  - `GameManager`가 없는 단독 씬 테스트 환경에서는 stageNumber 기본값 1(가장 쉬운 난이도)로 생성됨

## 스테이지 종료 판정
- 자세한 순서는 `05-move-limit-flow.md` 참고. 요약: 주문 완료가 이동 횟수 소진보다 우선 판정된다
- **주문 완료 시 보드에 남은 특수 타일(물약) 자동 활성화** (2026-08-04) — 일반 캐스케이드가 다
  가라앉은 시점에 주문이 완료돼 있으면, 클리어를 확정하기 전에 `GridController`가 보드에 남아있는
  특수 타일을 하나씩 강제로 터뜨려(`FindFirstSpecialCell` → 다음 캐스케이드 스텝의 강제 시드로 사용)
  보너스 점수를 더 준다. 각 활성화는 새로운 `CascadeStepInfo` 스텝으로 처리되므로 Presentation
  쪽 코드 변경 없이 기존 `PuzzleEffectController.PlayCascadeRoutine`이 이 스텝들까지 순서대로
  재생한 뒤에야(즉 모든 물약이 터지는 연출이 다 끝난 뒤에야) `Match3Controller.OnSwapPlaybackComplete`가
  `IsCleared`를 확인한다. 다만 그 즉시 배너를 띄우면 마지막 폭발 이펙트가 채 가라앉기도 전에 화면이
  가려져 버려서("연출이 좀 끝난 후에 클리어 패널이 뜨면 좋겠습니다" 요청, 2026-08-06),
  `ClearBannerDelay`(0.4초, 2026-08-06 "0.6초가 살짝 깁니다" 피드백으로 조정)만큼 더 기다린 뒤에
  클리어 배너를 띄운다(`Match3Controller.ShowCompleteAfterDelay`) — 라운드 종료 사운드도 이 배너와
  같은 타이밍으로 함께 옮겨졌다.

## 스테이지 전환
- 클리어 시 "수집 완료!" 배너(2026-08-06, "CLEAR!"에서 변경 — 주문=재료 수집이라는 게임 메커닉을
  더 직접적으로 드러내기 위함)의 "다음 스테이지로" 버튼 → `GameManager.AdvanceStage()` → 다음 `LevelData`로 보드 재구성
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
