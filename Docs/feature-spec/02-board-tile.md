# 2. 그리드 & 타일 (Board & IngredientData)

> 물리 엔진 미사용, 순수 데이터 그리드 (convention §3).

## 그리드
- 기본 크기 **6x6** (설정값, 하드코딩 금지 — `GameConfig` SO의 `boardWidth`/`boardHeight`)
- 좌표: `(x, y)`, 좌하단 `(0,0)` 기준 배열 인덱스
- 초기화: 매치가 이미 성립된 상태로 생성되지 않도록 초기 배치 시 검증(최소 1회 재섞기 로직)
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

- `IngredientData : ScriptableObject` 필드: `sprite`, `baseScore` (convention §9). 색상 태그(`colorTag`)는 렌더링/로직 어디에서도 쓰이지 않아 2026-08-02 제거 — 타일 정체성은 스프라이트만으로 표현.
- 타일 종류 수는 `GameConfig.tileTypeCount`로 관리, 배열은 등록된 `IngredientData[]`에서 개수만큼 사용

## 타일 뷰 (TileController)
- `IngredientData` 참조해 스프라이트 표시
- 선택/교환/제거 애니메이션은 DOTween 사용(convention §6)
- 생성/소멸은 `TileViewPool` 경유 (convention §5)

## 비스코프
- 타일 등급/승급 — 매치3는 동일 타일 매치이므로 불필요

특수 타일(폭탄류)은 정식 스펙으로 편입됨 → `Docs/feature-spec/12-special-tiles.md` 참고.
