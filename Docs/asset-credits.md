# 외부 에셋 출처 & 라이선스 메모

> CLAUDE.md "문서화 요구사항" 참고 — 여기 기록한 내용은 그대로 AI 활용 기술 문서(PDF)의 "외부 에셋 출처·라이선스" 항목에 옮겨 쓴다.

## 1. Cozy Fantasy RPG Asset Pack

- **출처**: https://lfoxyy.itch.io/cozy-fantasy-asset-pack
- **제작자**: Foxy (lfoxyy)
- **가격**: 유료 (itch.io, $2.99 USD~)
- **라이선스**: itch.io 페이지에 명시적 라이선스 조항(상업적 이용 가능 여부/재배포 제한 등)이 공개되어 있지 않음 — **다운로드 시 동봉된 라이선스 파일/구매 확인 메일을 직접 확인 필요.** 확인 전까지는 "재배포 불가"로 간주하고 취급.
- **사용처** (프로젝트 내 경로):
  - `Assets/Art/Sprites/Characters/` — 캐릭터(Witch=루나, Black_Cat=스텔라) 스프라이트/애니메이션 프레임
  - `Assets/Art/Sprites/Decorations/` — Fluffy_Pink, Magical_Plants_Bushes, Swaying_Flowers
  - `Assets/Art/Sprites/Objects/` — Witch_Cottage, Magical_Forest_Tree, Magical_Market_Stall, Magical_Castle, Puffy_Canopy_Trees (배경 오브젝트, 아직 씬에 미배치)
  - `Assets/Art/Sprites/Tiles/Cozy_and_magical.png` — 매치3 타일(재료) 스프라이트 시트
  - `Assets/Art/Sprites/Shadow/Shadow_32x32.aseprite` — 캐릭터 그림자
  - `Assets/Art/Sprites/UI/elements/` (Button_1, Button_2, Frame_NPC.png, Menu.png, ColorBombBadge.png), `Assets/Art/Sprites/UI/UI.png` — UI 버튼/프레임/배지
- **저장소 처리**: 위 폴더 전체를 `.gitignore`로 제외 (원본 재배포 가능 여부가 불명확하므로 Public 저장소에 원본 파일을 올리지 않음). 로컬 디스크에는 유지되므로 에디터 작업/WebGL 빌드에는 영향 없음.
  - ⚠️ **주의**: 이 폴더들의 `.meta` 파일도 함께 제외했기 때문에, 다른 PC에서 저장소를 새로 clone한 뒤 이 에셋 팩을 다시 내려받아 같은 경로에 넣으면 Unity가 새 GUID를 발급한다. 이미 커밋된 `IngredientData`/`LevelData`/프리팹 등이 이 스프라이트를 GUID로 참조하고 있다면 참조가 깨질 수 있음 — 재설치 후 참조 재연결이 필요할 수 있다는 점을 감안할 것.

## 2. Kenney — Emotes Pack

- **출처**: https://kenney.nl/assets/emotes-pack
- **제작자**: Kenney
- **라이선스**: **CC0** (퍼블릭 도메인) — 상업적 이용/재배포/수정 모두 자유, 출처 표시 법적 의무 없음
- **사용처**: `Assets/Art/Sprites/Emotes/` (emote_*.png 다수) — 루나/스텔라 리액션 연출용으로 보임(현재 코드에서 직접 참조는 미확인, 5단계 아트 작업에서 사용 예정으로 추정)
- **저장소 처리**: CC0이므로 원본 그대로 Public 저장소에 유지 (제외 불필요)

## 3. Cainos — Pixel Art Icon Pack - RPG

- **출처**: https://assetstore.unity.com/packages/2d/gui/icons/pixel-art-icon-pack-rpg-158343
- **제작자**: Cainos
- **가격**: 무료 (Unity Asset Store)
- **라이선스**: Standard Unity Asset Store EULA — 완성된 게임/빌드에 포함해 배포하는 것은 허용되지만, **에셋 원본 파일 자체를 별도로 재배포(공개 소스 저장소에 원본 그대로 커밋하는 것 포함)하는 것은 EULA상 허용되지 않을 수 있음**
- **사용처**: **6종 재료(`IngredientData`) 아이콘 확정**(game-design.md §6 참고):
  - `Texture/Ore & Gem/Obsidian.png` — 흑요석 조각
  - `Texture/Food/Apple.png` — 빨간 사과
  - `Texture/Monster Part/Slime Gel.png` — 이끼 방울
  - `Texture/Food/Mushroom.png` — 달빛 버섯
  - `Texture/Ore & Gem/Crystal.png` — 마법 수정
  - `Texture/Monster Part/Feather.png` — 요정 깃털
  - 임시 플레이스홀더가 아니라 현재 스펙상의 확정 아이콘 — 5단계 아트 작업에서 자체 제작 아트로 교체할 수도, 그대로 갈 수도 있음(결정 시 여기 갱신).
  - **특수 타일(물약) 배지**(`Docs/feature-spec/12-special-tiles.md` 참고, `Match3Controller`의 `rowBombSprite`/`columnBombSprite`/`radiusBombSprite`, 2026-08-04 재설계): `Texture/Potion/Red Potion 3.png`(행 폭탄, 플레이어에게는 "빨간 물약"으로 표시), `Texture/Potion/Green Potion 3.png`(열 폭탄, "초록 물약"), `Texture/Potion/Blue Potion 3.png`(범위 폭탄, "파란 물약") — 기존 컬러 폭탄(ColorBomb)용이었던 Green Potion을 열 폭탄으로 재사용
- **저장소 처리**: 폴더 전체를 `.gitignore`로 제외. 위 1번과 동일한 GUID 참조 깨짐 주의사항 적용됨 — **`Assets/Data/Ingredients/*.asset`은 커밋되는데 정작 그 안이 참조하는 스프라이트 원본은 저장소에 없다는 뜻**이므로, 다른 PC에서 clone 후 바로 플레이하려면 Cainos 팩을 로컬에 재설치해야 함(재설치 시 동일 경로에 넣으면 기존 GUID와 일치해 참조가 유지됨 — 팩 자체를 다시 받는 것뿐이라 새 GUID 문제는 발생하지 않음. 팩을 아예 다른 경로/버전으로 받으면 깨짐).

## 4. Casual Physics Puzzle BE6 (SFX)

- **출처**: https://goldmetal.co.kr/ (무료 배포 에셋으로 받음 — 개별 게시물 링크/정확한 라이선스 조항은 미확인)
- **라이선스**: 확인 전 — goldmetal.co.kr은 게시물마다 이용 조건이 다를 수 있어, 정확한 라이선스 원문을
  확인하기 전까지는 "재배포 불가"로 간주하고 취급(Cozy Fantasy 팩과 동일한 처리 원칙)
- **사용처**: `Assets/Audio/SFX/Match.ogg` — 매치 성공 효과음(`AudioManager.matchClip`). 원본은 `Assets/Casual Physics Puzzle BE6/Audio/next.ogg`였으나, 실제 사용하는 클립만 `Assets/Audio/SFX/`로 이동
- **저장소 처리**: 팩 폴더 전체와 `Assets/Audio/`를 `.gitignore`로 제외(무료 에셋이지만 라이선스 미확인 상태이므로 보수적으로 처리)

## 5. Epic Soundtracks for Every Genre - free (BGM)

- **출처**: https://assetstore.unity.com/packages/audio/music/epic-soundtracks-for-every-genre-free-342720
- **제작자**: Level Up Creative Lab
- **가격**: 무료 (Unity Asset Store)
- **라이선스**: Standard Unity Asset Store EULA — Cainos와 동일하게, 완성된 빌드 포함 배포는 허용되지만 **원본 파일 자체를 공개 소스 저장소에 그대로 커밋하는 것은 EULA상 허용되지 않을 수 있음**
- **사용처**: `Assets/Audio/BGM/`의 4곡(Charming Village, Whimsical Woods, Cheerful Companions, Cozy Fireside Glow) — 메뉴~퍼즐 씬 전환에도 끊기지 않는 인게임 BGM으로 사용, 시작 시 4곡 중 무작위 선택(`Assets/Scripts/Managers/AudioManager.cs`). 원본은 `Assets/Epic Soundtracks for Every Genre - free/Casual Game Music Pack — Fun & Playful Background Loops/`였으나, 실제 사용하는 4곡만 `Assets/Audio/BGM/`으로 이동
- **저장소 처리**: 팩 폴더 전체와 `Assets/Audio/`를 `.gitignore`로 제외. 위 Cainos 항목과 동일한 GUID 참조 깨짐 주의사항 적용됨(재설치 시 같은 경로에 넣으면 GUID 유지됨).

## 6. Galmuri (갈무리) — 한글 픽셀 폰트

- **출처**: https://github.com/quiple/galmuri
- **제작자**: quiple
- **라이선스**: **SIL Open Font License 1.1** — 폰트 자체를 단독으로 판매하지만 않으면 사용/수정/재배포 자유(상업적 이용 포함)
- **사용처**: `Assets/Fonts/Galmuri11-Bold.ttf`, `Assets/Fonts/Galmuri11-Bold SDF.asset` (TextMeshPro용)
- **저장소 처리**: OFL이므로 원본 그대로 Public 저장소에 유지 (제외 불필요)

## 7. AI 생성 이미지 (에셋 팩 아님 — 직접 생성)

> 외부 에셋 라이선스가 아니라 **AI 활용 기술 문서의 "사용 AI 도구" 항목**에 그대로 옮겨 적을 것 (CLAUDE.md 제출 요건 §4).

- **`Assets/Art/Sprites/UI/Title.png`** — Google Gemini로 생성
- **`Assets/Art/Sprites/UI/Background.png`** — ChatGPT로 생성
- **`Assets/Art/Sprites/UI/Blackground/u1.jpg`~`u64.jpg`** — AI로 생성한 64프레임 애니메이션 시퀀스(별빛 반짝임/꽃잎 낙하/수면 파문). Menu/Main 씬 배경에서 `FlipbookAnimator`로 루프 재생(`06-ui.md` 참고)
- **저장소 처리**: 위 파일 모두 직접 생성한 원본이므로 재배포 제한 없음 — `.gitignore` 제외 대상 아님, Public 저장소에 그대로 유지
