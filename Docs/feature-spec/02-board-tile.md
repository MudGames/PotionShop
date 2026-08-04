# 2. 그리드 & 타일 (Board & IngredientData)

> 물리 엔진 미사용, 순수 데이터 그리드 (convention §3).

## 그리드
- 기본 크기 **6x6** (설정값, 하드코딩 금지 — `LevelData.rows`/`columns`, 최소 3)
- 좌표: `GridCell(row, col)`, 좌상단 `(0,0)` 기준 배열 인덱스
- 초기화(`BoardInitializer`): 매치가 이미 성립된 상태로 생성되지 않도록 초기 배치 시 검증(좌→우/
  상→하로 채우며 직전 2칸과 같은 타입을 피하는 제약 완화 체인)
- **교환 가능한 수가 하나도 없는 상태("데드락")로 시작하는 것도 방지한다(2026-08-03 추가)**:
  `GridController` 생성자가 초기 배치 직후 `DeadlockDetector.HasValidMove`를 확인하고, 없으면
  즉시 `DeadlockDetector.Reshuffle`로 재배치한다 — 캐스케이드 종료 후(교환 성사 시점)에도 동일하게
  검사한다(`03-match-cascade.md` 참고)
- 그리드 크기/이동 횟수/재료 배열은 스테이지(`LevelData`)마다 다르게 설정 가능 (§`11-order-stage.md`)
- 장애물 셀(`LevelData.blockedCells`): 영구적으로 막혀 매치/캐스케이드에서 제외되는 선택적 셀

## 타일 (IngredientData)

기본 6종 (game-design.md §6, 2026-08-02 "채집 재료" 컨셉으로 변경):

| 재료 | 아이콘 |
|---|---|
| 흑요석 조각 | Cainos Obsidian |
| 빨간 사과 | Cainos Apple |
| 이끼 방울 | Cainos Slime Gel |
| 달빛 버섯 | Cainos Mushroom |
| 마법 수정 | Cainos Crystal |
| 요정 깃털 | Cainos Feather |

- `IngredientData : ScriptableObject` 필드는 `sprite` 하나뿐이다 — 색상 태그(`colorTag`)/등급/
  점수(`baseScore`) 필드는 두지 않는다. 타일 정체성은 스프라이트만으로 표현하고, 매치당 점수는
  `GridController`의 상수(`PointsPerTile`)로만 존재한다(§`04-score-combo.md`).
- 타일 종류 수는 별도 설정값이 아니라 `LevelData.ingredients` 배열의 길이 자체로 결정된다
  (`GridController`가 `ingredients.Length`를 타입 개수로 읽어 들임)

## 타일 뷰 (TileController)
- `IngredientData` 참조해 스프라이트 표시
- 선택/교환/제거 애니메이션은 DOTween 사용(convention §6)
- 생성/소멸은 `TileViewPool` 경유 (convention §5)
- **칸 배경은 고정 레이어(2026-08-04 추가)**: 빈 슬롯 사각형(`BoardView.CreateBackgroundGrid`)은
  칸 자체에 속한 별도 오브젝트로, `Build()`가 다시 불릴 때까지 절대 움직이지 않는다. 반면 재료
  아이콘/스페셜 배지를 담은 `TileView`는 "말"에 해당해서 스왑/캐스케이드마다 다른 칸으로 재배정되며
  이리저리 움직인다 — 이전에는 배경(`TileView`의 배경 이미지)이 말과 같은 RectTransform에 있어서
  드래그 미리보기·스왑·캐스케이드 애니메이션 중 배경까지 함께 움직여 보였다(§`01-input.md`).

## 비스코프
- 타일 등급/승급 — 매치3는 동일 타일 매치이므로 불필요

특수 타일(폭탄류)은 정식 스펙으로 편입됨 → `Docs/feature-spec/12-special-tiles.md` 참고.
