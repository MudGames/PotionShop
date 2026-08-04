using System;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle.Core
{
    // "게임 상태 머신": Board를 소유하며 swap -> match -> 스페셜 생성(special-spawn) ->
    // clear -> gravity/refill -> 재검출(cascade) 과정을 조율하고, 주문 진행 상황과 이동 횟수를 추적한다.
    // 완전히 동기적으로 동작하며 프레임/코루틴/애니메이션 개념이 전혀 없다 - Presentation이 반환된
    // SwapResult.Steps를 바탕으로 진행 속도를 결정한다.
    public sealed class GridController
    {
        private readonly int _typeCount;
        private readonly int _moveLimit; // 0 = 무제한
        private readonly ITileRandomSource _random;
        private readonly BoardSnapshotPool _snapshotPool = new BoardSnapshotPool();

        // 주문 추적: _requirements와 _collected는 같은 인덱스를 공유하며, _typeIndexToRequirementIndex는
        // 팔레트 색상을 해당 요구사항의 인덱스에 매핑한다 (레벨에서 동일한 typeIndex가 두 번 등장하는 경우
        // 먼저 매칭된 것이 우선한다).
        private readonly OrderRequirement[] _requirements;
        private readonly int[] _collected;
        private readonly Dictionary<int, int> _typeIndexToRequirementIndex;

        // 타일 하나를 제거할 때 얻는 점수 (아래의 cascade 깊이 배수가 적용되기 전 기본값).
        private const int PointsPerTile = 10;

        // 랜덤 주문(미션) 생성 범위 - 스테이지(마지막 스테이지 반복 포함)를 시작할 때마다 매번
        // 새로 뽑는다. LevelData.orderRequirements는 더 이상 읽지 않는다(아래 생성자 참고).
        // 아래 값들은 1스테이지 기준 난이도이며, GenerateRandomRequirements가 stageNumber에 따라
        // 점점 어렵게(재료 종류 수/요구 개수 모두) 조정한다 - 2026-08-04, "미션이 스테이지와 무관하게
        // 항상 랜덤이라 난이도 곡선이 없다"는 피드백으로 추가.
        private const int MinMissionIngredientTypes = 1;
        private const int MaxMissionIngredientTypes = 3; // inclusive
        private const int MinRequirementCount = 3;
        private const int MaxRequirementCount = 8; // inclusive

        // 재료 종류 수 상한이 한 단계 늘어나기까지 걸리는 스테이지 수 (예: 3이면 1~3스테이지는
        // 최대 1종, 4~6스테이지는 최대 2종, 7스테이지부터 최대 3종 - MaxMissionIngredientTypes에서 상한).
        private const int StagesPerExtraIngredientType = 3;

        // 스테이지 1당 요구 개수(최소/최대 모두)가 늘어나는 폭과, 무한정 어려워지지 않도록 두는 상한.
        private const int RequirementCountPerStage = 1;
        private const int MaxRequirementCountCap = 20;

        public Board Board { get; }
        public int MovesUsed { get; private set; }
        public int Score { get; private set; }

        // 콤보 = 1회 교환(또는 특수 타일 활성화)으로 시작된 캐스케이드 안에서 발생한 연쇄 매치
        // 횟수(steps.Count) - ResolveCascadeAndFinish 참고. MaxCombo는 이번 세션 중 발생한 콤보의
        // 최댓값이며, Score와 마찬가지로 스테이지가 바뀌어도 초기화되지 않고 이어진다
        // (Match3Controller.OnAdvanceRequested 참고).
        public int MaxCombo { get; private set; }
        public bool IsCleared { get; private set; }
        public bool IsGameOver { get; private set; }

        // 매 cascade 스텝마다 다시 빌드되는 스냅샷 - Board와 달리 계속 변경되지 않으므로 Presentation이
        // 자신의 속도에 맞춰 붙잡고 읽어도 안전하다.
        public IReadOnlyList<OrderProgressEntry> OrderProgress { get; private set; }

        public int MovesRemaining => _moveLimit <= 0 ? int.MaxValue : Mathf.Max(0, _moveLimit - MovesUsed);
        public bool HasMoveLimit => _moveLimit > 0;

        // 레벨이 클리어되거나 게임오버가 되면 더 이상 swap을 받지 않는다 - 목표 점수를 넘긴 뒤에도
        // 보드가 계속 플레이 가능한 상태로 남아있던 프로젝트의 기존 알려진 한계를 이 부분에서 수정했다.
        public bool CanAcceptSwaps => !IsCleared && !IsGameOver;

        // 점수/이동 횟수/주문 진행도는 cascade 도중에도 안전하게 실시간으로 반영해야 하는 값이라
        // GridController가 직접 이 채널들을 Raise한다. 반면 클리어/게임오버는 애니메이션이 끝난
        // 뒤에야 배너/리액션으로 드러나야 하므로(연출 타이밍은 Presentation의 책임) 이벤트 채널
        // 대신 IsCleared/IsGameOver 프로퍼티로만 노출하고, Match3Controller가 애니메이션 완료
        // 시점에 맞춰 직접 OrderClearedChannel/GameOverChannel을 Raise한다.
        private readonly IntEventChannel _scoreChangedChannel;
        private readonly IntEventChannel _movesChangedChannel;
        private readonly OrderProgressEventChannel _orderProgressChannel;

        public GridController(
            LevelData level,
            ITileRandomSource random,
            IntEventChannel scoreChangedChannel,
            IntEventChannel movesChangedChannel,
            OrderProgressEventChannel orderProgressChannel,
            int startingScore = 0,
            int startingMaxCombo = 0,
            int stageNumber = 1)
        {
            _typeCount = level.ingredients.Length;
            _moveLimit = level.moveLimit;
            _random = random;
            _scoreChangedChannel = scoreChangedChannel;
            _movesChangedChannel = movesChangedChannel;
            _orderProgressChannel = orderProgressChannel;
            Board = BoardInitializer.CreateInitialBoard(level.rows, level.columns, _typeCount, level.blockedCells, random);

            // BoardInitializer는 "시작부터 매치가 있으면 안 된다"만 보장할 뿐, "교환 가능한 수가
            // 하나라도 있는지"는 보장하지 않는다 - 운이 나쁘면 첫 수조차 못 두는 채로 게임이 시작될
            // 수 있으므로, ResolveCascadeAndFinish와 동일한 검사를 시작 시점에도 한 번 해준다.
            if (!DeadlockDetector.HasValidMove(Board))
            {
                DeadlockDetector.Reshuffle(Board, random);
            }

            // 다음 스테이지로 넘어갈 때 이전 스테이지의 점수/최고 콤보를 이어받기 위한 시작값 -
            // 캠페인 전체 누적 값이지, 이 스테이지 하나만의 값이 아니다(Match3Controller.OnAdvanceRequested 참고).
            Score = startingScore;
            MaxCombo = startingMaxCombo;

            // 미션은 LevelData에 고정된 값을 쓰는 대신 매번 무작위로 생성한다 - 재료 종류 수/요구
            // 개수 범위 모두 stageNumber가 높을수록 늘어난다(위 상수 및 GenerateRandomRequirements
            // 참고). LevelData.orderRequirements는 더 이상 읽지 않는다(사용 안 함 - 2026-08-02 결정).
            _requirements = GenerateRandomRequirements(_typeCount, stageNumber, random);
            _collected = new int[_requirements.Length];
            _typeIndexToRequirementIndex = new Dictionary<int, int>();
            for (int i = 0; i < _requirements.Length; i++)
            {
                if (!_typeIndexToRequirementIndex.ContainsKey(_requirements[i].typeIndex))
                {
                    _typeIndexToRequirementIndex[_requirements[i].typeIndex] = i;
                }
            }

            OrderProgress = BuildOrderProgress();

            // 구독자(PuzzleHud/PuzzleSidePanel)는 이 시점에 이미 채널을 구독해둔 상태여야 한다 -
            // Match3Controller.SetupLevel이 이 생성자를 호출하기 전에 구독을 마쳐두는 구조다.
            // 이렇게 초기값을 한 번 Raise해두면, 이전 레벨의 값이 새 레벨 화면에 잠깐 남아있는
            // 문제 없이 항상 현재 레벨의 실제 상태로 시작한다.
            _scoreChangedChannel.Raise(Score);
            _movesChangedChannel.Raise(MovesRemaining);
            _orderProgressChannel.Raise(OrderProgress);
        }

        // 재료 1~3종(typeCount보다 많이 고를 수는 없다)을 중복 없이 무작위로 골라, 각각 일정 개수
        // 수집을 요구하는 미션을 만든다. typeCount가 0이면 빈 배열을 반환한다 - 그러면
        // ResolveCascadeAndFinish의 "_requirements.Length > 0" 가드에 의해 자연히 절대 클리어되지
        // 않는 기존 안전장치와 동일하게 동작한다.
        //
        // stageNumber가 높을수록 두 축 모두 어려워진다: (1) 재료 종류 수의 상한이
        // StagesPerExtraIngredientType마다 1씩 늘어나 MaxMissionIngredientTypes에서 멈추고,
        // (2) 요구 개수의 최소/최대가 스테이지당 RequirementCountPerStage만큼 늘어나되
        // MaxRequirementCountCap에서 멈춘다(마지막 스테이지를 반복 플레이해도 난이도가 무한정
        // 오르지 않도록). stageNumber=1이면 기존과 동일한 난이도(3~8개, 최대 3종)로 시작한다.
        private static OrderRequirement[] GenerateRandomRequirements(int typeCount, int stageNumber, ITileRandomSource random)
        {
            int difficultyStep = Mathf.Max(0, stageNumber - 1);

            int maxIngredientTypes = Mathf.Min(
                MaxMissionIngredientTypes,
                MinMissionIngredientTypes + difficultyStep / StagesPerExtraIngredientType);

            int requirementTypeCount = Mathf.Min(
                random.NextInRange(MinMissionIngredientTypes, maxIngredientTypes + 1),
                typeCount);

            int minRequirementCount = Mathf.Min(MinRequirementCount + difficultyStep * RequirementCountPerStage, MaxRequirementCountCap);
            int maxRequirementCount = Mathf.Min(MaxRequirementCount + difficultyStep * RequirementCountPerStage, MaxRequirementCountCap);

            HashSet<int> chosenTypes = new HashSet<int>();
            while (chosenTypes.Count < requirementTypeCount)
            {
                chosenTypes.Add(random.NextTypeIndex(typeCount));
            }

            OrderRequirement[] requirements = new OrderRequirement[requirementTypeCount];
            int i = 0;
            foreach (int typeIndex in chosenTypes)
            {
                requirements[i] = new OrderRequirement
                {
                    typeIndex = typeIndex,
                    requiredCount = random.NextInRange(minRequirementCount, maxRequirementCount + 1)
                };
                i++;
            }

            return requirements;
        }

        // match를 만드는 swap만 이동 횟수를 소모한다 (전형적인 매치3 UX이며, 되돌려진 swap은 무료다).
        // 예외: 스페셜 타일을 이웃 타일과 swap하면 그 swap 자체가 새로운 match를 만들든 안 만들든
        // 항상 스페셜 타일이 활성화된다 - 이는 스페셜 타일에 대한 표준적인 매치3 UX다.
        public SwapResult TrySwap(GridCell a, GridCell b)
        {
            if (!CanAcceptSwaps || !IsValidSwap(a, b))
            {
                return SwapResult.Rejected;
            }

            SpecialKind aKind = Board.Get(a).Special;
            SpecialKind bKind = Board.Get(b).Special;
            bool aWasSpecial = aKind != SpecialKind.None;
            bool bWasSpecial = bKind != SpecialKind.None;
            bool activatesSpecial = aWasSpecial || bWasSpecial;

            Board.Swap(a, b);

            List<MatchRun> runs = MatchDetector.FindRuns(Board);
            if (runs.Count == 0 && !activatesSpecial)
            {
                Board.Swap(a, b);
                return SwapResult.Rejected;
            }

            MovesUsed++;
            _movesChangedChannel.Raise(MovesRemaining);

            // swap 이후, 이동한 스페셜 타일(들)은 이제 쌍의 반대쪽 셀에 위치하게 된다.
            HashSet<GridCell> forcedDetonationSeed = null;
            if (activatesSpecial)
            {
                forcedDetonationSeed = new HashSet<GridCell>();

                // 두 개의 스페셜 타일을 의도적으로 서로 swap하면 각각 단독으로 터지는 것보다 더 강력한 효과로
                // 결합된다 - SpecialTileResolver.ComboCells 참고.
                if (aWasSpecial && bWasSpecial)
                {
                    foreach (GridCell cell in SpecialTileResolver.ComboCells(Board, b, aKind, a, bKind))
                    {
                        forcedDetonationSeed.Add(cell);
                    }

                    forcedDetonationSeed.Add(a);
                    forcedDetonationSeed.Add(b);
                }
                else
                {
                    if (aWasSpecial)
                    {
                        forcedDetonationSeed.Add(b);
                    }

                    if (bWasSpecial)
                    {
                        forcedDetonationSeed.Add(a);
                    }
                }
            }

            return ResolveCascadeAndFinish(runs, forcedDetonationSeed);
        }

        // TrySwap과 똑같은 "성사되는가" 판정을 실제로 이동 횟수를 소모하거나 캐스케이드를 진행하지
        // 않고 미리 물어본다(Board.Swap으로 임시로 바꿔본 뒤 바로 되돌리는 dry-run) - 드래그
        // 미리보기가 스왑이 한 칸만큼 완전히 밀렸는데도 매치가 안 만들어질 걸 알면서 계속 손을
        // 잡고 있게 두지 않고 즉시 드롭시키기 위해 쓰인다(BoardView.CanAcceptSwap 참고).
        public bool WouldAcceptSwap(GridCell a, GridCell b)
        {
            if (!CanAcceptSwaps || !IsValidSwap(a, b))
            {
                return false;
            }

            bool activatesSpecial = Board.Get(a).Special != SpecialKind.None || Board.Get(b).Special != SpecialKind.None;

            Board.Swap(a, b);
            List<MatchRun> runs = MatchDetector.FindRuns(Board);
            Board.Swap(a, b);

            return runs.Count > 0 || activatesSpecial;
        }

        // 스페셜 타일을 다른 타일과 swap하지 않고 단독으로 탭하면, 이웃으로 이동시킬 필요 없이 그 자리에서
        // 바로 터진다 - 이 역시 match를 만드는 swap과 마찬가지로 이동 횟수를 소모하는데, 플레이어의 의도적인
        // 행동이라는 점에서 동등하기 때문이다 (TileController/Match3Controller 참고).
        public SwapResult TryActivateSpecial(GridCell cell)
        {
            if (!CanAcceptSwaps || !Board.InBounds(cell) || Board.Get(cell).IsBlocked)
            {
                return SwapResult.Rejected;
            }

            if (Board.Get(cell).Special == SpecialKind.None)
            {
                return SwapResult.Rejected;
            }

            MovesUsed++;
            _movesChangedChannel.Raise(MovesRemaining);

            HashSet<GridCell> forcedDetonationSeed = new HashSet<GridCell> { cell };
            return ResolveCascadeAndFinish(new List<MatchRun>(), forcedDetonationSeed);
        }

        // TrySwap과 TryActivateSpecial 양쪽이 공유하는 cascade 처리 마무리 단계로, 각 메서드가 이미
        // 행동을 검증하고 이동 횟수를 소모한 이후에 호출된다: 더 이상 아무것도 트리거되지 않을 때까지
        // ResolveOneStep을 실행한 다음, 주문 완료(order-complete)/게임오버 여부를 확인한다.
        private SwapResult ResolveCascadeAndFinish(List<MatchRun> runs, HashSet<GridCell> forcedDetonationSeed)
        {
            // 이번 액션의 cascade 도중 찍히는 스냅샷들은 매번 동일한 풀링된 Board 인스턴스를 재사용한다 -
            // 호출자(Presentation)가 다음 액션을 트리거하기 전에 하나의 SwapResult의 Steps를 완전히
            // 소비하기 때문에 안전하다. BoardSnapshotPool의 사용 계약을 참고.
            _snapshotPool.ReturnAll();

            List<CascadeStepInfo> steps = new List<CascadeStepInfo>();
            int guard = Board.Rows * Board.Columns;
            int stepIndex = 0;
            bool hasWorkToDo = runs.Count > 0 || forcedDetonationSeed != null;

            while (hasWorkToDo)
            {
                if (stepIndex >= guard)
                {
                    Debug.LogWarning("GridController cascade exceeded its safety cap - stopping early. This should be unreachable; investigate if seen.");
                    break;
                }

                CascadeStepInfo step = ResolveOneStep(stepIndex, runs, forcedDetonationSeed);
                steps.Add(step);

                OrderProgress = BuildOrderProgress();
                _orderProgressChannel.Raise(OrderProgress);

                Score += step.PointsAwarded;
                _scoreChangedChannel.Raise(Score);

                stepIndex++;
                forcedDetonationSeed = null; // 이번 액션의 첫 스텝에서만 강제된다
                runs = MatchDetector.FindRuns(Board);
                hasWorkToDo = runs.Count > 0;

                // 일반 캐스케이드가 다 가라앉았는데 마침 주문이 완료됐다면, 클리어를 확정하기 전에
                // 보드에 남아있는 물약(스페셜 타일)들을 하나씩 강제로 터뜨려 보너스 점수를 더 준다 -
                // "미션 클리어 시 남은 물약 자동 활성화" 요구사항. 한 번에 하나씩만 시드해 매번 새로운
                // CascadeStepInfo 스텝으로 처리되므로, Presentation은 코드 변경 없이 기존
                // PlayCascadeRoutine으로 이 스텝들까지 순서대로 재생한 뒤에야(모든 물약이 터지는
                // 연출이 끝난 뒤에야) OnSwapPlaybackComplete에서 IsCleared를 확인해 클리어 배너를
                // 띄우게 된다.
                if (!hasWorkToDo && _requirements.Length > 0 && IsOrderComplete())
                {
                    GridCell? remainingSpecial = FindFirstSpecialCell();
                    if (remainingSpecial.HasValue)
                    {
                        forcedDetonationSeed = new HashSet<GridCell> { remainingSpecial.Value };
                        hasWorkToDo = true;
                    }
                }
            }

            // 콤보 카운트 = 이번 액션에서 발생한 캐스케이드 스텝 수. 예: 교환 → 매치(1) →
            // 캐스케이드로 추가 매치(2) = 콤보 2. (04-score-combo.md 정의와 일치)
            if (steps.Count > MaxCombo)
            {
                MaxCombo = steps.Count;
            }

            // orderRequirements가 비어있다는 것은 스테이지가 아직 구성되지 않았다는 의미다 - 이 경우 절대 클리어되지 않는다 (LevelData 참고).
            // 클리어/게임오버 채널은 여기서 Raise하지 않는다 - Match3Controller가 캐스케이드 애니메이션이
            // 끝난 뒤(OnSwapPlaybackComplete) 이 프로퍼티들을 읽고 알맞은 타이밍에 직접 Raise한다.
            bool wasReshuffled = false;
            if (_requirements.Length > 0 && IsOrderComplete())
            {
                IsCleared = true;
            }
            else if (MovesRemaining <= 0)
            {
                IsGameOver = true;
            }
            else if (!DeadlockDetector.HasValidMove(Board))
            {
                // 라운드가 계속되는데 교환 가능한 매치가 하나도 없으면(데드락) 플레이어가 막혀버리므로,
                // 여기서 즉시 섞는다 - 클리어/게임오버로 라운드가 끝나는 경우는 어차피 다음 수가
                // 필요 없으므로 검사하지 않는다.
                DeadlockDetector.Reshuffle(Board, _random);
                wasReshuffled = true;
            }

            return new SwapResult(true, steps, wasReshuffled);
        }

        // 보드를 행 우선(row-major) 순서로 훑어 스페셜 타일이 있는 첫 셀을 찾는다 - 미션 클리어
        // 후 남은 물약을 하나씩 자동으로 터뜨릴 때 어떤 것부터 시작할지 정하는 데 쓰인다
        // (ResolveCascadeAndFinish 참고). 순서 자체에 특별한 의미는 없다 - 결국 다 터뜨릴 것이므로
        // 매번 일관되게 같은 셀부터 찾기만 하면 된다.
        private GridCell? FindFirstSpecialCell()
        {
            for (int row = 0; row < Board.Rows; row++)
            {
                for (int col = 0; col < Board.Columns; col++)
                {
                    GridCell cell = new GridCell(row, col);
                    if (Board.Get(cell).Special != SpecialKind.None)
                    {
                        return cell;
                    }
                }
            }

            return null;
        }

        private bool IsOrderComplete()
        {
            for (int i = 0; i < _collected.Length; i++)
            {
                if (_collected[i] < _requirements[i].requiredCount)
                {
                    return false;
                }
            }

            return true;
        }

        private OrderProgressEntry[] BuildOrderProgress()
        {
            OrderProgressEntry[] entries = new OrderProgressEntry[_requirements.Length];
            for (int i = 0; i < _requirements.Length; i++)
            {
                entries[i] = new OrderProgressEntry(_requirements[i].typeIndex, _collected[i], _requirements[i].requiredCount);
            }

            return entries;
        }

        private CascadeStepInfo ResolveOneStep(int stepIndex, List<MatchRun> runs, HashSet<GridCell> forcedDetonationSeed)
        {
            SpecialSpawnPlan plan = SpecialTileResolver.Plan(runs);

            HashSet<GridCell> seed = new HashSet<GridCell>(plan.CellsToClear);
            if (forcedDetonationSeed != null)
            {
                foreach (GridCell cell in forcedDetonationSeed)
                {
                    seed.Add(cell);
                }
            }

            HashSet<GridCell> clearedThisStep = SpecialTileResolver.ExpandWithChainReactions(Board, seed);

            // 안전장치: 관련 없는 chain reaction(예: 같은 색상을 공유하는 color bomb)이 함께 휩쓸어 가려고
            // 하더라도, anchor는 이번 스텝에서 반드시 살아남아야 한다.
            foreach (GridCell anchorCell in plan.Anchors.Keys)
            {
                clearedThisStep.Remove(anchorCell);
            }

            List<(GridCell Cell, SpecialKind Kind)> spawned = new List<(GridCell, SpecialKind)>(plan.Anchors.Count);
            foreach (KeyValuePair<GridCell, SpecialKind> anchor in plan.Anchors)
            {
                TileState current = Board.Get(anchor.Key);
                Board.Set(anchor.Key, current.WithSpecial(anchor.Value));
                spawned.Add((anchor.Key, anchor.Value));
            }

            foreach (GridCell cell in clearedThisStep)
            {
                TileState state = Board.Get(cell);

                // 특수 타일 자신은 별개의 타일로 취급한다 - 터지는 순간에도 밑에 깔린 재료 색상을
                // "그 재료를 모은 것"으로 치지 않는다(2026-08-04 변경, 기존에는 특수 타일이 터질 때도
                // 원래 색상 기준으로 주문 수집에 반영됐음). 특수 타일 폭발에 휩쓸린 일반 타일들은
                // 여전히 자신의 색상 기준으로 정상 반영된다 - 여기서 제외되는 건 특수 타일 자신뿐이다.
                if (state.Special == SpecialKind.None && _typeIndexToRequirementIndex.TryGetValue(state.TypeIndex, out int requirementIndex))
                {
                    // 한 번에 필요 개수보다 많이 제거되어도(예: 컬러 폭탄) 목표치를 넘어서까지
                    // 계속 세지 않는다 - IsOrderComplete 자체는 >=로 비교해 문제가 없었지만,
                    // OrderProgressEntry로 노출되는 수집량이 요구량을 넘는 걸 막아둔다.
                    _collected[requirementIndex] = Mathf.Min(_collected[requirementIndex] + 1, _requirements[requirementIndex].requiredCount);
                }

                Board.Set(cell, TileState.EmptyState);
            }

            GravityResult gravity = GravitySolver.CollapseAndRefill(Board, _typeCount, _random);

            // Cascade 깊이 배수: swap 자체가 만든 match는 step 0 (1배)이며, 이어지는 각 cascade 스텝은
            // 점점 더 많은 점수를 주어 긴 연쇄를 유발한 swap에 보상을 준다.
            int pointsAwarded = clearedThisStep.Count * PointsPerTile * (stepIndex + 1);

            return new CascadeStepInfo(
                stepIndex,
                new List<GridCell>(clearedThisStep),
                spawned,
                gravity.Moves,
                gravity.Fills,
                pointsAwarded,
                _snapshotPool.Rent(Board));
        }

        private bool IsValidSwap(GridCell a, GridCell b)
        {
            if (!Board.InBounds(a) || !Board.InBounds(b))
            {
                return false;
            }

            if (Board.Get(a).IsBlocked || Board.Get(b).IsBlocked)
            {
                return false;
            }

            return a.IsAdjacentTo(b);
        }
    }
}
