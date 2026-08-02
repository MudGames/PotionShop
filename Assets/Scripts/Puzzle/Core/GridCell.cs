using System;

namespace Puzzle.Core
{
    // 보드 좌표를 나타내며, Vector2Int가 갖는 화면 좌표스러운 x/y 대신 명시적으로 Row/Col이라는
    // 이름을 사용한다. 예전에는 그리드 코드가 (row, col)을 Vector2Int.x/.y에 저장했는데, 이 규칙을
    // 아는 사람에게는 문제없이 읽히지만 처음 보는 사람은 실제 화면/월드 좌표 (x, y)와 혼동하기 쉬웠다.
    [Serializable]
    public struct GridCell : IEquatable<GridCell>
    {
        public int Row;
        public int Col;

        public GridCell(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public bool Equals(GridCell other)
        {
            return Row == other.Row && Col == other.Col;
        }

        public override bool Equals(object obj)
        {
            return obj is GridCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Row * 397) ^ Col;
        }

        public static bool operator ==(GridCell a, GridCell b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(GridCell a, GridCell b)
        {
            return !a.Equals(b);
        }

        public override string ToString()
        {
            return $"({Row}, {Col})";
        }

        // 직교 방향으로 한 칸 떨어진 경우만 해당한다(대각선은 아님). "인접"에 대한 단일 진실 공급원
        // (single source of truth)이다 - 예전에는 GridController.IsValidSwap과 TileController가
        // 각자 이 공식을 따로 들고 있어서, 인접 규칙이 바뀌면 둘이 서로 어긋날 위험이 있었다.
        public bool IsAdjacentTo(GridCell other)
        {
            return (Math.Abs(Row - other.Row) == 1 && Col == other.Col) ||
                   (Math.Abs(Col - other.Col) == 1 && Row == other.Row);
        }
    }
}
