using System.Diagnostics;
using System.Linq;

namespace Othello;

//マスの評価点
//打つマスのウェイトを最後に掛ける
//「開放度」打ったことで裏返るマスの周囲の空きマスの数
//DONE Boardを再帰的に作成して先を読むようにする
//TODO 次のターンに相手に角を取られるようなマスの評価点を下げる
//DONE 次ターンで相手が置けるマスを数えて多いほど評価点を下げる
//TODO 置けるマスが無くなったらゲームオーバー
//TODO 確定石を見つける、ウェイトをEDGE_WEIGHTに設定
//TODO 終盤で「ウィング」に気を付ける
//DONE プレイヤーが白のときにコンピューターがゲームを始めるようにする

public class Board
{
    public bool Original { get; set; }
    public const int NONE = 0;
    public const int BLACK = 1;
    public const int WHITE = 2;
    public Stage CurrentStage { get; set; } = Stage.EARLY_GAME;
    int[,] multipliers ={//回転行列
        { 1,  1,  0, -1, -1, -1,  0,  1},//cosA
        { 0,  1, -1,  1,  0, -1,  1, -1},//-sinA
        { 0, -1,  1, -1,  0,  1, -1,  1},//sinA
        { 1,  1,  0, -1, -1, -1,  0,  1}//cosA
    };
    public int TotalCells { get; set; }
    private int totalStoneCount;
    public int TotalStoneCount
    {
        get => totalStoneCount;
        set
        {
            totalStoneCount = value;
            if (totalStoneCount == TotalCells)
            {
                CurrentStage = Stage.GAME_OVER;
                Debug.WriteLine($"GAME_OVER B{StoneCount[BLACK]}:W{StoneCount[WHITE]}");
            }
            else if (totalStoneCount >= TotalCells * 3 / 4)
            {
                CurrentStage = Stage.LATE_GAME;
                //Debug.WriteLine("LATE_GAME");
            }
            else if (totalStoneCount >= TotalCells / 2)
            {
                CurrentStage = Stage.MIDDLE_GAME;
                //Debug.WriteLine("MIDDLE_GAME");
            }
            else
            {
                CurrentStage = Stage.EARLY_GAME;
                //Debug.WriteLine("EARLY_GAME");
            }
        }
    }
    public int[] StoneCount { get; set; } = new int[3];
    private int rows;
    public int Rows
    {
        get => rows;
        set
        {
            rows = value;
            TotalCells = rows * Cols;
        }
    }
    private int cols;
    public int Cols
    {
        get => cols;
        set
        {
            cols = value;
            TotalCells = Rows * cols;
        }
    }
    public int[,] Stones { get; set; }
    public int[,] Weight { get; set; }
    private const int BASE_WEIGHT = 1;//基本のウェイト
    private const int CORNER_WEIGHT = 100;//角のウェイト
    private const int EDGE_WEIGHT = 10;//端のウェイト
    private const int AROUND_CORNER_WEIGHT = -10;//角周辺のウェイト
    private int turn;
    public int Turn
    {
        get => turn;
        set
        {
            turn = value;
            Opponent = OppositeColor(turn);
        }
    }
    public int Opponent { get; set; }

    public Board(int rows, int cols, int turn)
    {
        Rows = rows;
        Cols = cols;
        Stones = new int[Rows, Cols];
        CountStones();
        SetWeight();
        Turn = turn;
    }
    public Board(int[,] stones, int turn)
    {
        Rows = stones.GetLength(0);//行数を取得
        Cols = stones.GetLength(1);//列数を取得
        Stones = new int[Rows, Cols];
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                Stones[row, col] = stones[row, col];//要素をコピー
            }
        }
        CountStones();
        SetWeight();
        Turn = turn;
    }

    public void SetWeight()
    {
        Weight = new int[Rows, Cols];
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                int weight = BASE_WEIGHT;
                if (row == 0 || row == Rows - 1)
                {
                    weight = EDGE_WEIGHT;
                    if (col == 0 || col == Cols - 1)
                        weight = CORNER_WEIGHT;
                    else if (col == 1 || col == Cols - 2)
                        weight = AROUND_CORNER_WEIGHT;
                }
                else if (row == 1 || row == Rows - 2)
                {
                    if (col == 0 || col == 1 || col == Cols - 1 || col == Cols - 2)
                        weight = AROUND_CORNER_WEIGHT;
                }
                else if (col == 0 || col == Cols - 1)
                {
                    weight = EDGE_WEIGHT;
                }
                Weight[row, col] = weight;
            }
        }
    }

    public List<(int, int)> StonesToBeChanged(int row, int col, out int score, out int openness)
    {
        List<(int Row, int Col)> totalStones = new();
        score = 0;//評価点
        openness = 0;//開放度

        if (Stones[row, col] != NONE)
            return totalStones;

        List<(int Row, int Col)> totalCellsAround = new();
        List<(int Row, int Col)> totalEmptyCells = new();
        for (int i = 0; i < 8; i++)
        {
            List<(int Row, int Col)> stones = new();
            List<(int Row, int Col)> cellsAround = new();
            List<(int Row, int Col)> emptyCells = new();
            for (int d = 1; d < MathF.Max(Rows, Cols); d++)
            {
                var dx = d;
                var dy = 0;
                var ax = dx * multipliers[0, i] + dy * multipliers[1, i];
                var ay = dx * multipliers[2, i] + dy * multipliers[3, i];
                var currentCol = col + ax;
                var currentRow = row + ay;
                if (!IsInBounds(currentRow, currentCol))
                    break;
                if (Stones[currentRow, currentCol] == NONE)
                    break;
                if (Stones[currentRow, currentCol] == Turn)
                {
                    if (stones.Count > 0)
                    {
                        totalStones.AddRange(stones);
                        totalCellsAround.AddRange(cellsAround);
                        totalEmptyCells.AddRange(emptyCells);
                    }
                    break;
                }
                if (Stones[currentRow, currentCol] == Opponent)
                {
                    stones.Add((currentRow, currentCol));
                    //周囲8マスの空いているマスを調べる
                    for (int j = 0; j < 8; j++)
                    {
                        var sax = 1 * multipliers[0, j] + 0 * multipliers[1, j];
                        var say = 1 * multipliers[2, j] + 0 * multipliers[3, j];
                        var cc = currentCol + sax;
                        var cr = currentRow + say;
                        if (!IsInBounds(cr, cc))
                            continue;
                        if (cc == col && cr == row)
                            continue;
                        cellsAround.Add((cr, cc));
                        if (Stones[cr, cc] == NONE)
                        {
                            emptyCells.Add((cr, cc));
                        }
                    }
                }
            }
        }
        //置く石の周りの開放度も調べる
        for (int i = 0; i < 8; i++)
        {
            var dx = 1 * multipliers[0, i] + 0 * multipliers[1, i];
            var dy = 1 * multipliers[2, i] + 0 * multipliers[3, i];
            var currentCol = col + dx;
            var currentRow = row + dy;
            if (!IsInBounds(currentRow, currentCol))
                continue;
            totalCellsAround.Add((currentRow, currentCol));
            if (Stones[currentRow, currentCol] == NONE)
            {
                totalEmptyCells.Add((currentRow, currentCol));
            }
        }
        totalCellsAround.RemoveAll(totalStones.Contains);//裏返す石があるマスを取り除く
        var uniqueCellsAround = totalCellsAround.Distinct();//リストの重複を削除
        var uniqueEmptyCells = totalEmptyCells.Distinct();//リストの重複を削除
        if (totalStones.Count > 0)
        {
            openness = uniqueEmptyCells.Count() * 100 / uniqueCellsAround.Count();//開放度を計算
            switch (CurrentStage)
            {
                case Stage.EARLY_GAME://序盤は
                    score += 100 - openness;//開放度が低いマスほど評価点が高い
                    score -= totalStones.Count * 10;//取る石が少ない方が評価点が高い
                    break;
                case Stage.MIDDLE_GAME:
                    score = totalStones.Count * 10;//取る石が多いほど評価点が高い
                    break;
                case Stage.LATE_GAME:
                    score = totalStones.Count * 10;//取る石が多いほど評価点が高い
                    break;
            }
            if (Original)//無限に呼び出さないように、Original = trueのBoardからだけ呼び出す
            {
                //次のターンの盤面を作成
                var nextBoard = new Board(Stones, Turn);
                //石を置く
                nextBoard.PlaceStone(row, col);
                //置けるマスの数を数える
                var possibleCellCount = 0;
                for (int r = 0; r < nextBoard.Rows; r++)
                {
                    for (int c = 0; c < nextBoard.Cols; c++)
                    {
                        if (nextBoard.StonesToBeChanged(r, c, out _, out _).Count > 0)
                        {
                            possibleCellCount++;
                        }
                    }
                }
                score -= possibleCellCount;//置けるマスが多いほど評価点が下がる
            }
            score = score * Weight[row, col];//マスのウェイトを掛ける
        }
        return totalStones;
    }

    public void PlaceStone(int row, int col)
    {
        var stones = StonesToBeChanged(row, col, out _, out _);
        if (stones.Count > 0)
        {
            Stones[row, col] = Turn;
            foreach (var stone in stones)
            {
                Stones[stone.Item1, stone.Item2] = Turn;
            }
            //角に置いた場合、角の周囲のマス（斜め以外）は確定石になる、ウェイトを変更
            if (row == 0 || row == Rows - 1)
            {
                if (col == 0 || col == Cols - 1)
                {
                    if (IsInBounds(row, col + 1))
                        Weight[row, col + 1] = EDGE_WEIGHT;
                    if (IsInBounds(row - 1, col))
                        Weight[row - 1, col] = EDGE_WEIGHT;
                    if (IsInBounds(row, col - 1))
                        Weight[row, col - 1] = EDGE_WEIGHT;
                    if (IsInBounds(row + 1, col))
                        Weight[row + 1, col] = EDGE_WEIGHT;
                }
            }
            CountStones();
            ChangeTurn();
        }
    }

    public void ChangeTurn()
    {
        Turn = Opponent;
    }

    public static int OppositeColor(int color)
    {
        if (color == BLACK)
            return WHITE;
        else if (color == WHITE)
            return BLACK;
        else
            return NONE;
    }

    public void CountStones()
    {
        StoneCount[NONE] = 0;
        StoneCount[BLACK] = 0;
        StoneCount[WHITE] = 0;
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                if (Stones[row, col] == NONE)
                    StoneCount[NONE]++;
                else if (Stones[row, col] == BLACK)
                    StoneCount[BLACK]++;
                else if (Stones[row, col] == WHITE)
                    StoneCount[WHITE]++;
            }
        }
        TotalStoneCount = StoneCount[BLACK] + StoneCount[WHITE];
    }
    public bool IsInBounds(int row, int col) =>
        row >= 0 && row < Rows && col >= 0 && col < Cols;
}