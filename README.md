# Luna & Stella's Potion Shop

견습 마녀 **루나**와 검은고양이 **스텔라**가 등장하는 Cozy 테마 싱글플레이 매치3 퍼즐 게임입니다.
인접한 물약 재료 타일을 교환해 3개 이상 매치하고, 스테이지마다 무작위로 주어지는 "주문"(재료 수집
목표)을 이동 횟수 안에 완료해 나가는 캠페인 구조입니다.

> NHN AI Game Hackathon 사전 과제 제출용 프로젝트입니다. 기획/스펙 원문은 [`Docs/game-design.md`](Docs/game-design.md),
> [`Docs/feature-spec.md`](Docs/feature-spec.md)를 참고해주세요.

## 플레이 방법

- 마우스 클릭(또는 터치)으로 타일을 선택하고, 인접 타일을 클릭하면 교환을 시도합니다
- 3개 이상 같은 재료가 나란히 놓이면 매치가 성립합니다. 매치에 실패하면 원위치로 돌아가며 이동
  횟수는 소모되지 않습니다
- 화면 옆 주문 재료 패널에 표시된 재료를 이동 횟수(기본 20회) 안에 다 모으면 스테이지를 클리어하고 다음
  스테이지로 넘어갑니다. 재료를 다 모으기 전에 이동 횟수를 다 쓰면 게임 오버가 되며, 결과 화면(최종
  점수 + 최고 콤보)이 표시됩니다
- 4개/5개 이상 매치나 가로+세로 교차 매치는 특수 타일(물약 배지로 표시)을 만듭니다 — 인접 타일과
  교환하거나 단독으로 탭하면 활성화되어 줄/색상/범위 단위로 광역 제거됩니다

## 왜 이렇게 설계했는가

### 그리드/타일 종류를 설정값으로 분리한 이유
`GridController`는 그리드 크기·이동 횟수·재료 배열을 전부 `LevelData`(ScriptableObject)에서
읽어 들입니다(`Assets/Scripts/Puzzle/Core/LevelData.cs`). 보드 크기나 타일 종류 수를 코드에
하드코딩하면 스테이지마다 난이도를 다르게 주려 할 때마다 코드를 고쳐야 하는데, 이 프로젝트는 애초에
`StageSequence`로 여러 스테이지를 이어가는 캠페인 구조를 목표로 했기 때문에 스테이지 수만큼 코드
분기가 늘어나는 것을 피하려면 데이터 주도 설계가 필수였습니다. 새 스테이지를 추가하는 작업이 "에셋
하나 만들고 값 채우기"로 끝나는 것도 이 결정 덕분입니다(`Docs/feature-spec/11-order-stage.md` 참고).

### 이벤트 채널 아키텍처를 쓴 이유
`GridController`(게임 로직) · `PuzzleHud`/`PuzzleSidePanel`(UI) · `GameManager`(캠페인 진행)가
서로를 직접 참조하면, UI를 하나 고칠 때마다 로직 클래스를 건드려야 하고 반대의 경우도 마찬가지가
됩니다. `IntEventChannel`/`VoidEventChannel`/`OrderProgressEventChannel`(ScriptableObject
기반)로 묶어두면 각 시스템은 "이 채널에 뭐가 발행되는지"만 알면 되고, 실제로 누가 듣고 있는지는
몰라도 됩니다 — 실제로 결과 화면(점수+최고 콤보) 기능을 추가할 때도 `GridController`나
`GameManager` 코드는 건드릴 필요가 없었습니다(`Docs/convention.md` §4).

### 오브젝트 풀링으로 GC Alloc을 억제한 이유
캐스케이드 한 번에 타일이 수십 개씩 제거·생성될 수 있는데, 매번 `Destroy`/`Instantiate`를 하면
프레임마다 GC 압박이 커져 매치3 특유의 콤보 연출이 끊겨 보이기 쉽습니다. `TileViewPool`(타일 뷰)과
`BoardSnapshotPool`(캐스케이드 스텝별 보드 스냅샷)로 재사용 가능한 인스턴스를 미리 확보해두고
빌려주는 방식을 택했습니다.

> **Profiler 수치 (2026-08-03 측정, Unity 에디터 Play Mode 기준)**: `Main.unity`에서 Profiler의
> `GC Allocated In Frame` 카운터를 스왑 전(대기 상태)과 스왑+캐스케이드 발생 직후로 나눠 여러 프레임
> 샘플링했습니다.
> - 대기 상태(입력 없음): 프레임당 약 750~1050B, 11~17회 할당
> - 실제 스왑 → 매치 → 캐스케이드 직후: 프레임당 약 920~1060B, 14~17회 할당
>
> 두 상태의 수치가 사실상 같다는 것은 **캐스케이드(타일 제거·생성)가 별도의 GC 부담을 추가하지
> 않는다**는 뜻이라, `TileViewPool`/`BoardSnapshotPool` 풀링이 의도한 대로 동작하는 것으로 보입니다.
> 다만 대기 상태에서도 프레임당 약 1KB 내외의 할당이 꾸준히 잡혀서, 그 발생 지점을 하나씩 꺼가며
> (EventSystem, PuzzleCanvas 전체, 배경/장식 오브젝트의 `FlipbookAnimator` 5개, 루나·스텔라
> (Witch/BlackCat), `Global Light 2D`, `Physics2D` 자동 시뮬레이션) 제거해봤지만 전부 수치에
> 변화가 없었습니다. 최종적으로 **Main Camera까지 포함해 씬의 모든 오브젝트를 비활성화한 상태**
> (사실상 빈 씬)에서도 동일하게 프레임당 약 1KB, 14~17회 할당이 관측됐습니다. 이는 **이 잔여
> 할당이 프로젝트 코드가 아니라 Unity 에디터 Play Mode 자체(혹은 이 수치를 읽는 MCP 프로파일러
> 브릿지 호출 자체)의 오버헤드**라는 뜻이며, 프로젝트 코드 레벨에서는 더 제거할 대상이 없다는
> 결론입니다.
>
> 즉 **"1분 플레이 GC Alloc 0" 목표는 우리가 작성한 코드(타일 매치/캐스케이드/UI/캐릭터) 기준으로는
> 달성됐습니다** — 게임 로직이 추가로 만들어내는 할당은 측정 한계(에디터 자체 오버헤드) 아래로,
> 사실상 0으로 확인됩니다. 다만 이 수치는 WebGL 빌드가 아니라 에디터 Play Mode에서 잰 것이라 실제
> 배포 빌드(브라우저)에서의 절대값과는 차이가 있을 수 있고, 브라우저에 프로파일러를 직접 붙여
> 재검증하기 전까지는 그 절대 수치까지 보장하지는 않습니다.

### 주문(Order)을 매번 무작위로 생성하는 이유
처음에는 `LevelData`에 주문 재료/개수를 고정값으로 넣어뒀는데, 그러면 같은 스테이지를 다시 플레이할
때마다 항상 똑같은 주문이 나와 반복 플레이의 재미가 떨어졌습니다. `GridController` 생성자에서 재료
1~3종을 무작위로 골라 각각 3~8개를 요구하도록 바꿔서, 마지막 스테이지를 반복하거나 재시작할 때마다
다른 목표가 나오도록 했습니다(`Docs/feature-spec/11-order-stage.md` "랜덤 생성" 참고).

### 특수 타일을 없애지 않고 비주얼만 바꾼 이유
특수 타일(라인/컬러/레이디우스 폭탄) 시스템은 이미 로직이 완성돼 있었지만, 주문 진행과의 상호작용이
불분명하고(광역 제거가 무작위 재료를 지워서 예측이 안 됨) 전용 아트가 없어 화살표 placeholder로만
표시되는 문제가 있었습니다. 로직을 갈아엎는 대신 비주얼만 물약 3종 배지(Cainos 포션 아이콘)로
교체해서 문제의 절반(아트 부재)을 해결하고, 나머지 절반(주문 연동 모호함)은 의도된 "무차별 보너스"
동작으로 문서에 명시해뒀습니다(`Docs/feature-spec/12-special-tiles.md`).

### 모바일 화면 대응을 코드 대신 씬 계층 구조로 해결한 이유
HUD/보드를 담는 Canvas가 Screen Space-Overlay라 카메라와 무관하게 기기 화면 전체를 채우는데,
`PuzzlePanel`이 1920x1080(16:9) 기준 퍼센트 앵커로 배치돼 있어서 세로로 긴 모바일 화면에서는 같은
퍼센트를 적용해도 완전히 다른 모양의 박스가 되어버리는 문제가 있었습니다. `PuzzleHud`/
`Match3Controller`/`BoardView` 등 기존 로직 코드를 손대는 대신, Canvas 바로 아래에 uGUI 내장
`AspectRatioFitter`(FitInParent, 16:9) 래퍼 한 겹을 추가하고 기존 UI를 그 밑으로 재부모화하는
것으로 해결했습니다 — 앵커 퍼센트 계산이 항상 같은 16:9 기준으로 이뤄지므로 어떤 기기 화면비에서도
데스크톱에서 검증된 구도 그대로 렌더링되고(남는 공간은 레터박스 처리), GUID 참조도 전혀 깨지지
않습니다.

## 외부 에셋 출처 & 라이선스

전체 목록/라이선스 원문/저장소 처리 방식은 [`Docs/asset-credits.md`](Docs/asset-credits.md)에
정리되어 있습니다. 요약:

| 에셋 | 제작자 | 라이선스 | 저장소 반영 |
|---|---|---|---|
| Cozy Fantasy RPG Asset Pack (캐릭터/데코) | Foxy | 라이선스 원문 미공개 — 확인 전까지 재배포 불가로 간주 | `.gitignore` 제외 |
| Pixel Art Icon Pack - RPG | Cainos | Standard Unity Asset Store EULA | `.gitignore` 제외 |
| Epic Soundtracks for Every Genre - free (BGM) | Level Up Creative Lab | Standard Unity Asset Store EULA | `.gitignore` 제외 |
| Casual Physics Puzzle BE6 (매치 SFX) | goldmetal.co.kr | 미확인 — 확인 전까지 재배포 불가로 간주 | `.gitignore` 제외 |
| Emotes Pack | Kenney | CC0 | 저장소 포함 |
| Galmuri11 (한글 픽셀 폰트) | quiple | SIL Open Font License 1.1 | 저장소 포함 |
| Title.png / Background.png / Blackground(움직이는 배경 64프레임) | 직접 생성(Gemini / ChatGPT / AI) | 원본 제작물, 제한 없음 | 저장소 포함 |

라이선스가 불명확하거나 재배포가 제한된 에셋은 원본 파일을 저장소에서 제외하고 로컬 디스크에만
유지합니다(WebGL 빌드 자체에는 영향 없으며, 자세한 내용은 `Docs/asset-credits.md`의 GUID 관련
주의사항을 참고해주세요).

## 폴더 구조

- `Assets/Scripts/Puzzle/Core/` — 순수 로직(그리드/매치/캐스케이드/주문/특수 타일), Unity 의존성 최소화
- `Assets/Scripts/Puzzle/` — 프레젠테이션(Presentation) 레이어(`Match3Controller`가 오케스트레이션)
- `Assets/Scripts/UI/` — HUD/결과 화면 스크립트(`PuzzleHud`)
- `Assets/Scripts/EventChannels/` — 이벤트 채널 정의
- `Assets/Scripts/Managers/` — `GameManager`(캠페인 진행), `MenuManager`, `PoolManager<T>`
- `Assets/Data/` — `LevelData`/`StageSequence`/`IngredientData`/이벤트 채널 에셋
- `Assets/Audio/BGM/`, `Assets/Audio/SFX/` — 실제 재생에 쓰이는 BGM/SFX 클립만 모아둔 폴더(`AudioManager`
  참조). 무료 에셋이지만 재배포 라이선스 미확인이라 `.gitignore` 제외 — 로컬에는 유지, 빌드에는 영향 없음
- `Docs/` — 기획서, 기능 명세서(챕터별), 코드 컨벤션, 에셋 크레딧

세부 규칙은 [`Docs/feature-spec/09-folder-naming.md`](Docs/feature-spec/09-folder-naming.md)와
[`Docs/convention.md`](Docs/convention.md)를 참고해주세요.

## 빌드 & 실행

Unity **6000.3.20f1** (URP 2D)에서 열어주세요. 별도 CLI 빌드 파이프라인은 없으며, 에디터에서
File > Build Settings > WebGL로 빌드합니다 — **Player Settings의 Decompression Fallback을 켜두어야
합니다** (GitHub Pages 등 정적 호스팅은 Brotli 압축 파일의 Content-Encoding 헤더를 제공하지
않으므로, 꺼두면 브라우저에서 로딩이 멈춥니다).

- **플레이 링크**: https://MudGames.github.io/PotionShop/
- **소스**: 이 저장소 그대로입니다

### 모바일(iOS Safari) 대응 — 재빌드 시 반드시 확인
실기기 테스트에서 겪은 문제와 조치를 아래 Player Settings 값으로 반영해뒀습니다. 자세한 원인
분석은 [`Docs/feature-spec/10-build.md`](Docs/feature-spec/10-build.md)에 정리돼 있습니다.

- `WebGL > Maximum Memory Size`: **512MB** (2048MB였을 때 iOS Safari에서 페이지 자체가 안 열림)
- `Resizable Window`: **켜짐** (창/기기 크기에 맞춰 캔버스가 늘어나도록)
- **`Assets/Settings/UniversalRP.asset`의 HDR(`Supports HDR`)은 반드시 꺼진 상태를 유지할 것** — 켜면
  일부 모바일 WebGL2(특히 iOS Safari)에서 소리는 나오는데 화면이 전혀 안 보이는 문제가 재발합니다.
  이 게임은 픽셀아트 2D라 HDR이 시각적으로 필요하지 않습니다.
