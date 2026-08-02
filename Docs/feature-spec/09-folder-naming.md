# 9. 폴더 구조 & 씬 초기 구성

> 폴더 트리 SoT는 CLAUDE.md 본문(자동 로딩)과 동기화. 네이밍 규약은 `convention.md` §1.

```
Assets/
├── Scripts/
│   ├── Managers/        매니저 클래스
│   ├── Puzzle/          GridController, TileController, 매치/캐스케이드 로직
│   ├── EventChannels/   ScriptableObject Event Channel 정의/리스너
│   └── UI/              HUD, ResultScreen, TitleScreen 스크립트
├── ScriptableObjects/
│   └── IngredientData/  타일 종류별 데이터 에셋 6종
├── Prefabs/
│   ├── Characters/      루나(Luna, 마녀), 스텔라(Stella, 검은고양이)
│   └── VFX/             매치/캐스케이드 이펙트
├── Art/
│   ├── Sprites/         (Cozy Fantasy Asset Pack 원본은 ThirdParty/로 분리 권장)
│   └── Animations/
├── Scenes/
│   ├── Menu.unity
│   └── Main.unity
└── Settings/            URP, Input Actions (자동 생성분 포함)
```

## 씬 초기 구성 (Main.unity — 에디터에서 배치)
- **Board** (빈 GameObject, `GridController` 부착, 자식으로 타일 뷰 풀에서 생성된 타일들이 배치됨)
- **Main Camera** (Orthographic, 보드 전체가 화면에 들어오도록 size 조정)
- **Managers** (빈 GameObject 1개에 `GameManager` 부착)
- **Canvas** (HUD/결과/타이틀 컨테이너)
- **Characters** (루나, 스텔라(Stella, 검은고양이) — Idle 배치용, 게임플레이 로직과 분리)

## 외부 에셋 배치
- 구매 에셋(Cozy Fantasy RPG Asset Pack)은 `Assets/ThirdParty/CozyFantasyAssetPack/`에 원본 그대로 보관, 수정 최소화.
