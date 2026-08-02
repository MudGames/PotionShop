namespace Puzzle.Core
{
    // 캐스케이드 스텝별 스냅샷(CascadeStepInfo.BoardSnapshot 참고)으로 쓰이는 Board 인스턴스를
    // 풀링한다 - Presentation 쪽에서 TileViewPool이 쓰는 rent/return-all 패턴과 동일한 방식이다.
    // 이게 없으면 캐스케이드 스텝마다 Board.Clone()을 통해 완전히 새로운 Rows*Columns 배열을
    // 할당하게 되는데, 플레이 세션 전체로 보면 한 번의 스왑 안에서만 읽히면 되는 데이터를 위해
    // 수명이 짧은 배열 쓰레기가 상당히 쌓이는 셈이다.
    //
    // 사용 규약: TrySwap 호출 시작 시 ReturnAll()을 한 번 호출한 뒤, 그 호출 내에서 캐스케이드
    // 스텝마다 Rent(board)를 한 번씩 호출한다. 스냅샷들은 다음 ReturnAll()이 호출되기 전까지만
    // 유효하고 서로 구분되므로 - 호출자는 동일한 GridController 인스턴스에서 다른 스왑을 시작하기
    // 전에 반드시 이번 스왑의 CascadeStepInfo.BoardSnapshot 값들을 다 읽어 들여야 한다.
    public sealed class BoardSnapshotPool
    {
        // 첫 Rent(source) 호출 전까지는 source의 Rows/Columns를 알 수 없으므로 그때 지연 생성한다 -
        // 이 풀은 항상 같은 GridController 인스턴스의 Board만 스냅샷하므로, 이후 호출에서도 크기는
        // 동일하다.
        private PoolManager<Board> _pool;

        public void ReturnAll()
        {
            _pool?.ResetRent();
        }

        public Board Rent(Board source)
        {
            _pool ??= new PoolManager<Board>(() => new Board(source.Rows, source.Columns));

            Board snapshot = _pool.Rent();
            snapshot.CopyFrom(source);
            return snapshot;
        }
    }
}
