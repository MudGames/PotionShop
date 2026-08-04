using System.Collections;
using Puzzle.Core;
using UnityEngine;

// 얇은 Presentation 오케스트레이터: Puzzle.Core.GridController 인스턴스와 그 Presentation
// 협력 객체들 - BoardView(그리드 레이아웃/렌더링/애니메이션), TileController(클릭 -> 스왑
// 요청), PuzzleEffectController(연쇄 애니메이션 재생), PuzzleHud(점수/남은 이동/배너), 그리고
// PuzzleSidePanel(미션/주문 진행도, 보드 옆의 자체 패널) - 를 생성하고
// 서로 연결한다. 게임 규칙도, 레이아웃 계산도, 애니메이션 타이밍 로직도, UI 위젯 생성 로직도
// 이 클래스 자체에는 없다; 그런 것들은 이웃한 BoardView.cs/TileController.cs/
// PuzzleEffectController.cs/PuzzleHud.cs/PuzzleSidePanel.cs를 참고하고, 게임 규칙은
// Assets/Scripts/Puzzle/Core/를 참고할 것.
//
// 주문(order)이 완료되면 "다음 스테이지로" 버튼이 달린 Clear! 배너를 보여준다(PuzzleHud 참고).
// 이동 횟수 소진(게임 오버) 시점은 캠페인의 유일한 "끝"이라, 최종 점수 + 최고 콤보 + "다시 시작"
// 버튼이 있는 결과 화면을 보여준다 - 06-ui.md "결과 화면" 요건. 더 풍성한 것(스테이지별 요약/보상
// 화면)은 아직 미정이며, 제대로 설계될 때를 대비해 NoteController.cs는 사용하지 않는 채로 남겨
// 두었다.
//
// PuzzleHud/PuzzleSidePanel/Witch/GameManager를 서로 직접 참조하지 않도록 이벤트 채널로 묶는다
// (CLAUDE.md 이벤트 채널 아키텍처 원칙 참고): 점수/이동 횟수/주문 진행도처럼 캐스케이드 도중에도
// 안전하게 반영되는 값은 GridController가 직접 채널을 Raise하고, 각 구독자(PuzzleHud/
// PuzzleSidePanel/Witch/GameManager)는 그 채널만 알면 된다. 반면 클리어/게임오버/레벨 시작처럼
// "애니메이션이 끝난 뒤에" 혹은 "레벨이 막 구성된 뒤에"처럼 프레젠테이션 타이밍이 걸린 신호는
// 이 오케스트레이터가 알맞은 시점에 직접 Raise한다 - 이 타이밍 판단만큼은 GridController도,
// 각 구독자도 알 수 없는 정보이기 때문이다. 어떤 레벨을 다음으로 불러올지 결정하는 것(GameManager
// 조회/폴백)은 여전히 이 클래스가 맡는다 - 결과적으로 "다음 SetupLevel 호출"이 필요하므로.
public class Match3Controller : MonoBehaviour
{
    [SerializeField]
    private Transform buttonContainer;

    // 폴백 레벨. 씬에 GameManager가 없을 때만 사용된다(예: 이 씬을 단독으로 테스트하는 경우).
    // GameManager가 존재하면 GameManager.Instance.CurrentLevel이 우선한다.
    [SerializeField]
    private LevelData levelData;

    [Space]
    [SerializeField]
    private IntEventChannel scoreChangedChannel;
    [SerializeField]
    private IntEventChannel movesChangedChannel;
    [SerializeField]
    private OrderProgressEventChannel orderProgressChannel;
    [SerializeField]
    private VoidEventChannel orderClearedChannel;
    [SerializeField]
    private VoidEventChannel gameOverChannel;
    [SerializeField]
    private VoidEventChannel advanceRequestedChannel;
    [SerializeField]
    private VoidEventChannel restartRequestedChannel;
    [SerializeField]
    private VoidEventChannel levelStartedChannel;

    [Space]
    [SerializeField]
    private float cellSpacingRatio = 0.08f; // 셀 크기 대비 간격 비율

    // 스페셜 타일 표시(2026-08-04, 행/열/범위 3종으로 재정리 - 기존 컬러 폭탄은 활성화 시 배지가
    // 재료 아이콘을 가려버려 어떤 재료였는지 알 수 없는 문제로 제거). 셋 다 재료 아이콘을 완전히
    // 덮는 물약 배지로 표시되며 서로 다른 색이라 헷갈릴 일이 없다: 행 폭탄은 빨간 물약, 열 폭탄은
    // 초록 물약, 레이디우스 폭탄(주변 3x3, 5칸 이상 매치도 이걸 생성함)은 파란 물약.
    // TileView.RefreshSpecialEdges 참고.
    [SerializeField]
    private Sprite rowBombSprite;
    [SerializeField]
    private Sprite columnBombSprite;
    [SerializeField]
    private Sprite radiusBombSprite;

    [Space]
    [SerializeField]
    private float swapAnimationDuration = 0.15f;
    [SerializeField]
    private float clearAnimationDuration = 0.15f;
    [SerializeField]
    private float moveAnimationDuration = 0.2f;
    [SerializeField]
    private float stepPauseDuration = 0.05f;

    private LevelData _activeLevel;
    private GridController _logic;
    private BoardView _boardView;
    private TileController _input;
    private PuzzleEffectController _effects;
    private PuzzleHud _hud;
    private PuzzleSidePanel _sidePanel;
    private TutorialPanel _tutorial;
    private Coroutine _comboPopupCoroutine;

    // 창 리사이즈/화면 회전 등으로 이 GameObject(PuzzlePanel)의 RectTransform 크기가 바뀔 때마다
    // Unity가 호출한다. BoardView는 타일 크기를 Build() 시점에 한 번만 계산해두므로, 그대로 두면
    // 리사이즈에 반응하지 않는다 - 여기서 다시 계산하도록 알려준다. 보드가 아직 없으면(Start
    // 이전, 또는 레벨 전환 도중) BoardView.RefreshLayout이 조용히 무시한다.
    private void OnRectTransformDimensionsChange()
    {
        _boardView?.RefreshLayout();
    }

    private void Start()
    {
        LevelData initialLevel = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : null;
        if (initialLevel == null)
        {
            initialLevel = levelData;
        }

        if (initialLevel == null)
        {
            Debug.LogError("Match3Controller: no level available. Assign a levelData fallback, or add a GameManager with a StageSequence.");
            return;
        }

        _boardView = new BoardView(buttonContainer, cellSpacingRatio, rowBombSprite, columnBombSprite, radiusBombSprite);
        // 이 시점에는 _logic이 아직 존재하지 않는다(레벨별로 SetupLevel에서 생성됨) - 그래서
        // 이 predicate는 매 클릭마다 지연 평가로 읽는다. SetupLevel이 끝날 때까지 입력이
        // 비활성 상태로 유지되므로(아래 Reset/Enabled 참고) 이 방식은 안전하다.
        _boardView.CanAcceptSwap = (a, b) => _logic != null && _logic.WouldAcceptSwap(a, b);
        _input = new TileController(
            _boardView,
            cell => _logic != null && _logic.Board.Get(cell).Special != SpecialKind.None,
            cell => _logic != null && _logic.Board.Get(cell).IsBlocked);
        _effects = new PuzzleEffectController(
            this,
            _boardView,
            swapAnimationDuration,
            clearAnimationDuration,
            moveAnimationDuration,
            stepPauseDuration);

        // HUD 바는 화면 상단 전체를 가로지르고(transform.parent인 PuzzleCanvas에 부모로 연결),
        // 배너는 이 패널만 덮는다(transform인 PuzzlePanel에 부모로 연결) - PuzzleHud 참고.
        // PuzzleHud는 scoreChangedChannel/movesChangedChannel/orderClearedChannel을 스스로
        // 구독하고 advanceRequestedChannel/restartRequestedChannel을 스스로 Raise한다 - 이 클래스가
        // 직접 UpdateScore/ShowComplete를 호출하지 않는다. 단, ShowGameOver(최종 점수/최고 콤보)만은
        // 이 클래스가 타이밍에 맞춰 직접 호출한다(OnSwapPlaybackComplete 참고).
        _hud = new PuzzleHud(transform.parent, transform, scoreChangedChannel, movesChangedChannel, orderClearedChannel, advanceRequestedChannel, restartRequestedChannel);

        // 미션(주문 진행도)은 보드 옆의 자체 패널에 있으며, 마찬가지로 PuzzleCanvas
        // (transform.parent)에 부모로 연결되어 PuzzlePanel 자체의 로컬 스케일/장식과 무관하게
        // 위치가 유지된다 - PuzzleSidePanel 참고. orderProgressChannel도 스스로 구독한다.
        _sidePanel = new PuzzleSidePanel(transform.parent, orderProgressChannel);

        // 매치3 기본 규칙 + 특수 타일(물약) 3종 설명 - PuzzleSidePanel(미션 패널)과 대칭되는 보드
        // 왼쪽 자리에 놓이는 상시 참고용 패널이라, 모달처럼 열고 닫는 상태가 따로 없다.
        _tutorial = new TutorialPanel(transform.parent, rowBombSprite, columnBombSprite, radiusBombSprite);

        _input.SwapRequested += OnSwapRequested;
        _input.SpecialActivationRequested += OnSpecialActivationRequested;
        _input.TileSelected += OnTileSelected;

        advanceRequestedChannel.OnRaised += OnAdvanceRequested;
        restartRequestedChannel.OnRaised += OnRestartRequested;

        SetupLevel(initialLevel);
    }

    // 주어진 레벨에 대해 보드를 (다시) 구성하고 새 GridController를 연결한다. startingScore/
    // startingMaxCombo는 캠페인 전체 누적 값을 다음 스테이지로 이어가기 위한 것 - 새 스테이지라고
    // 0부터 다시 세지 않는다(OnAdvanceRequested 참고). "다시 시작"(OnRestartRequested)만 예외적으로
    // 0을 넘겨 캠페인을 처음부터 다시 센다.
    private void SetupLevel(LevelData level, int startingScore = 0, int startingMaxCombo = 0)
    {
        _activeLevel = level;

        // BoardView/PuzzleSidePanel은 재료의 스프라이트만 필요하므로, IngredientData 배열에서
        // 한 번만 뽑아 재사용한다.
        Sprite[] ingredientSprites = ExtractSprites(_activeLevel.ingredients);

        // 재료 아이콘은 이벤트가 아니라 레벨 설정값이므로, GridController가 생성자에서 초기
        // 주문 진행도를 Raise하기 전에 미리 넘겨둔다 - PuzzleSidePanel.SetIngredientSprites 참고.
        _sidePanel.SetIngredientSprites(ingredientSprites);

        // 미션 난이도(재료 종류 수/요구 개수)는 GameManager.CurrentStageNumber를 따라 올라간다 -
        // GameManager가 없는 단독 씬 테스트 환경에서는 기본값 1(가장 쉬운 난이도)로 생성된다.
        int stageNumber = GameManager.Instance != null ? GameManager.Instance.CurrentStageNumber : 1;
        _logic = new GridController(_activeLevel, new UnityRandomTileSource(), scoreChangedChannel, movesChangedChannel, orderProgressChannel, startingScore, startingMaxCombo, stageNumber);

        _boardView.Build(_activeLevel.rows, _activeLevel.columns, _logic.Board, ingredientSprites);

        _hud.SetMovesVisible(_logic.HasMoveLimit);

        // GameManager가 없는 상태(단독 씬 테스트)에서는 스테이지 개념 자체가 없으므로 표시하지 않는다.
        if (GameManager.Instance != null)
        {
            _hud.UpdateStageLabel(GameManager.Instance.CurrentStageNumber);
        }

        _input.Enabled = true;
        _boardView.InputEnabled = true;

        // 오프닝 비트: 클리어/게임 오버 반응과 마찬가지로, 마녀가 실제로 무언가를 요청하는 순간을
        // 준다 - 그렇지 않으면 주문이 존재한다는 유일한 단서가 HUD의 진행도 숫자뿐이게 된다.
        levelStartedChannel.Raise();
    }

    private void OnSwapRequested(GridCell a, GridCell b)
    {
        if (!_logic.CanAcceptSwaps)
        {
            return;
        }

        SwapResult result = _logic.TrySwap(a, b);

        _input.Enabled = false;
        _boardView.InputEnabled = false;
        _effects.Play(a, b, result, () => OnSwapPlaybackComplete(result));
    }

    private void OnTileSelected(GridCell cell)
    {
        AudioManager.Instance?.PlayTileSelect();
    }

    private void OnSpecialActivationRequested(GridCell cell)
    {
        if (!_logic.CanAcceptSwaps)
        {
            return;
        }

        SwapResult result = _logic.TryActivateSpecial(cell);

        _input.Enabled = false;
        _boardView.InputEnabled = false;
        _effects.PlayActivation(result, () => OnSwapPlaybackComplete(result));
    }

    // 플레이어가 연쇄 애니메이션이 끝나는 것을 실제로 본 다음에야 클리어/게임오버를 공개한다 -
    // GridController가 논리적으로 클리어/게임오버를 확정하는 시점(TrySwap 내부, 애니메이션 재생
    // 전)에 곧바로 Raise하면 화면이 애니메이션 중간에 잘려 나갈 것이다. 이 타이밍 판단은
    // GridController도 각 채널 구독자도 알 수 없으므로, 이 오케스트레이터가 직접 Raise한다.
    private void OnSwapPlaybackComplete(SwapResult result)
    {
        if (_logic.IsCleared)
        {
            AudioManager.Instance?.PlayRoundEnd();
            orderClearedChannel.Raise();
            return;
        }

        if (_logic.IsGameOver)
        {
            AudioManager.Instance?.PlayRoundEnd();
            _hud.ShowGameOver(_logic.Score, _logic.MaxCombo);
            gameOverChannel.Raise();
            return;
        }

        // 이번 액션에서 매치 스텝이 2번 이상 이어졌을 때만("콤보") 잠깐 팝업을 띄운다 - 1은 그냥
        // 매치 한 번일 뿐 콤보라 부를 게 없다(04-score-combo.md 콤보 정의 참고). 콤보 팝업 애니메이션
        // 재생 중에도 입력은 이미 다시 켜지므로(아래), 팝업이 끝나기 전에 다음 콤보가 또 발생하면
        // 이전 코루틴을 먼저 멈춰야 한다 - 안 그러면 두 코루틴이 같은 GameObject의 크기/텍스트/
        // 색상을 동시에 건드려 깜빡이거나 어긋나 보일 수 있다(2026-08-04 버그 픽스).
        if (result.Steps.Count >= 2)
        {
            if (_comboPopupCoroutine != null)
            {
                StopCoroutine(_comboPopupCoroutine);
            }

            _comboPopupCoroutine = StartCoroutine(_hud.AnimateComboPopup(result.Steps.Count));
        }

        // GridController가 캐스케이드 종료 후 데드락(교환 가능한 매치 없음)을 발견해 보드를 섞은
        // 경우, 방금 재생된 애니메이션의 마지막 스냅샷은 이미 낡은 상태이므로 지금 보드를 그대로
        // 다시 그려서 화면과 실제 데이터를 맞춘다. 아무 안내 없이 순간적으로 바뀌면 어색하므로,
        // 짧은 안내 문구를 잠깐 띄운다.
        if (result.WasReshuffled)
        {
            _boardView.RefreshAll(_logic.Board);
            _hud.ShowReshuffleNotice();
            StartCoroutine(HideReshuffleNoticeAfterDelay());
        }

        // 무조건 다시 활성화하지 않는다: 이 스왑의 연쇄가 방금 레벨을 클리어했거나 이동 제한을
        // 소진시켰다면, 위 분기에서 이미 입력을 비활성화했으므로 그 상태가 유지되어야 한다.
        _input.Enabled = _logic.CanAcceptSwaps;
        _boardView.InputEnabled = _logic.CanAcceptSwaps;
    }

    // 데드락 재셔플 안내 문구를 잠깐 보여준 뒤 자동으로 숨긴다 - 배너들과 달리 플레이어가 직접
    // 닫는 버튼이 없는 순수 안내용이라 시간이 지나면 스스로 사라져야 한다.
    private const float ReshuffleNoticeDuration = 1.5f;

    private IEnumerator HideReshuffleNoticeAfterDelay()
    {
        yield return new WaitForSeconds(ReshuffleNoticeDuration);
        _hud.HideReshuffleNotice();
    }


    private void OnAdvanceRequested()
    {
        // 다음 스테이지로 넘어가도 점수/최고 콤보는 초기화하지 않고 이어간다 - 캠페인 전체 누적
        // 값이지 스테이지 하나만의 값이 아니다.
        int carriedScore = _logic.Score;
        int carriedMaxCombo = _logic.MaxCombo;

        LevelData nextLevel = null;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AdvanceStage();
            nextLevel = GameManager.Instance.CurrentLevel;
        }

        // GameManager가 아예 없을 때와, GameManager는 있지만 CurrentLevel이 null로 돌아온 경우
        // (예: 할당되지 않았거나 잘못 설정된 StageSequence) 모두 인스펙터에 할당된 레벨로
        // 폴백한다 - Start()의 폴백과 동일한 방식이며, 어느 쪽이든 SetupLevel이 null 레벨로
        // 호출되어서는 안 되기 때문이다.
        if (nextLevel == null)
        {
            nextLevel = levelData;
        }

        if (nextLevel == null)
        {
            Debug.LogError("Match3Controller: no level available to advance to. Assign a levelData fallback, or fix GameManager's StageSequence.");
            return;
        }

        SetupLevel(nextLevel, carriedScore, carriedMaxCombo);
    }

    // 결과 화면의 "다시 시작" 버튼에서 호출된다 - 캠페인 진행도(GameManager)를 스테이지 1로
    // 되돌리고, 점수/최고 콤보도 0부터 다시 센다(OnAdvanceRequested와 달리 여기서는 값을 이어가지
    // 않는 게 의도된 동작이다).
    private void OnRestartRequested()
    {
        LevelData firstLevel = null;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetProgress();
            firstLevel = GameManager.Instance.CurrentLevel;
        }

        if (firstLevel == null)
        {
            firstLevel = levelData;
        }

        if (firstLevel == null)
        {
            Debug.LogError("Match3Controller: no level available to restart. Assign a levelData fallback, or fix GameManager's StageSequence.");
            return;
        }

        SetupLevel(firstLevel, 0, 0);
    }

    // BoardView/PuzzleSidePanel은 렌더링에만 관여하므로 IngredientData 자체를 몰라도 되고, 스프라이트만
    // 있으면 충분하다 - 레벨 (재)구성마다 한 번만 호출되므로 배열 할당은 매 프레임 GC 부담과 무관하다.
    private static Sprite[] ExtractSprites(IngredientData[] ingredients)
    {
        Sprite[] sprites = new Sprite[ingredients.Length];
        for (int i = 0; i < ingredients.Length; i++)
        {
            sprites[i] = ingredients[i] != null ? ingredients[i].sprite : null;
        }

        return sprites;
    }
}
