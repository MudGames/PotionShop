using System.Collections.Generic;

namespace Puzzle.Core
{
    // 플레이어가 첫 수를 두기 전에 3연속 매치가 존재하지 않도록 시작 보드를 구성한다
    // (그렇지 않으면 캐스케이드가 자동으로 이를 처리해버려서 시작부터 점수가 0보다 커지게 된다).
    //
    // 이 "즉시 매치 없음" 보장은 typeCount >= 3일 때만 성립한다. 색상이 정확히 2개뿐이면, 어떤 셀은
    // 왼쪽에 이미 배치된 동일 색상 쌍(색상 A라고 하자)과 위쪽에 이미 배치된 동일 색상 쌍(색상 B, B != A)을
    // 동시에 갖게 될 수 있다 - 색상이 총 두 개뿐이므로 남은 선택지는 항상 A 아니면 B이고, 결국 무엇을
    // 고르든 두 런(run) 중 하나는 반드시 완성되어 버린다. 이는 좌→우/상→하 방식의 이 구성 로직에서
    // 2색 팔레트가 갖는 구조적인 한계이지 버그가 아니다: typeCount >= 3이면 두 런을 동시에 피할 수 있는
    // 색상이 항상 하나 이상 남아 있으므로 보장이 성립한다.
    public static class BoardInitializer
    {
        public static Board CreateInitialBoard(int rows, int columns, int typeCount, IReadOnlyList<GridCell> blockedCells, ITileRandomSource random)
        {
            Board board = new Board(rows, columns);

            if (blockedCells != null)
            {
                foreach (GridCell cell in blockedCells)
                {
                    if (board.InBounds(cell))
                    {
                        board.Set(cell, TileState.BlockedState);
                    }
                }
            }

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    if (board.Get(row, col).IsBlocked)
                    {
                        continue;
                    }

                    int typeIndex = PickTypeIndexAvoidingMatch(board, row, col, typeCount, random);
                    board.Set(row, col, new TileState(typeIndex));
                }
            }

            return board;
        }

        // 가장 엄격한 제약(가로/세로 런을 모두 완성하지 않기)부터 시도한 뒤, 한 번에 하나씩 제약을
        // 완화한다. 팔레트가 작을 경우(예: 2~3색) 그렇지 않으면 후보가 0개가 되어 예전에는 예외가
        // 발생했었다 - 이 폴백 체인은 색상이 항상 하나는 선택되도록 보장한다.
        private static int PickTypeIndexAvoidingMatch(Board board, int row, int col, int typeCount, ITileRandomSource random)
        {
            List<int> candidates = CollectCandidates(board, row, col, typeCount, avoidHorizontal: true, avoidVertical: true);

            if (candidates.Count == 0)
            {
                candidates = CollectCandidates(board, row, col, typeCount, avoidHorizontal: true, avoidVertical: false);
            }

            if (candidates.Count == 0)
            {
                candidates = CollectCandidates(board, row, col, typeCount, avoidHorizontal: false, avoidVertical: true);
            }

            if (candidates.Count == 0)
            {
                for (int typeIndex = 0; typeIndex < typeCount; typeIndex++)
                {
                    candidates.Add(typeIndex);
                }
            }

            int pickIndex = random.NextTypeIndex(candidates.Count);
            return candidates[pickIndex];
        }

        private static List<int> CollectCandidates(Board board, int row, int col, int typeCount, bool avoidHorizontal, bool avoidVertical)
        {
            List<int> candidates = new List<int>();

            for (int typeIndex = 0; typeIndex < typeCount; typeIndex++)
            {
                bool matchesHorizontal = avoidHorizontal && col >= 2 &&
                    IsSameFilledType(board, row, col - 1, typeIndex) &&
                    IsSameFilledType(board, row, col - 2, typeIndex);

                bool matchesVertical = avoidVertical && row >= 2 &&
                    IsSameFilledType(board, row - 1, col, typeIndex) &&
                    IsSameFilledType(board, row - 2, col, typeIndex);

                if (!matchesHorizontal && !matchesVertical)
                {
                    candidates.Add(typeIndex);
                }
            }

            return candidates;
        }

        private static bool IsSameFilledType(Board board, int row, int col, int typeIndex)
        {
            TileState state = board.Get(row, col);
            return state.IsFilled && state.TypeIndex == typeIndex;
        }
    }
}
