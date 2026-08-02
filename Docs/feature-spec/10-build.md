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

## 비스코프
- 빌드 자동화 스크립트/CI (한 번 빌드라 불필요)
- 코드 사이닝, 별도 모바일 APK (필요 시 선택 사항으로만 추가)
