package chess

import (
	"encoding/json"
	"fmt"
	"strings"
)

// Piece constants use standard algebraic notation:
//
//	uppercase = white, lowercase = black
//	K/k = king, Q/q = queen, R/r = rook, B/b = bishop, N/n = knight, P/p = pawn
//	"" = empty square
const (
	WhiteKing   = "K"
	WhiteQueen  = "Q"
	WhiteRook   = "R"
	WhiteBishop = "B"
	WhiteKnight = "N"
	WhitePawn   = "P"
	BlackKing   = "k"
	BlackQueen  = "q"
	BlackRook   = "r"
	BlackBishop = "b"
	BlackKnight = "n"
	BlackPawn   = "p"
	Empty       = ""
)

// Board is an 8×8 chess board. board[0] is rank 8 (black side), board[7] is rank 1 (white side).
// board[row][col] where col 0=a, col 7=h.
type Board [8][8]string

// NewBoard returns the standard starting position.
func NewBoard() Board {
	return Board{
		{BlackRook, BlackKnight, BlackBishop, BlackQueen, BlackKing, BlackBishop, BlackKnight, BlackRook},
		{BlackPawn, BlackPawn, BlackPawn, BlackPawn, BlackPawn, BlackPawn, BlackPawn, BlackPawn},
		{Empty, Empty, Empty, Empty, Empty, Empty, Empty, Empty},
		{Empty, Empty, Empty, Empty, Empty, Empty, Empty, Empty},
		{Empty, Empty, Empty, Empty, Empty, Empty, Empty, Empty},
		{Empty, Empty, Empty, Empty, Empty, Empty, Empty, Empty},
		{WhitePawn, WhitePawn, WhitePawn, WhitePawn, WhitePawn, WhitePawn, WhitePawn, WhitePawn},
		{WhiteRook, WhiteKnight, WhiteBishop, WhiteQueen, WhiteKing, WhiteBishop, WhiteKnight, WhiteRook},
	}
}

// Square represents a position on the board in algebraic notation (e.g. "e4").
type Square struct {
	Row int // 0-7, 0 = rank 8 (top)
	Col int // 0-7, 0 = file a (left)
}

// ParseSquare converts algebraic notation like "e4" to a Square.
func ParseSquare(s string) (Square, error) {
	if len(s) != 2 {
		return Square{}, fmt.Errorf("invalid square: %q", s)
	}
	col := int(s[0] - 'a')
	row := 8 - int(s[1]-'0')
	if col < 0 || col > 7 || row < 0 || row > 7 {
		return Square{}, fmt.Errorf("invalid square: %q", s)
	}
	return Square{Row: row, Col: col}, nil
}

// String returns the algebraic notation of the square.
func (sq Square) String() string {
	return fmt.Sprintf("%c%d", 'a'+sq.Col, 8-sq.Row)
}

// Get returns the piece at the given square.
func (b *Board) Get(sq Square) string {
	return b[sq.Row][sq.Col]
}

// Set places a piece at the given square.
func (b *Board) Set(sq Square, piece string) {
	b[sq.Row][sq.Col] = piece
}

// IsWhite returns true if the piece is white (uppercase).
func IsWhite(piece string) bool {
	return piece != "" && piece == strings.ToUpper(piece)
}

// IsBlack returns true if the piece is black (lowercase).
func IsBlack(piece string) bool {
	return piece != "" && piece == strings.ToLower(piece)
}

// ColorOf returns "white" or "black" for a piece, or "" for empty.
func ColorOf(piece string) string {
	if IsWhite(piece) {
		return "white"
	}
	if IsBlack(piece) {
		return "black"
	}
	return ""
}

// ToJSON serializes the board to a JSON string (8-element array of 8-element arrays).
func (b *Board) ToJSON() string {
	data, err := json.Marshal(b)
	if err != nil {
		return "[]"
	}
	return string(data)
}

// FindKing returns the square of the king for the given color.
func (b *Board) FindKing(color string) (Square, bool) {
	target := WhiteKing
	if color == "black" {
		target = BlackKing
	}
	for r := 0; r < 8; r++ {
		for c := 0; c < 8; c++ {
			if b[r][c] == target {
				return Square{Row: r, Col: c}, true
			}
		}
	}
	return Square{}, false
}

// Clone returns a deep copy of the board.
func (b *Board) Clone() Board {
	var copy Board
	for r := 0; r < 8; r++ {
		for c := 0; c < 8; c++ {
			copy[r][c] = b[r][c]
		}
	}
	return copy
}
