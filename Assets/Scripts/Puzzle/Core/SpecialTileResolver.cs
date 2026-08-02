using System.Collections.Generic;
using UnityEngine;

namespace Puzzle.Core
{
    public readonly struct SpecialSpawnPlan
    {
        // 이번 스텝에서 스페셜 타일로 승격되는 셀들 - 이번 스텝에서 절대 clear되어서는 안 되며,
        // 새로운 bomb로서 살아남는다.
        public IReadOnlyDictionary<GridCell, SpecialKind> Anchors { get; }

        // 위의 anchor들을 제외한 매치된 모든 셀 - 이번 스텝에서 실제로 clear되는 셀들이다.
        public IReadOnlyCollection<GridCell> CellsToClear { get; }

        public SpecialSpawnPlan(IReadOnlyDictionary<GridCell, SpecialKind> anchors, IReadOnlyCollection<GridCell> cellsToClear)
        {
            Anchors = anchors;
            CellsToClear = cellsToClear;
        }
    }

    // 어떤 매치된 run이 스페셜 타일을 생성할지 결정하고, 그 clear 과정에 휩쓸린 스페셜 타일의
    // chain-reaction 폭발을 반영해 clear 대상 집합을 확장한다.
    //
    // 규칙 (PRD를 그대로 옮긴 것이 아니라 설계상의 선택이므로 여기 문서화해둔다):
    //  - run 길이 == 4            -> run의 가운데 셀이 (horizontal run이면) LineRow 또는
    //                                   (vertical run이면) LineColumn bomb으로 살아남는다.
    //  - run 길이 >= 5            -> run의 가운데 셀이 ColorBomb으로 살아남는다.
    //  - horizontal run과 vertical run의 교차 (L자/T자 모양) -> 공유하는 셀이 RadiusBomb(주변
    //                                   3x3 반경)으로 살아남는다 (같은 셀에서 더 약한 4-length
    //                                   line-bomb anchor를 덮어쓴다. 하나의 run이 길이-4이면서
    //                                   동시에 교차의 일부일 수 있기 때문이다).
    public static class SpecialTileResolver
    {
        public static SpecialSpawnPlan Plan(IReadOnlyList<MatchRun> runs)
        {
            Dictionary<GridCell, SpecialKind> anchors = new Dictionary<GridCell, SpecialKind>();

            foreach (MatchRun run in runs)
            {
                if (run.Cells.Count == 4)
                {
                    GridCell anchor = run.Cells[run.Cells.Count / 2];
                    anchors[anchor] = run.IsHorizontal ? SpecialKind.LineRow : SpecialKind.LineColumn;
                }
            }

            foreach (MatchRun run in runs)
            {
                if (run.Cells.Count >= 5)
                {
                    GridCell anchor = run.Cells[run.Cells.Count / 2];
                    anchors[anchor] = SpecialKind.ColorBomb;
                }
            }

            // 매 horizontal run마다 새로운 HashSet을 만드는 대신, 모든 horizontal run의 셀을 한 번만
            // 모아 만든 통합 집합 - horizontal run이 여러 개 있는 스텝에서도 vertical run과의 교차를
            // 검사하기 위해 run마다 일회용 집합을 할당하지 않아도 된다.
            HashSet<GridCell> horizontalCells = new HashSet<GridCell>();
            foreach (MatchRun run in runs)
            {
                if (run.IsHorizontal)
                {
                    foreach (GridCell cell in run.Cells)
                    {
                        horizontalCells.Add(cell);
                    }
                }
            }

            foreach (MatchRun run in runs)
            {
                if (run.IsHorizontal)
                {
                    continue;
                }

                foreach (GridCell cell in run.Cells)
                {
                    if (horizontalCells.Contains(cell))
                    {
                        anchors[cell] = SpecialKind.RadiusBomb;
                    }
                }
            }

            HashSet<GridCell> cellsToClear = MatchDetector.ToCellSet(runs);
            foreach (GridCell anchorCell in anchors.Keys)
            {
                cellsToClear.Remove(anchorCell);
            }

            return new SpecialSpawnPlan(anchors, cellsToClear);
        }

        // 초기 clear 대상 셀 집합을 chain-reaction 폭발로 확장한다: clear되는 셀이 이미 스페셜 타일을
        // 갖고 있다면 그 효과(행/열/색상 전체)도 집합에 추가되고, 새로 추가된 셀들 역시 차례로 자신의
        // 스페셜 효과가 있는지 검사받는다.
        //
        // 구조적으로 반드시 종료된다: `cleared.Add`는 하나의 셀이 큐에 단 한 번만 들어가도록 하므로,
        // 보드 내용과 무관하게 rows*columns로 상한이 정해진다. 아래의 반복 횟수 상한은 순전히 심층
        // 방어용이며 (도달할 수 없어야 정상) 정상 경로의 제한이 아니라 향후 로직 버그를 감지하기 위한
        // 카나리아로 존재한다.
        public static HashSet<GridCell> ExpandWithChainReactions(Board board, IEnumerable<GridCell> initialCleared)
        {
            HashSet<GridCell> cleared = new HashSet<GridCell>(initialCleared);
            Queue<GridCell> pending = new Queue<GridCell>(cleared);
            HashSet<GridCell> detonated = new HashSet<GridCell>();

            int guard = board.Rows * board.Columns * 2;
            int iterations = 0;

            while (pending.Count > 0)
            {
                if (++iterations > guard)
                {
                    Debug.LogWarning("SpecialTileResolver.ExpandWithChainReactions exceeded its safety cap - aborting expansion early. This should be unreachable; investigate if seen.");
                    break;
                }

                GridCell cell = pending.Dequeue();
                if (!detonated.Add(cell) || !board.InBounds(cell))
                {
                    continue;
                }

                TileState state = board.Get(cell);
                if (state.Special == SpecialKind.None)
                {
                    continue;
                }

                foreach (GridCell extra in CellsAffectedBySpecial(board, cell, state))
                {
                    if (cleared.Add(extra))
                    {
                        pending.Enqueue(extra);
                    }
                }
            }

            return cleared;
        }

        private static IEnumerable<GridCell> CellsAffectedBySpecial(Board board, GridCell cell, TileState state)
        {
            switch (state.Special)
            {
                case SpecialKind.LineRow:
                    foreach (GridCell c in RowCells(board, cell.Row))
                    {
                        yield return c;
                    }
                    break;

                case SpecialKind.LineColumn:
                    foreach (GridCell c in ColumnCells(board, cell.Col))
                    {
                        yield return c;
                    }
                    break;

                case SpecialKind.ColorBomb:
                    foreach (GridCell c in ColorCells(board, state.TypeIndex))
                    {
                        yield return c;
                    }
                    break;

                case SpecialKind.RadiusBomb:
                    foreach (GridCell c in RadiusCells(board, cell, RadiusBombRadius))
                    {
                        yield return c;
                    }
                    break;
            }
        }

        // RadiusBomb 한 칸 - (2*radius+1) 정사각형 범위. radius=1이면 3x3 (전형적인 "wrapped candy").
        private const int RadiusBombRadius = 1;

        private static IEnumerable<GridCell> RadiusCells(Board board, GridCell center, int radius)
        {
            for (int row = center.Row - radius; row <= center.Row + radius; row++)
            {
                for (int col = center.Col - radius; col <= center.Col + radius; col++)
                {
                    GridCell cell = new GridCell(row, col);
                    if (board.InBounds(cell) && !board.Get(cell).IsBlocked)
                    {
                        yield return cell;
                    }
                }
            }
        }

        private static IEnumerable<GridCell> RowCells(Board board, int row)
        {
            for (int col = 0; col < board.Columns; col++)
            {
                if (!board.Get(row, col).IsBlocked)
                {
                    yield return new GridCell(row, col);
                }
            }
        }

        private static IEnumerable<GridCell> ColumnCells(Board board, int col)
        {
            for (int row = 0; row < board.Rows; row++)
            {
                if (!board.Get(row, col).IsBlocked)
                {
                    yield return new GridCell(row, col);
                }
            }
        }

        private static IEnumerable<GridCell> ColorCells(Board board, int color)
        {
            for (int row = 0; row < board.Rows; row++)
            {
                for (int col = 0; col < board.Columns; col++)
                {
                    TileState other = board.Get(row, col);
                    if (other.IsFilled && other.TypeIndex == color)
                    {
                        yield return new GridCell(row, col);
                    }
                }
            }
        }

        private static bool IsLine(SpecialKind kind)
        {
            return kind == SpecialKind.LineRow || kind == SpecialKind.LineColumn;
        }

        private static bool IsRadius(SpecialKind kind)
        {
            return kind == SpecialKind.RadiusBomb;
        }

        private static IEnumerable<GridCell> CrossCells(Board board, GridCell cell)
        {
            foreach (GridCell c in RowCells(board, cell.Row))
            {
                yield return c;
            }

            foreach (GridCell c in ColumnCells(board, cell.Col))
            {
                yield return c;
            }
        }

        private static IEnumerable<GridCell> AllFilledCells(Board board)
        {
            for (int row = 0; row < board.Rows; row++)
            {
                for (int col = 0; col < board.Columns; col++)
                {
                    TileState state = board.Get(row, col);
                    if (state.IsFilled)
                    {
                        yield return new GridCell(row, col);
                    }
                }
            }
        }

        private static IEnumerable<GridCell> ColorToLineCells(Board board, int color, SpecialKind lineKind)
        {
            foreach (GridCell colorCell in ColorCells(board, color))
            {
                IEnumerable<GridCell> affected = lineKind == SpecialKind.LineRow
                    ? RowCells(board, colorCell.Row)
                    : ColumnCells(board, colorCell.Col);

                foreach (GridCell cell in affected)
                {
                    yield return cell;
                }
            }
        }

        // color bomb + radius bomb 콤보용: 색상 일치하는 모든 타일 각각을 radius bomb의 중심으로
        // 취급해 터뜨린다 - ColorToLineCells와 같은 패턴이다.
        private static IEnumerable<GridCell> ColorToRadiusCells(Board board, int color, int radius)
        {
            foreach (GridCell colorCell in ColorCells(board, color))
            {
                foreach (GridCell cell in RadiusCells(board, colorCell, radius))
                {
                    yield return cell;
                }
            }
        }

        // line + radius bomb 콤보용: line bomb 자신의 행/열을 중심으로 (2*extraLines+1)개의 행 또는
        // 열 전체를 clear한다("두꺼운 line") - 보드 경계를 벗어나는 행/열은 건너뛴다(RowCells/
        // ColumnCells는 유효한 행/열 번호가 들어온다고 가정하므로 여기서 미리 걸러야 한다).
        private static IEnumerable<GridCell> ThickLineCells(Board board, GridCell lineCell, SpecialKind lineKind, int extraLines)
        {
            if (lineKind == SpecialKind.LineRow)
            {
                for (int row = lineCell.Row - extraLines; row <= lineCell.Row + extraLines; row++)
                {
                    if (row < 0 || row >= board.Rows)
                    {
                        continue;
                    }

                    foreach (GridCell cell in RowCells(board, row))
                    {
                        yield return cell;
                    }
                }
            }
            else
            {
                for (int col = lineCell.Col - extraLines; col <= lineCell.Col + extraLines; col++)
                {
                    if (col < 0 || col >= board.Columns)
                    {
                        continue;
                    }

                    foreach (GridCell cell in ColumnCells(board, col))
                    {
                        yield return cell;
                    }
                }
            }
        }

        // 두 개의 스페셜 타일을 의도적으로 서로 swap하면 각각 독립적으로 터지는 대신 하나의 더 강력한
        // 효과로 결합된다 (전형적인 매치3 UX). SpecialKind에는 세 가지 "계열"(line 계열, color bomb,
        // radius bomb)이 있으므로 모든 조합이 다뤄진다:
        //  - line + line           -> 두 anchor 셀 모두 "십자(cross)" 형태가 된다 (각자 자신의 행과
        //                              열 모두), 원래 각자의 단일 line만 clear하는 대신.
        //  - colorBomb + colorBomb -> 보드 전체가 clear된다 (화려한 피니셔).
        //  - colorBomb + line      -> color bomb에 저장된 색상과 일치하는 모든 타일이 짝을 이룬 line
        //                              special의 방향을 가진 것처럼 취급되어 터진다 (전형적인
        //                              "color bomb + striped candy" 콤보).
        //  - radiusBomb + radiusBomb -> 두 anchor 셀 모두 반경이 한 단계 커진다(3x3 -> 5x5).
        //  - radiusBomb + line     -> line bomb의 행/열을 중심으로 그 방향의 행/열 3개가 통째로
        //                              clear된다("두꺼운 line", 전형적인 "wrapped + striped" 콤보).
        //  - radiusBomb + colorBomb -> color bomb에 저장된 색상과 일치하는 모든 타일 각각이 radius
        //                              bomb의 중심인 것처럼 취급되어 터진다.
        public static IEnumerable<GridCell> ComboCells(Board board, GridCell cellA, SpecialKind kindA, GridCell cellB, SpecialKind kindB)
        {
            bool aIsLine = IsLine(kindA);
            bool bIsLine = IsLine(kindB);
            bool aIsRadius = IsRadius(kindA);
            bool bIsRadius = IsRadius(kindB);

            if (kindA == SpecialKind.ColorBomb && kindB == SpecialKind.ColorBomb)
            {
                foreach (GridCell cell in AllFilledCells(board))
                {
                    yield return cell;
                }

                yield break;
            }

            if (aIsLine && bIsLine)
            {
                foreach (GridCell cell in CrossCells(board, cellA))
                {
                    yield return cell;
                }

                foreach (GridCell cell in CrossCells(board, cellB))
                {
                    yield return cell;
                }

                yield break;
            }

            if (aIsRadius && bIsRadius)
            {
                foreach (GridCell cell in RadiusCells(board, cellA, RadiusBombRadius + 1))
                {
                    yield return cell;
                }

                foreach (GridCell cell in RadiusCells(board, cellB, RadiusBombRadius + 1))
                {
                    yield return cell;
                }

                yield break;
            }

            if (kindA == SpecialKind.ColorBomb && bIsLine)
            {
                foreach (GridCell cell in ColorToLineCells(board, board.Get(cellA).TypeIndex, kindB))
                {
                    yield return cell;
                }

                yield break;
            }

            if (kindB == SpecialKind.ColorBomb && aIsLine)
            {
                foreach (GridCell cell in ColorToLineCells(board, board.Get(cellB).TypeIndex, kindA))
                {
                    yield return cell;
                }

                yield break;
            }

            if (aIsRadius && bIsLine)
            {
                foreach (GridCell cell in ThickLineCells(board, cellB, kindB, RadiusBombRadius))
                {
                    yield return cell;
                }

                yield break;
            }

            if (bIsRadius && aIsLine)
            {
                foreach (GridCell cell in ThickLineCells(board, cellA, kindA, RadiusBombRadius))
                {
                    yield return cell;
                }

                yield break;
            }

            if (aIsRadius && kindB == SpecialKind.ColorBomb)
            {
                foreach (GridCell cell in ColorToRadiusCells(board, board.Get(cellB).TypeIndex, RadiusBombRadius))
                {
                    yield return cell;
                }

                yield break;
            }

            if (bIsRadius && kindA == SpecialKind.ColorBomb)
            {
                foreach (GridCell cell in ColorToRadiusCells(board, board.Get(cellA).TypeIndex, RadiusBombRadius))
                {
                    yield return cell;
                }
            }
        }
    }
}
