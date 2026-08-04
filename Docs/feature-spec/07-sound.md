# 7. 게임 사운드 (Sound)

> 원래는 폴리싱 단계(개발 순서 5단계 이후) 예정이었으나, WebGL 재배포에 맞춰 앞당겨 구현함.

## 구현 개요
BGM/SFX 전부 `Assets/Scripts/Managers/AudioManager.cs` 하나가 담당한다. `GameManager`와 동일한
`DontDestroyOnLoad` 싱글톤으로 메뉴 씬에 하나만 배치해두며, 메뉴에서 시작한 BGM이 퍼즐 씬으로
넘어가도 끊기지 않고, 퍼즐 씬의 여러 시점(타일 선택/매치/실패/캐스케이드/라운드 종료)에서도
`AudioManager.Instance`를 통해 같은 인스턴스로 SFX를 재생한다. `Match3Controller`/
`PuzzleEffectController`가 `AudioManager.Instance?.PlayXxx()` 형태로 직접 호출한다(별도 주입 없이
`GameManager.Instance`를 참조하는 것과 동일한 패턴) — 메뉴 씬을 거치지 않고 퍼즐 씬만 단독으로
테스트하는 경우에도 `?.`로 안전하게 무시된다.

## SFX (트리거 ↔ 구현 위치)
| 트리거 | 재생 시점 | 비고 |
|---|---|---|
| 타일 선택 | `TileController.TileSelected` → `Match3Controller.OnTileSelected` | 일반 타일 탭(드래그 아님) 시 |
| 교환 성공(매치) | `PuzzleEffectController.PlayCascadeRoutine` step 0 | 매치 개수별 피치 구분은 미구현(스펙상 선택 사항) |
| 교환 실패(매치 없음) | `PuzzleEffectController.PlayRoutine`, `!result.Accepted` | |
| 캐스케이드 연쇄 | `PuzzleEffectController.PlayCascadeRoutine` step 1+ | 전용 AudioSource에서 Stop 후 재생해 "겹치면 최근 것만" 만족 |
| 스테이지 종료(클리어/게임오버) | `Match3Controller.OnSwapPlaybackComplete` | 스테이지 클리어·게임 오버 둘 다 1회 재생 |

- `AudioManager`가 SFX용 `AudioSource`를 2개 추가로 소유한다: 일반 원샷용 1개(`PlayOneShot`),
  캐스케이드 전용 1개(겹치면 `Stop()` 후 재생)
- 클립이 비어있으면 조용히 무시된다 — 아직 모든 클립이 준비되지 않은 상태에서도 안전
- 매치(`matchClip`)와 캐스케이드(`cascadeClip`) 효과음은 둘 다 같은 클립
  `Assets/Audio/SFX/Match.ogg`(원본: `Casual Physics Puzzle BE6`(goldmetal.co.kr) 팩)를 재사용한다
  (2026-08-03 — 캐스케이드 전용 클립이 따로 준비되기 전까지의 임시 조치)
- `tileSelectClip`/`swapFailClip`/`roundEndClip`은 아직 비어있음(null) — 준비되는 대로 할당하고
  이 절을 갱신할 것

## BGM
- `bgmClips` 배열에 지정된 곡 중 `Awake()` 시점에 무작위로 하나를 골라 루프 재생
- 인게임/타이틀 화면 모두 같은 트랙(재생 시작 시 4곡 중 무작위 1곡)을 이어서 재생하는 방식 채택
  (별도 크로스페이드/전환 없음)

## 비스코프
- 다이나믹 믹싱, 음량 옵션 메뉴
- 매치 개수(3/4/5+)별 피치 변화(스펙에서도 선택 사항으로 명시됐던 부분, 시간상 생략)
