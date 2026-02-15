package chess

import (
	"strings"
)

// Move represents a chess move.
type Move struct {
	From Square
	To   Square
}

// ValidateMoveForColor checks if a move is legal for the given color.
// It checks:
//  1. The piece at 'from' belongs to the moving color
//  2. The piece can move to 'to' by its movement rules
//  3. The resulting position does not leave the king in check
func ValidateMoveForColor(b *Board, from, to Square, color string) error {
	piece := b.Get(from)
	if piece == Empty {
		return errNoPiece
	}
	if ColorOf(piece) != color {
		return errWrongColor
	}

	// Cannot capture own piece
	target := b.Get(to)
	if target != Empty && ColorOf(target) == color {
		return errCaptureOwn
	}

	// Check piece-specific movement
	if !isValidPieceMove(b, from, to, piece) {
		return errIllegalMove
	}

	// Simulate the move and check if king is still in check
	simulated := b.Clone()
	simulated.Set(to, piece)
	simulated.Set(from, Empty)
	if IsInCheck(&simulated, color) {
		return errKingInCheck
	}

	return nil
}

// isValidPieceMove checks if the piece at 'from' can reach 'to' on board 'b'.
func isValidPieceMove(b *Board, from, to Square, piece string) bool {
	upper := strings.ToUpper(piece)
	switch upper {
	case "P":
		return isValidPawnMove(b, from, to, piece)
	case "R":
		return isValidRookMove(b, from, to)
	case "N":
		return isValidKnightMove(from, to)
	case "B":
		return isValidBishopMove(b, from, to)
	case "Q":
		return isValidQueenMove(b, from, to)
	case "K":
		return isValidKingMove(from, to)
	}
	return false
}

// isValidPawnMove handles pawn movement including initial two-square advance and diagonal capture.
func isValidPawnMove(b *Board, from, to Square, piece string) bool {
	dir := -1 // white moves up (decreasing row)
	startRow := 6
	if IsBlack(piece) {
		dir = 1 // black moves down (increasing row)
		startRow = 1
	}

	dr := to.Row - from.Row
	dc := to.Col - from.Col

	// Forward one square
	if dc == 0 && dr == dir && b.Get(to) == Empty {
		return true
	}

	// Forward two squares from starting position
	if dc == 0 && dr == 2*dir && from.Row == startRow {
		intermediate := Square{Row: from.Row + dir, Col: from.Col}
		if b.Get(intermediate) == Empty && b.Get(to) == Empty {
			return true
		}
	}

	// Diagonal capture
	if abs(dc) == 1 && dr == dir {
		target := b.Get(to)
		if target != Empty && ColorOf(target) != ColorOf(piece) {
			return true
		}
	}

	return false
}

// isValidRookMove validates rook movement (horizontal/vertical, no jumping).
func isValidRookMove(b *Board, from, to Square) bool {
	if from.Row != to.Row && from.Col != to.Col {
		return false
	}
	return isPathClear(b, from, to)
}

// isValidKnightMove validates knight movement (L-shape).
func isValidKnightMove(from, to Square) bool {
	dr := abs(to.Row - from.Row)
	dc := abs(to.Col - from.Col)
	return (dr == 2 && dc == 1) || (dr == 1 && dc == 2)
}

// isValidBishopMove validates bishop movement (diagonal, no jumping).
func isValidBishopMove(b *Board, from, to Square) bool {
	dr := abs(to.Row - from.Row)
	dc := abs(to.Col - from.Col)
	if dr != dc || dr == 0 {
		return false
	}
	return isPathClear(b, from, to)
}

// isValidQueenMove validates queen movement (rook + bishop).
func isValidQueenMove(b *Board, from, to Square) bool {
	return isValidRookMove(b, from, to) || isValidBishopMove(b, from, to)
}

// isValidKingMove validates king movement (one square in any direction).
func isValidKingMove(from, to Square) bool {
	dr := abs(to.Row - from.Row)
	dc := abs(to.Col - from.Col)
	return dr <= 1 && dc <= 1 && (dr+dc > 0)
}

// isPathClear checks that no pieces block the path between from and to (exclusive).
func isPathClear(b *Board, from, to Square) bool {
	dr := sign(to.Row - from.Row)
	dc := sign(to.Col - from.Col)

	r, c := from.Row+dr, from.Col+dc
	for r != to.Row || c != to.Col {
		if b[r][c] != Empty {
			return false
		}
		r += dr
		c += dc
	}
	return true
}

// IsInCheck returns true if the given color's king is under attack.
func IsInCheck(b *Board, color string) bool {
	kingSq, found := b.FindKing(color)
	if !found {
		return false
	}

	enemyColor := "black"
	if color == "black" {
		enemyColor = "white"
	}

	// Check if any enemy piece can move to the king's square
	for r := 0; r < 8; r++ {
		for c := 0; c < 8; c++ {
			piece := b[r][c]
			if piece == Empty || ColorOf(piece) != enemyColor {
				continue
			}
			from := Square{Row: r, Col: c}
			if isValidPieceMove(b, from, kingSq, piece) {
				return true
			}
		}
	}
	return false
}

// HasAnyLegalMove returns true if the given color has at least one legal move.
func HasAnyLegalMove(b *Board, color string) bool {
	for r := 0; r < 8; r++ {
		for c := 0; c < 8; c++ {
			piece := b[r][c]
			if piece == Empty || ColorOf(piece) != color {
				continue
			}
			from := Square{Row: r, Col: c}
			for tr := 0; tr < 8; tr++ {
				for tc := 0; tc < 8; tc++ {
					to := Square{Row: tr, Col: tc}
					if from == to {
						continue
					}
					if ValidateMoveForColor(b, from, to, color) == nil {
						return true
					}
				}
			}
		}
	}
	return false
}

// GameStatus returns the game status for the given turn color:
//
//	"playing"   — normal play
//	"check"     — current player is in check but has legal moves
//	"checkmate" — current player is in checkmate (opponent wins)
//	"stalemate" — current player has no legal moves but is not in check
func GameStatus(b *Board, turnColor string) string {
	inCheck := IsInCheck(b, turnColor)
	hasMove := HasAnyLegalMove(b, turnColor)

	if inCheck && !hasMove {
		return "checkmate"
	}
	if !inCheck && !hasMove {
		return "stalemate"
	}
	if inCheck {
		return "check"
	}
	return "playing"
}

// Helper functions

func abs(x int) int {
	if x < 0 {
		return -x
	}
	return x
}

func sign(x int) int {
	if x > 0 {
		return 1
	}
	if x < 0 {
		return -1
	}
	return 0
}

// Sentinel errors for move validation
type moveError string

func (e moveError) Error() string { return string(e) }

const (
	errNoPiece    moveError = "no piece at source square"
	errWrongColor moveError = "piece does not belong to you"
	errCaptureOwn moveError = "cannot capture your own piece"
	errIllegalMove moveError = "illegal move for this piece"
	errKingInCheck moveError = "move would leave king in check"
)
