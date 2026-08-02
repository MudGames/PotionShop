namespace Puzzle.Core
{
    // 그리드를 위한 순수 데이터 홀더. MonoBehaviour/GameObject에 의존하지 않으므로 씬을 건드리지 않고도
    // EditMode 테스트 안에서 자유롭게 생성하고 값을 바꿔도 안전하다.
    public sealed class Board
    {
        private readonly TileState[,] _cells;

        public int Rows { get; }
        public int Columns { get; }

        public Board(int rows, int columns)
        {
            Rows = rows;
            Columns = columns;
            _cells = new TileState[rows, columns];
        }

        public bool InBounds(int row, int col)
        {
            return row >= 0 && row < Rows && col >= 0 && col < Columns;
        }

        public bool InBounds(GridCell cell)
        {
            return InBounds(cell.Row, cell.Col);
        }

        public TileState Get(int row, int col)
        {
            return _cells[row, col];
        }

        public TileState Get(GridCell cell)
        {
            return _cells[cell.Row, cell.Col];
        }

        public void Set(int row, int col, TileState state)
        {
            _cells[row, col] = state;
        }

        public void Set(GridCell cell, TileState state)
        {
            _cells[cell.Row, cell.Col] = state;
        }

        public void Swap(GridCell a, GridCell b)
        {
            (_cells[a.Row, a.Col], _cells[b.Row, b.Col]) = (_cells[b.Row, b.Col], _cells[a.Row, a.Col]);
        }

        // 완전히 새로운 Board로 깊은 복사를 수행한다. 캐스케이드 스텝마다 스냅샷이 필요한 핫 패스에서는
        // 이 메서드 대신 CopyFrom(BoardSnapshotPool 경유)을 사용할 것 - 이 메서드는 호출할 때마다 새로운
        // 배킹 배열을 할당한다.
        public Board Clone()
        {
            Board clone = new Board(Rows, Columns);
            clone.CopyFrom(this);
            return clone;
        }

        // 크기가 동일한 다른 Board로부터 제자리(in-place)에서 깊은 복사를 수행한다 - 할당이 발생하지
        // 않는다. 이 덕분에 BoardSnapshotPool은 스텝마다 새로운 Rows*Columns 배열을 할당하는 대신,
        // 여러 캐스케이드 스텝/스왑에 걸쳐 동일한 배킹 배열을 재사용할 수 있다.
        public void CopyFrom(Board other)
        {
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    _cells[row, col] = other._cells[row, col];
                }
            }
        }
    }
}
