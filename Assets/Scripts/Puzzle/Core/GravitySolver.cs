using System.Collections.Generic;

namespace Puzzle.Core
{
    public readonly struct GravityResult
    {
        public IReadOnlyList<(GridCell From, GridCell To)> Moves { get; }
        public IReadOnlyList<(GridCell Cell, int TypeIndex)> Fills { get; }

        public GravityResult(IReadOnlyList<(GridCell From, GridCell To)> moves, IReadOnlyList<(GridCell Cell, int TypeIndex)> fills)
        {
            Moves = moves;
            Fills = fills;
        }
    }

    // 각 열을 아래쪽으로 압축시키고, 비워진 상단 셀들을 새로 채운다. 막힌(장애물) 셀은 하나의 열을
    // 독립된 구간들로 나눈다 - 타일은 장애물을 넘어 떨어지지 않으며, 장애물 위/아래의 각 구간은
    // 각자 따로 압축/리필된다.
    public static class GravitySolver
    {
        public static GravityResult CollapseAndRefill(Board board, int typeCount, ITileRandomSource random)
        {
            List<(GridCell From, GridCell To)> moves = new List<(GridCell, GridCell)>();
            List<(GridCell Cell, int TypeIndex)> fills = new List<(GridCell, int)>();

            for (int col = 0; col < board.Columns; col++)
            {
                int segmentStart = 0;
                for (int row = 0; row <= board.Rows; row++)
                {
                    bool isBoundary = row == board.Rows || board.Get(row, col).IsBlocked;
                    if (!isBoundary)
                    {
                        continue;
                    }

                    CollapseSegment(board, col, segmentStart, row, typeCount, random, moves, fills);
                    segmentStart = row + 1;
                }
            }

            return new GravityResult(moves, fills);
        }

        // 한 열의 반열림 구간 [startRowInclusive, endRowExclusive)에 대해 압축/리필을 수행한다.
        private static void CollapseSegment(
            Board board,
            int col,
            int startRowInclusive,
            int endRowExclusive,
            int typeCount,
            ITileRandomSource random,
            List<(GridCell From, GridCell To)> moves,
            List<(GridCell Cell, int TypeIndex)> fills)
        {
            if (startRowInclusive >= endRowExclusive)
            {
                return;
            }

            int writeRow = endRowExclusive - 1;

            for (int row = endRowExclusive - 1; row >= startRowInclusive; row--)
            {
                TileState state = board.Get(row, col);
                if (!state.IsFilled)
                {
                    continue;
                }

                if (writeRow != row)
                {
                    board.Set(writeRow, col, state);
                    board.Set(row, col, TileState.EmptyState);
                    moves.Add((new GridCell(row, col), new GridCell(writeRow, col)));
                }

                writeRow--;
            }

            for (int row = writeRow; row >= startRowInclusive; row--)
            {
                int typeIndex = random.NextTypeIndex(typeCount);
                board.Set(row, col, new TileState(typeIndex));
                fills.Add((new GridCell(row, col), typeIndex));
            }
        }
    }
}
