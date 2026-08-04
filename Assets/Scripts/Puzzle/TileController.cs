using System;
using Puzzle.Core;

// 원시 입력(BoardView.TileClicked=탭, BoardView.TileSwiped=드래그)을 "인접한 두 셀을 스왑하고
// 싶다" 또는 "이 스페셜 타일을 제자리에서 활성화하고 싶다" 요청으로 변환한다. 스페셜/막힌 셀
// 여부를 구분하는 것은 게임 규칙이므로, 이 클래스가 직접 Board를 들여다보는 대신
// predicate(isSpecialTile, isBlockedTile)로 주입받는다.
//
// 스왑은 드래그 한 번으로 완결된다(2026-08-04, 기존 "첫 번째 칸 탭 → 인접한 두 번째 칸 탭" 방식을
// 대체) - BoardView.TileSwiped가 이미 드래그 방향으로 계산한 인접 칸 쌍을 주므로, 대기 중인 선택
// 상태를 따로 들고 있을 필요가 없다. 탭은 이제 스페셜 타일 즉시 활성화(그 자리에서, 방향 없이)
// 전용이며, 일반 타일 탭은 TileSelected(선택 사운드용)만 알린다.
public sealed class TileController
{
    private readonly BoardView _boardView;
    private readonly Func<GridCell, bool> _isSpecialTile;
    private readonly Func<GridCell, bool> _isBlockedTile;

    // 스왑이 애니메이션되는 동안(혹은 레벨이 클리어/게임 오버된 이후) false로 설정되어, 호출자가
    // 다시 활성화할 때까지 입력이 무시된다.
    public bool Enabled { get; set; } = true;

    public event Action<GridCell, GridCell> SwapRequested;
    public event Action<GridCell> SpecialActivationRequested;
    public event Action<GridCell> TileSelected;

    public TileController(BoardView boardView, Func<GridCell, bool> isSpecialTile, Func<GridCell, bool> isBlockedTile)
    {
        _boardView = boardView;
        _isSpecialTile = isSpecialTile;
        _isBlockedTile = isBlockedTile;
        _boardView.TileClicked += OnTileClicked;
        _boardView.TileSwiped += OnTileSwiped;
    }

    private void OnTileClicked(GridCell cell)
    {
        if (!Enabled || _isBlockedTile(cell))
        {
            return;
        }

        if (_isSpecialTile(cell))
        {
            SpecialActivationRequested?.Invoke(cell);
            return;
        }

        TileSelected?.Invoke(cell);
    }

    private void OnTileSwiped(GridCell from, GridCell to)
    {
        if (!Enabled || _isBlockedTile(from) || _isBlockedTile(to))
        {
            return;
        }

        SwapRequested?.Invoke(from, to);
    }
}
