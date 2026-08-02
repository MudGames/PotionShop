# 8. 인트로 화면 (Title)

## 구성 (최소)
- 게임명 "Luna & Stella's Potion Shop" 텍스트
- "시작하기", "종료" 버튼 (클릭)
- 배경: 루나(Luna, 마녀)와 스텔라(Stella, 검은고양이) Idle 애니메이션 1컷 + 정적/그라디언트 배경

## 동작
- `Title` 상태에서 표시. 버튼 클릭 → `Playing`으로 전환(`05-move-limit-flow.md`)
- 결과 화면의 "다시 시작"은 **타이틀로 돌아가지 않는다** — 씬 전환 없이 퍼즐 씬(`Main.unity`) 안에서
  바로 `GameManager.ResetProgress()` 후 첫 스테이지로 보드를 재구성한다
  (`Match3Controller.OnRestartRequested`, 2026-08-03 확정). 씬 리로드보다 매끄럽고 빠름.

## 비스코프
- 설정 메뉴, 튜토리얼 애니메이션
