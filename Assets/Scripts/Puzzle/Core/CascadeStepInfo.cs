using System;
using System.Collections.Generic;

namespace Puzzle.Core
{
    // 캐스케이드의 한 스텝이 해소된 결과를 Presentation이 애니메이션으로 표현하는 데 필요한 모든 것:
    // 무엇이 제거되었는지, 무엇이 스페셜 타일로 승격되었는지, 기존 타일이 어떻게 아래로 내려왔는지,
    // 새 타일이 어디에 나타났는지.
    public sealed class CascadeStepInfo
    {
        public int StepIndex { get; }
        public IReadOnlyList<GridCell> ClearedCells { get; }
        public IReadOnlyList<(GridCell Cell, SpecialKind Kind)> SpawnedSpecials { get; }
        public IReadOnlyList<(GridCell From, GridCell To)> Moves { get; }
        public IReadOnlyList<(GridCell Cell, int TypeIndex)> Fills { get; }

        // 이 스텝 하나만으로 얻은 점수 (이 스텝에서 제거된 타일 수 * 캐스케이드 깊이 배율)이며,
        // Presentation이 이 스텝을 보게 될 시점에는 이미 GridController.Score에 합산되어 있다 -
        // Presentation이 누적 점수와 별개로 스텝별 콤보 팝업을 보여줄 수 있도록 따로 노출해 둔 것.
        public int PointsAwarded { get; }

        // 이 스텝이 끝난 시점(제거 + 스페셜 생성 + 중력/리필까지 모두 반영된 후)의 보드 모습을 그대로
        // 담고 있다. 호출자가 실제로 스텝 0을 애니메이션하기 시작할 즈음에는 GridController.Board
        // 자체는 이미 캐스케이드 종료 후 최종 상태로 넘어가 있으므로, Presentation이 이 스텝의 중간
        // 결과를 렌더링할 수 있도록 별도로 보관해 둔 것이다.
        //
        // 스텝/스왑마다 새로운 Rows*Columns 할당이 발생하지 않도록 풀링된 Board(BoardSnapshotPool)를
        // 사용한다 - 동일 인스턴스에서 다음 GridController.TrySwap 호출이 일어나기 전까지만 유효하다.
        // 다음 스왑을 트리거하기 전에 각 스텝의 데이터를 반드시 다 소비해야 한다.
        public Board BoardSnapshot { get; }

        public CascadeStepInfo(
            int stepIndex,
            IReadOnlyList<GridCell> clearedCells,
            IReadOnlyList<(GridCell Cell, SpecialKind Kind)> spawnedSpecials,
            IReadOnlyList<(GridCell From, GridCell To)> moves,
            IReadOnlyList<(GridCell Cell, int TypeIndex)> fills,
            int pointsAwarded,
            Board boardSnapshot)
        {
            StepIndex = stepIndex;
            ClearedCells = clearedCells;
            SpawnedSpecials = spawnedSpecials;
            Moves = moves;
            Fills = fills;
            PointsAwarded = pointsAwarded;
            BoardSnapshot = boardSnapshot;
        }
    }

    // 한 번의 GridController.TrySwap 호출 결과. Accepted=false는 스왑이 매치를 만들지 못해 즉시
    // 되돌려졌다는 뜻이다 - 이동 횟수도 소비되지 않았고 Steps도 비어 있다.
    public sealed class SwapResult
    {
        public static readonly SwapResult Rejected = new SwapResult(false, Array.Empty<CascadeStepInfo>());

        public bool Accepted { get; }
        public IReadOnlyList<CascadeStepInfo> Steps { get; }

        public SwapResult(bool accepted, IReadOnlyList<CascadeStepInfo> steps)
        {
            Accepted = accepted;
            Steps = steps;
        }
    }
}
