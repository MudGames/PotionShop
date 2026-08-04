# 3. 매치 판정 & 캐스케이드 (Match & Cascade)

> 결정론적 로직, 물리 미사용 (convention §3).

## 매치 판정
- 교환 직후 `MatchDetector.FindRuns`가 보드 전체를 가로/세로로 스캔해 **연속 동일 `IngredientData`
  3개 이상**인 run을 찾는다(교환된 두 타일 주변만 보는 게 아니라 전체 스캔 — 캐스케이드 반복마다
  동일한 방식으로 재사용하기 위함)
- 3개, 4개, 5개 이상 매치 모두 유효 (점수 공식은 `04-score-combo.md`)
- L자/T자 형태(교차 매치)도 유효 매치로 판정, 겹치는 타일은 중복 제거 후 1회만 제거
- **이미 만들어진 특수 타일(물약)은 매치 대상이 아니다(2026-08-03 확정)**: 색깔이 같아도 런에
  끼워주지 않는다 — 캐스케이드 중 우연히 같은 색 사이에 끼여도 조용히 사라지지 않고 그대로 남고,
  오직 직접 스왑/탭으로만 사라진다(다른 폭탄의 연쇄 폭발 범위에 들어가는 경우는 예외, 계속 자동으로
  일어남). 상세는 `12-special-tiles.md` 참고.

## 캐스케이드 (연쇄, `GridController.ResolveCascadeAndFinish`)
1. 매치된 타일 제거 (풀로 반환) — 그냥 페이드아웃이 아니라 살짝 부풀었다가(팝) 줄어들며, 동시에
   무작위 각도로 살짝 회전하며 사라진다(`TileView.AnimateFadeOut` 참고, 2026-08-04 추가 — 페이드만
   으로는 손맛이 부족하다는 피드백에 이어, "좀 더 동적으로" 만들어달라는 후속 피드백으로 회전과
   매치/콤보 규모 비례 크기를 추가). 팝 크기는 규모에 비례한다(`PuzzleEffectController.
   ComputeClearPopScale` 참고): 3개 매치(또는 캐스케이드가 아닌 기본 스텝)는 1.25배, 4개 매치는
   1.4배, 5개 이상 매치나 캐스케이드로 이어진 스텝(스페셜 타일 폭발 포함)은 1.6배로 튄다. 배경(칸
   슬롯)은 이 타일과 분리된 고정 레이어라 함께 부풀거나 회전하지 않는다(§`02-board-tile.md`).
2. 제거된 칸 위의 타일들이 아래로 낙하 (빈 칸 수만큼 이동, Tween으로 낙하 연출)
3. 상단 빈 칸은 신규 타일로 보충 (풀에서 대여) — 자신의 최종 위치 바로 위에서 슬라이드해
   내려온다(`BoardView.AnimateGravity`/`TileView.AnimateMoveTo` 참고). 이 스텝에서 실제로
   바뀐 셀(제거/이동 도착지/새로 채워짐)만 `BoardView.RefreshChangedCells`로 최종 상태를
   즉시 맞춘다 — 캐스케이드가 잦은 매치3 특성상 바뀌지 않은 나머지 셀까지 매번 훑는 전체
   리프레시는 낭비이기 때문.
4. 낙하/보충 완료 후 **보드 전체 재탐색** — 새로운 매치가 발생하면 2번부터 반복
5. 더 이상 매치가 없으면 캐스케이드 종료 → 콤보 카운트 확정, 입력 재활성화
6. **일반 캐스케이드가 다 가라앉은 시점에 주문이 이미 완료돼 있다면(2026-08-04 추가)**, 클리어를
   확정하기 전에 보드에 남은 특수 타일(물약)을 하나씩 강제로 추가 캐스케이드 스텝으로 터뜨려
   보너스 점수를 더 준다(`11-order-stage.md` 참고) — 새 특수 타일이 없을 때까지 반복
7. **캐스케이드가 완전히 끝난 뒤(라운드가 계속되는 경우에 한해) 데드락(교환 가능한 매치가 하나도
   없는 상태) 여부를 확인한다(2026-08-03 추가)** — `DeadlockDetector.HasValidMove`가 false면
   `DeadlockDetector.Reshuffle`로 즉시 재배치하고, `SwapResult.WasReshuffled`를 통해
   Presentation(`Match3Controller`)에게 알려 보드를 다시 그리고 "매치 가능한 조합이 없어 보드를
   섞었어요!" 안내 문구를 잠깐 띄우게 한다(`PuzzleHud.ShowReshuffleNotice`). 게임 시작 시점의 초기
   배치도 동일하게 검사한다(§`02-board-tile.md`).
   - `HasValidMove`는 "교환 시 매치가 생기는지"뿐 아니라, **특수 타일과의 교환은 매치 여부와
     무관하게 항상 유효한 수로 취급한다**(`GridController.TrySwap`의 `activatesSpecial`과 동일한
     기준) — 그렇지 않으면 특수 타일이 있어서 실제로는 둘 수 있는데도 데드락으로 오판할 수 있다.

## 이벤트/상태 노출
- 점수는 `scoreChangedChannel`(IntEventChannel), 이동 횟수는 `movesChangedChannel`, 주문 진행도는
  `orderProgressChannel`(OrderProgressEventChannel)로 각 스텝마다 Raise된다
- 콤보 카운트 자체는 별도 이벤트가 없다 — `GridController.MaxCombo` 프로퍼티를 `Match3Controller`가
  직접 읽는다(§`04-score-combo.md`)

## 특수 타일 연동
- 매치 4개, 5개 이상, 또는 가로+세로 교차 매치는 캐스케이드 도중 특수 타일을 생성한다
- 특수 타일을 인접 타일과 교환하거나 단독으로 탭하면 즉시 활성화되며 이동 횟수를 소모한다
- 생성 규칙/활성화 효과/콤보 결합 규칙은 `12-special-tiles.md` 참고

## 비스코프
(특수 타일에 의한 광역 제거는 정식 스펙으로 편입됨 → `12-special-tiles.md` 참고)
