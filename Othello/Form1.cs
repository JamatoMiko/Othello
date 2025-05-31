using System.Threading.Tasks;
using System.Diagnostics;
using Timer = System.Windows.Forms.Timer;

namespace Othello;

public partial class Form1 : Form
{
    Random rng = new Random();//乱数発生器
    public int CellSize { get; set; } = 48;
    public int PlayerColor { get; set; } = Board.BLACK;
    Board currentBoard;
    ListBox historyListBox;
    public Form1()
    {
        InitializeComponent();
        var startBoard = new int[,]{
            {0, 0, 0, 0, 0, 0, 0, 0},
            {0, 0, 0, 0, 0, 0, 0, 0},
            {0, 0, 0, 0, 0, 0, 0, 0},
            {0, 0, 0, 2, 1, 0, 0, 0},
            {0, 0, 0, 1, 2, 0, 0, 0},
            {0, 0, 0, 0, 0, 0, 0, 0},
            {0, 0, 0, 0, 0, 0, 0, 0},
            {0, 0, 0, 0, 0, 0, 0, 0},
        };
        currentBoard = new Board(startBoard, Board.BLACK);
        currentBoard.Original = true;

        historyListBox = new()
        {
            Location = new Point(currentBoard.Cols * CellSize, 0),
            Size = new Size(8 * 8, currentBoard.Rows * CellSize)
        };
        Controls.Add(historyListBox);

        if (PlayerColor != Board.BLACK)
            _ = HandleAITurn();

        DoubleBuffered = true;
        Invalidate();
    }

    private async Task<bool> HandleAITurn()
    {
        List<((int Row, int Col) Position, int Score)> candidateCells = new();
        for (int row = 0; row < currentBoard.Rows; row++)
        {
            for (int col = 0; col < currentBoard.Cols; col++)
            {
                var count = currentBoard.StonesToBeChanged(row, col, out int score, out _).Count;
                if (count > 0)
                {
                    candidateCells.Add(((row, col), score));
                }
            }
        }
        if (candidateCells.Count == 0)
        {
            AddHistory(currentBoard.Turn);
            currentBoard.ChangeTurn();
            await Task.Delay(1000);//ミリ秒
            return false;
        }
        //スコアの高い順にソート
        var changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < candidateCells.Count - 1; i++)
            {
                var cursor = candidateCells[i];
                var next = candidateCells[i + 1];
                if (cursor.Score < next.Score)
                {
                    candidateCells[i] = next;
                    candidateCells[i + 1] = cursor;
                    changed = true;
                }
                else if (cursor.Score == next.Score)
                {
                    if (rng.NextDouble() < 0.5)//スコアが同じ場合50で入れ替える
                    {
                        candidateCells[i] = next;
                        candidateCells[i + 1] = cursor;
                        changed = true;
                    }
                }
            }
        }
        await Task.Delay(100 + candidateCells.Count * 100);//ミリ秒
        AddHistory(currentBoard.Turn, candidateCells[0].Position.Row, candidateCells[0].Position.Col);
        currentBoard.PlaceStone(candidateCells[0].Position.Row, candidateCells[0].Position.Col);
        Invalidate();
        //プレイヤーが石を置けるか調べる
        var canPlace = false;
        for (int row = 0; row < currentBoard.Rows; row++)
        {
            for (int col = 0; col < currentBoard.Cols; col++)
            {
                var count = currentBoard.StonesToBeChanged(row, col, out _, out _).Count;
                if (count > 0)
                {
                    canPlace = true;
                    break;
                }
            }
        }
        if (!canPlace)
        {
            AddHistory(currentBoard.Turn);
            currentBoard.ChangeTurn();
            _ = HandleAITurn();
        }
        return true;
    }

    private void AddHistory(int turn, int row = -1, int col = -1)
    {
        var itemText = "";
        itemText += turn switch { Board.BLACK => " B: ", Board.WHITE => "W: ", _ => "N: " };
        if (row == -1 && col == -1)
            itemText += "PASS";
        else
            itemText += $"{Board.Columns[col]}{row + 1}";
        historyListBox.Items.Add($"{itemText}");
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var button = e.Button;
        var mx = e.Location.X;
        var my = e.Location.Y;
        if (button == MouseButtons.Left)
        {
            var row = my / CellSize;
            var col = mx / CellSize;
            if (currentBoard.IsInBounds(row, col))
            {
                if (currentBoard.Turn == PlayerColor)
                {
                    if (currentBoard.StonesToBeChanged(row, col, out _, out _).Count > 0)
                    {
                        AddHistory(currentBoard.Turn, row, col);
                        currentBoard.PlaceStone(row, col);
                        Invalidate();
                        _ = HandleAITurn();
                    }
                }
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.FillRectangle(new SolidBrush(Color.Black), 0, 0, currentBoard.Cols * CellSize, currentBoard.Rows * CellSize);
        for (int row = 0; row < currentBoard.Rows; row++)
        {
            for (int col = 0; col < currentBoard.Cols; col++)
            {
                var rect = new Rectangle(col * CellSize + 1, row * CellSize + 1, CellSize - 2, CellSize - 2);
                g.FillRectangle(new SolidBrush(Color.Green), rect);

                if (row == 0)
                    g.DrawString($"{Board.Columns[col]}", new Font("Arial", CellSize / 4), new SolidBrush(Color.Black), col * CellSize + CellSize / 4, row * CellSize);
                if (col == 0)
                    g.DrawString($"{row + 1}", new Font("Arial", CellSize / 4), new SolidBrush(Color.Black), col * CellSize, row * CellSize + CellSize / 4);

                if (currentBoard.Stones[row, col] == Board.BLACK)
                {
                    g.FillEllipse(new SolidBrush(Color.Black), rect);
                }
                else if (currentBoard.Stones[row, col] == Board.WHITE)
                {
                    g.FillEllipse(new SolidBrush(Color.White), rect);
                }

                var count = currentBoard.StonesToBeChanged(row, col, out int score, out int openness).Count;
                if (count > 0)
                {
                    g.FillEllipse(new SolidBrush(currentBoard.Turn switch { Board.BLACK => Color.FromArgb(20, 50, 20), Board.WHITE => Color.FromArgb(50, 200, 50), _ => throw new NotImplementedException() }), rect);
                    g.DrawEllipse(new Pen(Color.Black), rect);
                    //g.DrawString($"{score}", new Font("Arial", CellSize / 4), new SolidBrush(Color.Red), col * CellSize, row * CellSize);
                    //g.DrawString($"{openness}%", new Font("Arial", CellSize / 4), new SolidBrush(Color.Blue), col * CellSize, row * CellSize + CellSize / 4);
                }
                //g.DrawString($"{currentBoard.Weight[row, col]}", new Font("Arial", CellSize / 4), new SolidBrush(Color.Red), col * CellSize, row * CellSize);
            }
        }
    }
}
