# 10. 빌드 (Build / Delivery)

> 완성된 게임을 **WebGL**로 내보내 GitHub Pages에 배포. 배포 단계 — 개발 순서 7단계.

## 타깃
- 플랫폼: **WebGL** (PC/모바일 브라우저 모두 커버, 대회 규정상 .exe 제출 불가)
- 압축: `Player Settings > Publishing Settings > Compression Format` — **Brotli** 또는 **Gzip** 활성화 (초기 로딩 시간 단축)

## Build Settings (에디터)
- `File ▸ Build Settings ▸ Platform = WebGL`
- **Scenes In Build에 `Title.unity`, `Main.unity` 등록** (누락 시 빈 화면 — 가장 흔한 실수)
- 출력 폴더: 저장소 내 `docs/` (GitHub Pages 소스 폴더로 지정) 또는 별도 `gh-pages` 브랜치

## Player Settings (체크)
- Product Name: **Luna & Stella's Potion Shop** / Company Name / 아이콘
- 해상도: WebGL은 반응형 캔버스 권장 (Template 기본값 또는 커스텀 HTML)

## GitHub Pages 배포
1. 저장소 `Settings > Pages`에서 소스 브랜치/폴더 지정
2. 빌드 결과물 커밋 & push
3. `https://<유저명>.github.io/<저장소명>/`으로 접근 확인 (WebGL 빌드 실제 플레이 테스트 필수)

## 빌드 에러 처리
- 브라우저 콘솔(F12) 에러 로그를 Claude에 전달 — 흔한 원인: 압축 설정과 서버 MIME 타입 불일치, 씬 미등록, 누락 레퍼런스

## 모바일(iOS Safari) 대응 — 실제 겪은 문제와 수정 (2026-08-02)

GitHub Pages 배포 후 아이폰 Safari에서 순서대로 겪은 문제와 조치:

1. **페이지 자체가 안 열림(백지/에러)** → `Player Settings > WebGL > Maximum Memory Size`를
   2048MB → **512MB**로 축소. iOS Safari가 WASM 메모리를 2GB만큼 미리 예약하는 데 실패해
   로드 자체가 안 되는 것으로 추정(이 게임 규모엔 2GB가 과함).
2. **화면 비율이 다 깨짐** → Canvas가 Screen Space-Overlay라 카메라와 무관하게 기기 화면을
   그대로 채우는데, `PuzzlePanel`이 16:9 기준 퍼센트 앵커라 세로 화면에서 완전히 다른 모양의
   박스가 됨. `PuzzleCanvas`/`MenuCanvas` 바로 아래에 `AspectRatioFitter`(FitInParent, 16:9)
   래퍼를 추가하고 기존 최상위 UI를 그 밑으로 재부모화 — 어떤 화면비에서도 레터박스/필러박스로
   기존 구도 유지(코드 변경 없음, 씬 계층만 수정).
3. **세로 화면에서 보드가 너무 작음** → "가로로 돌려주세요" 안내, CSS `transform: rotate(90deg)`
   강제 가로 표시를 각각 시도했으나 **둘 다 실기기에서 실패**(전자는 캔버스 크기 0으로 로딩
   자체가 멈춤, 후자는 캔버스 합성이 깨져 소리만 나오고 화면이 안 보임) — 결론: **세로는 레터박스로
   작게라도 정상 표시, 가로가 기본**으로 방침 확정. `index.html`을 건드리는 방향/회전 관련 CSS
   트릭은 전부 되돌림(레터박스만 유지).
4. **소리는 나오는데 화면이 완전히 안 보임(로딩바도 안 보임)** → `Assets/Settings/UniversalRP.asset`의
   **HDR(`supportsHDR`)을 껐더니 해결**. HDR(float 프레임버퍼)이 일부 모바일 WebGL2 구현(iOS
   Safari)에서 렌더링 자체를 실패시키는 것으로 확인됨 — 픽셀아트 2D 게임이라 HDR이 시각적으로
   불필요해서 손실 없이 제거. **⚠️ 이후 절대 다시 켜지 말 것** — 모바일에서 재발 확인됨.
5. Player Settings의 **Resizable Window**도 활성화(모바일/다양한 창 크기에 캔버스가 맞춰 늘어나도록).

## 비스코프
- 빌드 자동화 스크립트/CI (한 번 빌드라 불필요)
- 코드 사이닝, 별도 모바일 APK (필요 시 선택 사항으로만 추가)
