# 12. 특수 타일 (Special Tiles / 폭탄)

> 2026-08-02 정식 스코프 편입. 기존 `02-board-tile.md`/`03-match-cascade.md`의 "여유 시간 스트레치 목표" 방침 대체.

## 종류 (`SpecialKind`)
| 종류 | 생성 조건 | 활성화 효과 |
|---|---|---|
| `LineRow` / `LineColumn` | 가로/세로 4개 매치 (run의 가운데 셀에 생성) | 해당 행 또는 열 전체 제거 |
| `ColorBomb` | 가로/세로 5개 이상 매치 | 보드 전체에서 해당 색상(재료 종류) 전부 제거 |
| `RadiusBomb` | 가로+세로 매치가 교차(L자/T자)하는 지점 | 중심 기준 3x3 범위 제거 |

- 생성 규칙 우선순위: 길이 4 → 길이 5+ → 교차(교차가 있으면 해당 셀은 `RadiusBomb`으로 덮어씀). 상세는 `SpecialTileResolver.Plan` 참고
- 특수 타일로 확정된 셀(anchor)은 그 매치 스텝에서 제거되지 않고 보드에 남는다
- **특수 타일은 매치 대상이 아니다(2026-08-03 확정)**: 이미 만들어진 특수 타일은 색깔이 같아도
  `MatchDetector`가 런에 끼워주지 않는다 — 캐스케이드 중 우연히 같은 색 사이에 끼여도 조용히
  사라지지 않고 그대로 남아, 오직 아래 "활성화 방법"(직접 스왑/탭)으로만 사라진다. 다른 폭탄이
  터져서 그 효과 범위 안에 있는 연쇄 폭발은 별개(계속 자동으로 일어남).

## 활성화 방법
- 특수 타일을 인접 타일과 교환(swap) → 즉시 활성화, 이동 횟수 소모
- 특수 타일을 단독으로 탭 → 즉시 활성화, 이동 횟수 소모 (`GridController.TryActivateSpecial`)
- 활성화된 특수 타일의 효과 범위 안에 다른 특수 타일이 있으면 연쇄 폭발(chain reaction)한다 — 안전장치로 보드 크기 기준 반복 상한을 둠(`SpecialTileResolver.ExpandWithChainReactions`)

## 콤보 결합 (두 특수 타일을 서로 swap)
| 조합 | 효과 |
|---|---|
| Line + Line | 두 지점 모두 십자(행+열) 제거 |
| ColorBomb + ColorBomb | 보드 전체 제거 |
| ColorBomb + Line | 해당 색상의 모든 타일이 각각 Line 폭탄인 것처럼 터짐 |
| RadiusBomb + RadiusBomb | 두 지점 모두 반경 5x5로 확대 제거 |
| RadiusBomb + Line | Line 방향으로 3줄(두꺼운 라인) 제거 |
| RadiusBomb + ColorBomb | 해당 색상의 모든 타일이 각각 RadiusBomb 중심인 것처럼 터짐 |

## 점수/주문 연동
- 특수 타일 폭발로 제거된 타일도 일반 매치 제거와 동일하게 점수 및 주문(Order) 수집 개수에 반영됨
- 특수 타일 자체(anchor)가 생성되는 셀은 "제거"가 아니므로 그 타일의 점수/수집 집계에는 포함되지 않음 (다음 폭발 때 집계됨)

## 비주얼 (확정, 2026-08-02)
- 세 종류 모두 재료 아이콘을 완전히 덮는 물약 배지로 표시 (Cainos Potion 아이콘 재사용, `asset-credits.md` 참고)
- `LineRow`/`LineColumn` (배지 공용): Red Potion — 활성화 전에는 행/열 중 어느 쪽인지 배지만으로는 알 수 없음(의도된 단순화, 방향 화살표 없음)
- `ColorBomb`: Green Potion
- `RadiusBomb`: Blue Potion
