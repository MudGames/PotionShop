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
        private const int MinMissionIngredientTypes = 1;
        private const int MaxMissionIngredientTypes = 3; // inclusive
        private const int MinRequirementCount = 3;
        private const int MaxRequirementCount = 8; // inclusive

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
            int startingMaxCombo = 0)
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

            // 미션은 LevelData에 고정된 값을 쓰는 대신 매번 무작위로 생성한다 - 재료 1~3종을
            // 무작위로 골라 각각 3~8개 수집을 요구한다. LevelData.orderRequirements는 더 이상 읽지
            // 않는다(사용 안 함 - 2026-08-02 결정).
            _requirements = GenerateRandomRequirements(_typeCount, random);
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

        // 재료 1~3종(typeCount보다 많이 고를 수는 없다)을 중복 없이 무작위로 골라, 각각 3~8개
        // 수집을 요구하는 미션을 만든다. typeCount가 0이면 빈 배열을 반환한다 - 그러면
        // ResolveCascadeAndFinish의 "_requirements.Length > 0" 가드에 의해 자연히 절대 클리어되지
        // 않는 기존 안전장치와 동일하게 동작한다.
        private static OrderRequirement[] GenerateRandomRequirements(int typeCount, ITileRandomSource random)
        {
            int requirementTypeCount = Mathf.Min(
                random.NextInRange(MinMissionIngredientTypes, MaxMissionIngredientTypes + 1),
                typeCount);

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
                    requiredCount = random.NextInRange(MinRequirementCount, MaxRequirementCount + 1)
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
                int typeIndex = Board.Get(cell).TypeIndex;
                if (_typeIndexToRequirementIndex.TryGetValue(typeIndex, out int requirementIndex))
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
