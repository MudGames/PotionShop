using System.Collections.Generic;

namespace Puzzle.Core
{
    // 캐스케이드가 모두 끝난 뒤 보드에 교환 가능한 매치가 하나도 없는("데드락") 상태로 플레이어에게
    // 넘기는 것을 막는다 - BoardInitializer는 시작 시점에 매치가 "이미 있는" 것만 막을 뿐, 캐스케이드로
    // 새로 채워진 이후 "매치를 만들 수 있는 수가 하나도 없는" 경우는 어디서도 확인하지 않았다.
    public static class DeadlockDetector
    {
        private const int MaxReshuffleAttempts = 30;

        // 인접한 두 셀(가로/세로)을 실제로 바꿔보지 않고, 임시로 교체한 뒤 매치가 생기는지만 확인하고
        // 즉시 되돌린다 - 셀 하나당 오른쪽/아래쪽만 검사하면 모든 인접 쌍을 중복 없이 훑을 수 있다.
        public static bool HasValidMove(Board board)
        {
            for (int row = 0; row < board.Rows; row++)
            {
                for (int col = 0; col < board.Columns; col++)
                {
                    GridCell cell = new GridCell(row, col);
                    if (board.Get(cell).IsBlocked)
                    {
                        continue;
                    }

                    if (col + 1 < board.Columns && IsValidSwapCandidate(board, cell, new GridCell(row, col + 1)))
                    {
                        return true;
                    }

                    if (row + 1 < board.Rows && IsValidSwapCandidate(board, cell, new GridCell(row + 1, col)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // 채워진 타일들의 TileState를 셀 위치끼리 무작위로 섞는다(개수/색상 구성은 그대로 유지). 곧바로
        // 매치가 생기거나 여전히 데드락이면 다시 섞기를 반복한다 - typeCount>=3인 일반적인 6x6 보드에서
        // MaxReshuffleAttempts 안에 실패하는 일은 사실상 없지만, 혹시 실패하더라도 무한 루프에 빠지는
        // 대신 마지막 섞기 결과를 그대로 둔다.
        public static void Reshuffle(Board board, ITileRandomSource random)
        {
            List<GridCell> cells = new List<GridCell>();
            List<TileState> states = new List<TileState>();

            for (int row = 0; row < board.Rows; row++)
            {
                for (int col = 0; col < board.Columns; col++)
                {
                    TileState state = board.Get(row, col);
                    if (state.IsFilled)
                    {
                        cells.Add(new GridCell(row, col));
                        states.Add(state);
                    }
                }
            }

            for (int attempt = 0; attempt < MaxReshuffleAttempts; attempt++)
            {
                ShuffleInPlace(states, random);

                for (int i = 0; i < cells.Count; i++)
                {
                    board.Set(cells[i], states[i]);
                }

                if (MatchDetector.FindRuns(board).Count == 0 && HasValidMove(board))
                {
                    return;
                }
            }
        }

        // GridController.TrySwap의 activatesSpecial과 같은 기준: 스페셜 타일과의 교환은 매치가
        // 생기든 안 생기든 항상 유효한 수다(교환하면 무조건 활성화됨). 매치 여부만 보면, 특수
        // 타일이 있어서 실제로는 둘 수 있는데도 데드락으로 오판해 불필요하게 재셔플할 수 있다.
        private static bool IsValidSwapCandidate(Board board, GridCell a, GridCell b)
        {
            TileState stateA = board.Get(a);
            TileState stateB = board.Get(b);
            if (!stateA.IsFilled || !stateB.IsFilled)
            {
                return false;
            }

            if (stateA.Special != SpecialKind.None || stateB.Special != SpecialKind.None)
            {
                return true;
            }

            if (stateA.TypeIndex == stateB.TypeIndex)
            {
                return false;
            }

            board.Set(a, stateB);
            board.Set(b, stateA);
            bool createsMatch = MatchDetector.FindRuns(board).Count > 0;
            board.Set(a, stateA);
            board.Set(b, stateB);

            return createsMatch;
        }

        private static void ShuffleInPlace(List<TileState> states, ITileRandomSource random)
        {
            for (int i = states.Count - 1; i > 0; i--)
            {
                int j = random.NextInRange(0, i + 1);
                (states[i], states[j]) = (states[j], states[i]);
            }
        }
    }
}
