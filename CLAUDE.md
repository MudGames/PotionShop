# CLAUDE.md - Luna & Stella's Potion Shop
이 파일은 Claude Code(claude.ai/code) 가 이 저장소의 코드를 다룰 때 참고할 수 있도록 안내합니다.

## 페르소나
유니티 게임을 같이 만드는 페어 프로그래머. 자세한 소통 방식은 아래 "응답·소통 톤" 참고.

## 응답·소통 톤
- 한국어로 답변
- 코드 수정 전, **의도를 한 줄로** 먼저 설명
- 명세서와 다른 길로 가게 되면 **차이를 먼저 알려주고** 진행 (조용히 바꾸지 말기)
- 모르는 건 추측하지 말고 "모르겠습니다"라고 말하고, 확인할 방법(공식 문서 링크, 테스트 방법 등)을 같이 제안하기
- **요청한 것만 만들기** — 안 쓸 유연성·설정·미래 대비 코드를 미리 넣지 말기
- **고치라는 것만 고치기** — 멀쩡히 잘 실행되는 인접 코드·서식은 건드리지 말고, 군더더기는 지우기 전에 먼저 알려주기

## 프로젝트 한 줄 요약
마녀와 검은고양이가 등장하는 Cozy 테마의 **싱글플레이 매치3(Match-3) 퍼즐 게임** — 인접한 포션 타일을 교환해 3개 이상 매치.

## 제출 대상: NHN AI Game Hackathon
- 제출 항목 5종 (하나라도 누락 시 심사 대상 제외):
  1. 플레이 가능한 빌드+소스 — GitHub Pages(WebGL) + 전체 소스, **Public 저장소 권장** (비공개 시 `dl_gameai_reviewer@nhn.com` 초대)
  2. 30~60초 플레이 영상 (실제 플레이만, 합성/편집 도용 금지) — YouTube
  3. 게임 소개 PDF (개요/플레이방법/실행방법/링크)
  4. **AI 활용 기술 문서 PDF** — 사용 AI 도구, 주요 프롬프트/지시 내역, 외부 에셋 출처·라이선스 명시
  5. 팀원 롤 기술서 (개인 참여이므로 생략)
- 개발 자체보다 **5개 제출물의 완전성이 최우선** — 미완성/누락 시 통째로 심사 제외되므로, 스코프를 넓히기보다 확실히 완성하는 쪽으로 판단할 것.
- AI 활용 기술 문서는 이 CLAUDE.md 자체와 Claude Code와의 대화 흐름이 핵심 근거 자료가 됨 — 개발 중 주요 프롬프트/의사결정을 기록해둘 것.

## 형상관리 (반드시 준수)
- **GitHub이 유일한 형상관리 시스템입니다.** 커밋 히스토리 자체가 포트폴리오 심사 근거이므로 기능 단위로 의미 있게 커밋합니다.
- Unity Version Control(`com.unity.collab-proxy`)은 제거 완료. GitHub 단일 형상관리 체계로 확정됨.
- Unity 프로젝트용 `.gitignore` 필수 항목: `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, `UserSettings/`, `.vs/`, `*.csproj`, `*.sln`, `*.slnx` (IDE가 자동 재생성)
- **저장소는 Public으로 시작합니다.** (해커톤 제출 요구사항 — 커밋 히스토리 자체가 AI 활용 문서의 근거 자료이므로 투명하게 유지)
- 바이너리 애셋(스프라이트, 오디오, aseprite/psd 원본)이 커지면 **Git LFS** 도입을 고려. 현재 스코프에서는 애셋 볼륨이 작을 것으로 예상되어 필수는 아니지만, PSD/aseprite 원본 파일이 누적되면 추적 대상에 추가.
- 원격 저장소 push 등 GitHub에 실제로 쓰기 작업을 하기 전에는 항상 확인받을 것 (push, force-push, 브랜치 삭제 등).

## 엔진/스택 (반드시 준수)
- Unity 6 LTS, URP 2D Renderer
- 보드: **그리드 기반 로직** (물리 엔진 미사용 — Rigidbody2D/Collider 불필요. 좌표는 순수 데이터(2차원 배열)로 관리)
- 타일 이동/애니메이션: DOTween 또는 Unity Animation(Coroutine 기반 Tween)으로 교환/낙하(cascade) 연출만 처리 (물리 시뮬레이션 아님)
- 데이터: ScriptableObject 기반 데이터 주도 설계
- 빌드 타깃: WebGL (배포 링크 필수, 항상 WebGL에서도 동작 검증)
- **네트워크 스택(NGO/Relay/Lobby) 미사용** — 싱글플레이 전용 프로젝트로 스코프 확정됨.

## 핵심 아키텍처 원칙 (이 프로젝트의 정체성 — 절대 어기지 말 것)
1. **보드 상태는 순수 데이터(그리드 배열)로 관리하고, 매치 판정은 결정론적 로직으로 처리한다.** 물리 엔진에 의존하지 않는다.
2. 그리드 크기와 타일 종류 수는 **하드코딩하지 않고 설정 가능한 값**으로 관리한다 (ScriptableObject 또는 상수 클래스로 분리). 이후 확장(그리드 확대, 타일 종류 추가)을 코드 수정 없이 값만 바꿔서 대응할 수 있어야 한다.
3. 시스템 간 결합은 이벤트 채널(ScriptableObject Event Channel 패턴)로 합니다. GameManager/ScoreManager/PoolManager/GridController가 서로 직접 참조하지 않습니다.
4. 타일 생성/소멸(매치 후 캐스케이드로 새 타일이 위에서 채워지는 연출 포함)은 반드시 `PoolManager` 경유. `new`/`Destroy` 남발 금지. 목표: 1분 플레이 GC Alloc 0.

## 스코프 (완성 기준 — 절대 넘기지 말 것)
- 싱글 플레이 1종만. 멀티플레이/관전/랭킹/채팅 등 추가 기능 금지.
- 그리드 크기는 6x6을 기본값으로 하되, 이후 확장 가능 (고정값 아님 — 하드코딩하지 말고 설정 가능한 값으로 둘 것).
- 타일 종류는 6종을 기본값으로 하되, 이후 추가 가능 (마찬가지로 하드코딩 금지).
- 라운드 종료 조건: **이동 횟수 제한** 방식 (타이머 아님). 기본값 20회, 이후 조정 가능 (하드코딩 금지 원칙 동일 적용).
- 결과 화면: 라운드 종료 시점 **최종 점수와 최고 콤보 수**를 표시.
- 캐릭터: 마녀, 검은고양이 2종만.
- 배경: 기본은 단색/그라디언트이나, 이후 추가·확장 가능.

## 코딩 컨벤션
- `PotionData : ScriptableObject` — 타일 종류별 색상/스프라이트/점수 참조 (등급/승급 개념 없음 — 매치3는 같은 타일 3개+ 매치이므로 "다음 등급" 필드 불필요)
- `TileController : MonoBehaviour` — 스왑 입력 처리 후 그리드 로직에 매치 판정 요청, 매치 성립 시 `MatchEvent` 발행 (직접 GameManager 호출 금지)
- `GridController` — 2차원 배열로 보드 상태 관리 (배열 크기는 설정값 참조), 매치 탐색(가로/세로 3개 이상), 캐스케이드(매치 후 낙하 채움) 로직 담당
- `PoolManager` — 타일/이펙트 오브젝트 풀 관리 전담. 다른 클래스는 직접 `Instantiate`/`Destroy` 하지 않고 `PoolManager`를 통해 Get/Return
- 이벤트 채널 예시: `OnTilesMatched(int tileType, int count)`, `OnScoreUpdated(int score)`, `OnComboTriggered(int comboCount)`, `OnMovesExhausted(int finalScore, int maxCombo)`

## 개발 순서
| 단계 | 내용 |
|---|---|
| 1 | 코어 루프 (그리드 초기화 → 스왑 → 매치 판정 → 캐스케이드 → 이동 횟수 소진 시 라운드 종료) |
| 2 | 오브젝트 풀링 + 이벤트 채널 아키텍처 |
| 3 | 그리드/타일 종류 설정값 분리 (하드코딩 제거, 확장 가능한 구조로 검증) |
| 4 | 결과 화면(최종 점수/최고 콤보) + UI 마무리 |
| 5 | 아트/애니메이션 적용 |
| 6 | 배경 추가·확장 (기본 단색/그라디언트 외 확장분 있다면 이 단계에서) |
| 7 | 최적화 (Profiler로 GC Alloc 검증) + WebGL 빌드 |
| 8 | 플레이 영상(30~60초) 촬영/편집, PDF 2종 작성(게임 소개, AI 활용 기술 문서), 저장소 Public 전환·정리 |
| 9 | **버퍼일** — 버그 픽스, 문서/영상 누락분 보완, 링크 접근 권한 재확인, 최종 리허설 플레이 |
| 10 | 제출 (신규 작업 없이 확인만 하는 것을 목표) |

## 문서화 요구사항
작업 완료 시마다 README에 "왜 이렇게 설계했는가"를 근거와 함께 남길 것. 특히 그리드/타일 종류를 하드코딩 대신 설정값으로 분리한 이유, 오브젝트 풀링으로 GC Alloc을 억제한 근거(Profiler 수치) 서술이 이 프로젝트의 핵심 어필 포인트입니다.

**해커톤 PDF 2종 관련**
- 외부 에셋(이미지·사운드·폰트 등)을 쓸 경우, 사용 즉시 출처·라이선스를 별도 메모에 남길 것 (나중에 몰아서 정리하면 누락 위험) — AI 활용 기술 문서에 그대로 옮겨 씀.
- Claude Code에게 준 주요 프롬프트/의사결정(예: 매치3 전환, 멀티플레이 스코프 제외 결정 등)을 단계별로 짧게 기록 — AI 활용 기술 문서 초안 재료로 사용.

## 프로젝트 실행
이 프로젝트는 커맨드라인으로 빌드 가능한 앱이 아니라 Unity 에디터 프로젝트이며, 별도의 빌드/린트/테스트 CLI 파이프라인이 구성되어 있지 않습니다. Claude Code를 실행 중인 Unity 에디터 인스턴스에 직접 연결해주는 **MCPForUnity** MCP 서버(`Packages/manifest.json`의 `com.coplaydev.unity-mcp`)를 통해 프로젝트와 상호작용하세요:
- 도구 호출 전에 `mcpforunity://instances`와 `mcpforunity://editor/state` 리소스를 확인하세요 — editor state 리소스는 `advice.ready_for_tools`와 차단 사유(컴파일 중, 도메인 리로드 등)를 알려줍니다.
- 빌드를 시도하는 대신 `manage_editor`(action `play`/`pause`/`stop`)로 Play 모드 진입/종료를 제어하세요.
- 스크립트 변경이나 도메인 리로드 후에는 `read_console`로 컴파일 에러 여부를 반드시 확인하세요.
- 테스트가 존재하게 되면 `run_tests` / `mcpforunity://tests` 리소스로 Unity Test Framework 테스트(`com.unity.test-framework` 설치됨)를 실행하세요.
- 세션 시작 시 `mcpforunity://custom-tools` 리소스를 확인하세요 — 표준 MCPForUnity 도구 외에 프로젝트 전용 커스텀 도구가 있다면 여기에 나열됩니다.
- 여러 Unity 인스턴스가 동시에 연결된 경우, 다른 도구/리소스를 사용하기 전에 `set_active_instance`(`Name@hash`)로 세션을 고정하세요.
- 에디터 관련 작업(씬, 게임오브젝트, 콘솔 등)은 파일을 직접 텍스트로 수정하기보다 Unity MCP 도구 사용을 우선 고려하세요 — 씬 파일(.unity)을 텍스트로 직접 편집하면 참조가 깨질 위험이 있습니다.
- `PotionShop.slnx`는 IDE(Rider/Visual Studio, `com.unity.ide.rider` / `com.unity.ide.visualstudio` 사용)용으로 생성된 C# 솔루션 파일입니다 — Unity가 자동 생성하는 파일이므로 직접 관리하지 않습니다.

## 폴더 구조 (반드시 준수 — 새 파일은 아래 규칙대로 배치)
```
Assets/
├── Scripts/
│   ├── Manager/         # GameManager, ScoreManager, PoolManager 등 매니저 클래스
│   ├── Puzzle/          # 매치 판정/캐스케이드 로직
│   ├── EventChannel/    # ScriptableObject Event Channel 정의/리스너
│   └── UI/              # 점수/결과 화면 UI 스크립트
├── ScriptableObjects/
│   └── PotionData/      # 타일 종류별 PotionData 에셋
├── Prefabs/
│   ├── Characters/      # 마녀, 검은고양이
│   └── VFX/             # 매치/캐스케이드 이펙트 등
├── Art/
│   ├── Sprites/
│   └── Animations/
├── Scenes/
│   ├── Menu.unity
│   └── Puzzle.unity
└── Settings/            # URP, Input Actions 등 (자동 생성분 포함)
```
- 이 구조 밖으로 스크립트를 놓아야 하는 예외 상황이 생기면, Claude Code는 조용히 새 폴더를 만들지 말고 먼저 제안하고 확인받을 것.
- 폴더명은 위 표기 그대로 사용 (대소문자/영문 통일 — 검색 편의).

## 패키지/파이프라인 참고사항
- 렌더 파이프라인: **URP**(Universal Render Pipeline), 2D 렌더러(`Assets/Settings/Renderer2D.asset`, `UniversalRP.asset`). **새 씬을 만들 때는 `Assets/Settings/Lit2DSceneTemplate.scenetemplate`를 기준으로 할 것.**
- 설치된 2D 패키지: animation, aseprite import, PSD import, sprite, spriteshape, tilemap(+ extras), 2D tooling — 매치3 그리드 렌더링에 tilemap 또는 스프라이트 그리드 배치를 활용.
- 입력: 신규 **Input System** 패키지(`com.unity.inputsystem`), `Assets/InputSystem_Actions.inputactions`로 구동. **레거시 Input Manager 금지 — 반드시 Input Action Asset을 통해 입력 처리.**