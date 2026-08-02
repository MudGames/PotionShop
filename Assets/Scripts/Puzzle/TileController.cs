using System;
using Puzzle.Core;

// 원시 타일 클릭(BoardView.TileClicked를 통해)을 "인접한 두 셀을 스왑하고 싶다" 또는 "이 스페셜
// 타일을 제자리에서 활성화하고 싶다" 요청으로 변환한다. 스페셜/막힌 셀 여부를 구분하는 것은 게임
// 규칙이므로, 이 클래스가 직접 Board를 들여다보는 대신 predicate(isSpecialTile, isBlockedTile)로
// 주입받는다 - 이 클래스는 클릭 순서만 알 뿐이다.
public sealed class TileController
{
    private readonly BoardView _boardView;
    private readonly Func<GridCell, bool> _isSpecialTile;
    private readonly Func<GridCell, bool> _isBlockedTile;
    private GridCell? _selectedCell;

    // 스왑이 애니메이션되는 동안(혹은 레벨이 클리어/게임 오버된 이후) false로 설정되어, 호출자가
    // 다시 활성화할 때까지 클릭이 무시된다.
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
    }

    // 대기 중인 첫 탭 선택을 지운다 - 레벨을 시작/재시작할 때 호출해서, 이전 스테이지에서 남은
    // 오래된 선택이 새로 만들어진 보드에 대해 완료되는 일이 없도록 한다.
    public void Reset()
    {
        _selectedCell = null;
    }

    private void OnTileClicked(GridCell cell)
    {
        if (!Enabled || _isBlockedTile(cell))
        {
            return;
        }

        if (!_selectedCell.HasValue)
        {
            SelectOrActivate(cell);
            return;
        }

        GridCell first = _selectedCell.Value;
        _boardView.SetHighlight(first, false);

        if (first == cell)
        {
            _selectedCell = null;
            return;
        }

        if (!first.IsAdjacentTo(cell))
        {
            SelectOrActivate(cell);
            return;
        }

        _selectedCell = null;
        SwapRequested?.Invoke(first, cell);
    }

    // 스페셜 타일은 스왑할 두 번째 타일을 기다리는 대신, 현재 선택이 되는 순간 항상 제자리에서
    // 즉시 활성화된다 - 일반 타일은 여전히 선택/하이라이트만 되고, 두 번째 탭으로 스왑되기를
    // 기다린다.
    private void SelectOrActivate(GridCell cell)
    {
        if (_isSpecialTile(cell))
        {
            SpecialActivationRequested?.Invoke(cell);
            return;
        }

        _selectedCell = cell;
        _boardView.SetHighlight(cell, true);
        TileSelected?.Invoke(cell);
    }
}
