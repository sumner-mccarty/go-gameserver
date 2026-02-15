// ChessUnityClient/GameState/ChessBoard.cs
// Manages the logical chess board state received from the server.
// Parses the server's JSON board into a usable 2D array and provides
// helper methods for square selection, legal move highlighting, and notation.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessUnityClient
{
    /// <summary>
    /// Represents the client-side chess board state, parsed from server data.
    /// This is a "view model" — the server is the source of truth.
    /// </summary>
    public class ChessBoard
    {
        /// <summary>Board[row][col] — row 0 = rank 1 (white's side), col 0 = file 'a'.</summary>
        public string[,] Squares { get; private set; } = new string[8, 8];

        public string Turn { get; private set; } = "white";
        public string Status { get; private set; } = "playing";
        public int MoveCount { get; private set; }
        public int GameID { get; private set; }
        public int WhiteID { get; private set; }
        public int BlackID { get; private set; }
        public List<string> MoveHistory { get; private set; } = new List<string>();

        /// <summary>The currently selected square (algebraic notation), or null.</summary>
        public string SelectedSquare { get; set; }

        /// <summary>
        /// Updates the board state from server data.
        /// </summary>
        public void UpdateFromServer(ChessGameData data)
        {
            GameID = data.id;
            WhiteID = data.white_id;
            BlackID = data.black_id;
            Turn = data.turn;
            Status = data.status;
            MoveCount = data.move_count;

            // Parse the board JSON: a JSON array of 8 arrays of 8 strings
            // e.g. [["R","N","B",...],["P","P",...], ...]
            ParseBoard(data.board);
            ParseMoveHistory(data.move_history);
        }

        /// <summary>
        /// Gets the piece at a given algebraic square (e.g. "e2").
        /// Returns empty string if no piece.
        /// </summary>
        public string GetPieceAt(string square)
        {
            var (row, col) = AlgebraicToIndex(square);
            if (row < 0) return "";
            return Squares[row, col] ?? "";
        }

        /// <summary>
        /// Gets the piece at row,col. Returns "" if empty.
        /// </summary>
        public string GetPieceAt(int row, int col)
        {
            if (row < 0 || row > 7 || col < 0 || col > 7) return "";
            return Squares[row, col] ?? "";
        }

        /// <summary>
        /// Returns true if the piece at the given square belongs to the given color.
        /// </summary>
        public bool IsPieceOwnedBy(int row, int col, string color)
        {
            string piece = GetPieceAt(row, col);
            if (string.IsNullOrEmpty(piece)) return false;
            bool isWhitePiece = char.IsUpper(piece[0]);
            return (color == "white") == isWhitePiece;
        }

        /// <summary>
        /// Converts algebraic notation (e.g. "e2") to (row, col) indices.
        /// Returns (-1, -1) on invalid input.
        /// </summary>
        public static (int row, int col) AlgebraicToIndex(string square)
        {
            if (string.IsNullOrEmpty(square) || square.Length != 2)
                return (-1, -1);
            int col = square[0] - 'a';
            int row = square[1] - '1';
            if (col < 0 || col > 7 || row < 0 || row > 7)
                return (-1, -1);
            return (row, col);
        }

        /// <summary>
        /// Converts (row, col) to algebraic notation.
        /// </summary>
        public static string IndexToAlgebraic(int row, int col)
        {
            if (row < 0 || row > 7 || col < 0 || col > 7) return "";
            return $"{(char)('a' + col)}{row + 1}";
        }

        /// <summary>
        /// Returns the display character for a piece code.
        /// Uppercase = white, lowercase = black.
        /// K=King, Q=Queen, R=Rook, B=Bishop, N=Knight, P=Pawn
        /// </summary>
        public static string PieceDisplayName(string pieceCode)
        {
            if (string.IsNullOrEmpty(pieceCode)) return "";
            switch (pieceCode.ToUpper())
            {
                case "K": return "King";
                case "Q": return "Queen";
                case "R": return "Rook";
                case "B": return "Bishop";
                case "N": return "Knight";
                case "P": return "Pawn";
                default: return pieceCode;
            }
        }

        /// <summary>
        /// Returns true if the game is still in progress.
        /// </summary>
        public bool IsGameActive => Status == "playing" || Status == "check";

        // ───────────────────────── Parsing ─────────────────────────

        private void ParseBoard(string boardJson)
        {
            // The server sends a JSON 2D array: [["R","N",...], ["P","P",...], ...]
            // Unity's JsonUtility can't handle 2D arrays natively, so we parse manually.
            if (string.IsNullOrEmpty(boardJson))
            {
                Squares = new string[8, 8];
                return;
            }

            try
            {
                // Wrap in object for JsonUtility: {"rows": [...]}
                string wrapped = "{\"rows\":" + boardJson + "}";
                var parsed = JsonUtility.FromJson<BoardWrapper>(wrapped);
                if (parsed?.rows != null)
                {
                    for (int r = 0; r < 8 && r < parsed.rows.Length; r++)
                    {
                        string rowStr = parsed.rows[r];
                        // Each row is itself a JSON array: ["R","N","B",...]
                        // Parse it manually
                        var cells = ParseJsonStringArray(rowStr);
                        for (int c = 0; c < 8 && c < cells.Length; c++)
                        {
                            Squares[r, c] = cells[c];
                        }
                    }
                }
            }
            catch
            {
                // Fallback: try simple manual parse
                ParseBoardManual(boardJson);
            }
        }

        private void ParseBoardManual(string boardJson)
        {
            // Simple manual parser for [["R","N",...],...]
            Squares = new string[8, 8];
            int row = 0, col = 0;
            bool inString = false;
            var current = new System.Text.StringBuilder();
            int depth = 0;

            foreach (char c in boardJson)
            {
                if (c == '[')
                {
                    depth++;
                    continue;
                }
                if (c == ']')
                {
                    if (depth == 2 && inString)
                    {
                        if (row < 8 && col < 8)
                            Squares[row, col] = current.ToString();
                        current.Clear();
                        inString = false;
                        col++;
                    }
                    depth--;
                    if (depth == 1)
                    {
                        row++;
                        col = 0;
                    }
                    continue;
                }
                if (c == '"')
                {
                    if (inString)
                    {
                        if (row < 8 && col < 8)
                            Squares[row, col] = current.ToString();
                        current.Clear();
                        inString = false;
                        col++;
                    }
                    else
                    {
                        current.Clear();
                        inString = true;
                    }
                    continue;
                }
                if (c == ',' && !inString)
                    continue;
                if (inString)
                    current.Append(c);
            }
        }

        private void ParseMoveHistory(string historyJson)
        {
            MoveHistory.Clear();
            if (string.IsNullOrEmpty(historyJson)) return;

            try
            {
                var moves = ParseJsonStringArray(historyJson);
                MoveHistory.AddRange(moves);
            }
            catch { }
        }

        /// <summary>
        /// Parses a flat JSON string array like ["a","b","c"].
        /// </summary>
        private static string[] ParseJsonStringArray(string json)
        {
            var result = new List<string>();
            bool inString = false;
            var current = new System.Text.StringBuilder();

            foreach (char c in json)
            {
                if (c == '"')
                {
                    if (inString)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                    inString = !inString;
                    continue;
                }
                if (inString)
                    current.Append(c);
            }

            return result.ToArray();
        }

        [Serializable]
        private class BoardWrapper
        {
            public string[] rows;
        }
    }
}
