using System.Collections.Generic;

namespace Puzzle.Core
{
    public readonly struct MatchRun
    {
        public IReadOnlyList<GridCell> Cells { get; }
        public bool IsHorizontal { get; }

        public MatchRun(IReadOnlyList<GridCell> cells, bool isHorizontal)
        {
            Cells = cells;
            IsHorizontal = isHorizontal;
        }
    }

    // 행을 먼저 스캔한 다음 열을 스캔하여, 채워진(비어있지 않고 막히지 않은) 동일한 타일 타입이
    // 3개 이상 연속되는 run을 찾는다. L자/T자 모양은 별도의 특수 처리가 필요 없다 - 코너 셀은
    // 그냥 horizontal run과 vertical run에 동시에 속하게 되며, 호출자(SpecialTileResolver)가
    // 그 겹침을 감지할 수 있다.
    public static class MatchDetector
    {
        public static List<MatchRun> FindRuns(Board board)
        {
            List<MatchRun> runs = new List<MatchRun>();

            for (int row = 0; row < board.Rows; row++)
            {
                int runStart = 0;
                for (int col = 1; col <= board.Columns; col++)
                {
                    bool sameAsPrev = col < board.Columns && IsSameFilledType(board, row, col, row, col - 1);
                    if (!sameAsPrev)
                    {
                        AddRunIfLongEnough(runs, runStart, col, isHorizontal: true, fixedIndex: row);
                        runStart = col;
                    }
                }
            }

            for (int col = 0; col < board.Columns; col++)
            {
                int runStart = 0;
                for (int row = 1; row <= board.Rows; row++)
                {
                    bool sameAsPrev = row < board.Rows && IsSameFilledType(board, row, col, row - 1, col);
                    if (!sameAsPrev)
                    {
                        AddRunIfLongEnough(runs, runStart, row, isHorizontal: false, fixedIndex: col);
                        runStart = row;
                    }
                }
            }

            return runs;
        }

        public static HashSet<GridCell> ToCellSet(IReadOnlyList<MatchRun> runs)
        {
            HashSet<GridCell> cells = new HashSet<GridCell>();
            foreach (MatchRun run in runs)
            {
                foreach (GridCell cell in run.Cells)
                {
                    cells.Add(cell);
                }
            }
            return cells;
        }

        private static void AddRunIfLongEnough(List<MatchRun> runs, int runStart, int runEndExclusive, bool isHorizontal, int fixedIndex)
        {
            int length = runEndExclusive - runStart;
            if (length < 3)
            {
                return;
            }

            List<GridCell> cells = new List<GridCell>(length);
            for (int k = runStart; k < runEndExclusive; k++)
            {
                cells.Add(isHorizontal ? new GridCell(fixedIndex, k) : new GridCell(k, fixedIndex));
            }

            runs.Add(new MatchRun(cells, isHorizontal));
        }

        // 특수 타일(폭탄)은 매치 대상이 아니다 - 색깔이 같아도 런에 끼워주지 않는다. 그래야 캐스케이드
        // 중 우연히 같은 색 사이에 끼어도 조용히 사라지지 않고, 플레이어가 직접 스왑/탭으로 활성화할
        // 때만 사라진다(12-special-tiles.md 참고).
        private static bool IsSameFilledType(Board board, int rowA, int colA, int rowB, int colB)
        {
            TileState a = board.Get(rowA, colA);
            TileState b = board.Get(rowB, colB);
            return a.IsFilled && b.IsFilled && a.TypeIndex == b.TypeIndex
                && a.Special == SpecialKind.None && b.Special == SpecialKind.None;
        }
    }
}
