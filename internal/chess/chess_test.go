package chess

import (
	"bytes"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"

	"github.com/gorilla/mux"
)

// ─────────────────────── Board Tests ───────────────────────

func TestNewBoard(t *testing.T) {
	b := NewBoard()

	// White pieces on rank 1 (row 7)
	if b[7][0] != WhiteRook {
		t.Errorf("expected white rook at a1, got %q", b[7][0])
	}
	if b[7][4] != WhiteKing {
		t.Errorf("expected white king at e1, got %q", b[7][4])
	}

	// Black pieces on rank 8 (row 0)
	if b[0][0] != BlackRook {
		t.Errorf("expected black rook at a8, got %q", b[0][0])
	}
	if b[0][4] != BlackKing {
		t.Errorf("expected black king at e8, got %q", b[0][4])
	}

	// White pawns on rank 2 (row 6)
	for c := 0; c < 8; c++ {
		if b[6][c] != WhitePawn {
			t.Errorf("expected white pawn at col %d row 6, got %q", c, b[6][c])
		}
	}

	// Empty squares in middle
	for r := 2; r <= 5; r++ {
		for c := 0; c < 8; c++ {
			if b[r][c] != Empty {
				t.Errorf("expected empty at row %d col %d, got %q", r, c, b[r][c])
			}
		}
	}
}

func TestParseSquare(t *testing.T) {
	tests := []struct {
		input   string
		wantRow int
		wantCol int
		wantErr bool
	}{
		{"a1", 7, 0, false},
		{"h8", 0, 7, false},
		{"e4", 4, 4, false},
		{"d7", 1, 3, false},
		{"", 0, 0, true},
		{"z9", 0, 0, true},
		{"abc", 0, 0, true},
	}

	for _, tt := range tests {
		t.Run(tt.input, func(t *testing.T) {
			sq, err := ParseSquare(tt.input)
			if tt.wantErr {
				if err == nil {
					t.Error("expected error")
				}
				return
			}
			if err != nil {
				t.Fatalf("unexpected error: %v", err)
			}
			if sq.Row != tt.wantRow || sq.Col != tt.wantCol {
				t.Errorf("ParseSquare(%q) = (%d,%d), want (%d,%d)", tt.input, sq.Row, sq.Col, tt.wantRow, tt.wantCol)
			}
		})
	}
}

func TestSquareString(t *testing.T) {
	sq := Square{Row: 4, Col: 4}
	if sq.String() != "e4" {
		t.Errorf("expected e4, got %s", sq.String())
	}
	sq = Square{Row: 7, Col: 0}
	if sq.String() != "a1" {
		t.Errorf("expected a1, got %s", sq.String())
	}
}

func TestColorOf(t *testing.T) {
	if ColorOf(WhiteKing) != "white" {
		t.Error("expected white for K")
	}
	if ColorOf(BlackKing) != "black" {
		t.Error("expected black for k")
	}
	if ColorOf(Empty) != "" {
		t.Error("expected empty for empty")
	}
}

func TestFindKing(t *testing.T) {
	b := NewBoard()
	sq, found := b.FindKing("white")
	if !found {
		t.Fatal("white king not found")
	}
	if sq.String() != "e1" {
		t.Errorf("expected white king at e1, got %s", sq.String())
	}

	sq, found = b.FindKing("black")
	if !found {
		t.Fatal("black king not found")
	}
	if sq.String() != "e8" {
		t.Errorf("expected black king at e8, got %s", sq.String())
	}
}

func TestBoardToJSON(t *testing.T) {
	b := NewBoard()
	jsonStr := b.ToJSON()
	if jsonStr == "" {
		t.Error("expected non-empty JSON")
	}

	// Should be valid JSON
	var parsed [8][8]string
	if err := json.Unmarshal([]byte(jsonStr), &parsed); err != nil {
		t.Fatalf("invalid JSON: %v", err)
	}
	if parsed[7][4] != WhiteKing {
		t.Errorf("expected white king at e1 in parsed JSON, got %q", parsed[7][4])
	}
}

// ─────────────────────── Rules Tests ───────────────────────

func TestPawnMoveForward(t *testing.T) {
	b := NewBoard()
	// White pawn e2 to e3 (single forward)
	err := ValidateMoveForColor(&b, sq("e2"), sq("e3"), "white")
	if err != nil {
		t.Errorf("expected legal move e2-e3: %v", err)
	}

	// White pawn e2 to e4 (double forward from start)
	err = ValidateMoveForColor(&b, sq("e2"), sq("e4"), "white")
	if err != nil {
		t.Errorf("expected legal move e2-e4: %v", err)
	}
}

func TestPawnCannotMoveBackward(t *testing.T) {
	b := NewBoard()
	err := ValidateMoveForColor(&b, sq("e2"), sq("e1"), "white")
	if err == nil {
		t.Error("pawn should not move backward")
	}
}

func TestPawnCaptureDiagonal(t *testing.T) {
	b := emptyBoard()
	b.Set(sq("e4"), WhitePawn)
	b.Set(sq("d5"), BlackPawn)
	b.Set(sq("e8"), BlackKing) // need kings on board
	b.Set(sq("e1"), WhiteKing)

	err := ValidateMoveForColor(&b, sq("e4"), sq("d5"), "white")
	if err != nil {
		t.Errorf("pawn should capture diagonally: %v", err)
	}

	// Cannot capture forward
	b.Set(sq("e5"), BlackPawn)
	err = ValidateMoveForColor(&b, sq("e4"), sq("e5"), "white")
	if err == nil {
		t.Error("pawn should not move forward into occupied square")
	}
}

func TestRookMovement(t *testing.T) {
	b := emptyBoard()
	b.Set(sq("e2"), WhiteKing) // King out of the way of rank 1
	b.Set(sq("e8"), BlackKing)
	b.Set(sq("a1"), WhiteRook)

	// Horizontal
	err := ValidateMoveForColor(&b, sq("a1"), sq("h1"), "white")
	if err != nil {
		t.Errorf("rook should move horizontally: %v", err)
	}

	// Vertical
	err = ValidateMoveForColor(&b, sq("a1"), sq("a8"), "white")
	if err != nil {
		t.Errorf("rook should move vertically: %v", err)
	}

	// Cannot move diagonally
	err = ValidateMoveForColor(&b, sq("a1"), sq("c3"), "white")
	if err == nil {
		t.Error("rook should not move diagonally")
	}
}

func TestRookBlockedByPiece(t *testing.T) {
	b := emptyBoard()
	b.Set(sq("e2"), WhiteKing) // Out of the way
	b.Set(sq("e8"), BlackKing)
	b.Set(sq("a1"), WhiteRook)
	b.Set(sq("a4"), WhitePawn) // Blocking piece

	err := ValidateMoveForColor(&b, sq("a1"), sq("a8"), "white")
	if err == nil {
		t.Error("rook should be blocked by own piece")
	}
}

func TestKnightMovement(t *testing.T) {
	b := emptyBoard()
	b.Set(sq("e1"), WhiteKing)
	b.Set(sq("e8"), BlackKing)
	b.Set(sq("d4"), WhiteKnight)

	legalTargets := []string{"c6", "e6", "f5", "f3", "e2", "c2", "b3", "b5"}
	for _, target := range legalTargets {
		// Clone board each time since we're just checking validity
		err := ValidateMoveForColor(&b, sq("d4"), sq(target), "white")
		if err != nil {
			t.Errorf("knight d4-%s should be legal: %v", target, err)
		}
	}

	// Invalid knight move
	err := ValidateMoveForColor(&b, sq("d4"), sq("d5"), "white")
	if err == nil {
		t.Error("knight should not move one square forward")
	}
}

func TestBishopMovement(t *testing.T) {
	b := emptyBoard()
	b.Set(sq("e1"), WhiteKing)
	b.Set(sq("e8"), BlackKing)
	b.Set(sq("c1"), WhiteBishop)

	// Diagonal
	err := ValidateMoveForColor(&b, sq("c1"), sq("f4"), "white")
	if err != nil {
		t.Errorf("bishop should move diagonally: %v", err)
	}

	// Cannot move horizontally
	err = ValidateMoveForColor(&b, sq("c1"), sq("f1"), "white")
	if err == nil {
		t.Error("bishop should not move horizontally")
	}
}

func TestQueenMovement(t *testing.T) {
	b := emptyBoard()
	b.Set(sq("a1"), WhiteKing) // Corner, out of the way
	b.Set(sq("e8"), BlackKing)
	b.Set(sq("d4"), WhiteQueen) // Center queen for unobstructed movement

	// Horizontal
	err := ValidateMoveForColor(&b, sq("d4"), sq("h4"), "white")
	if err != nil {
		t.Errorf("queen should move horizontally: %v", err)
	}

	// Vertical
	err = ValidateMoveForColor(&b, sq("d4"), sq("d8"), "white")
	if err != nil {
		t.Errorf("queen should move vertically: %v", err)
	}

	// Diagonal
	err = ValidateMoveForColor(&b, sq("d4"), sq("g7"), "white")
	if err != nil {
		t.Errorf("queen should move diagonally: %v", err)
	}
}

func TestKingMovement(t *testing.T) {
	b := emptyBoard()
	b.Set(sq("e4"), WhiteKing)
	b.Set(sq("a8"), BlackKing)

	// One square in any direction
	legalTargets := []string{"d3", "d4", "d5", "e3", "e5", "f3", "f4", "f5"}
	for _, target := range legalTargets {
		err := ValidateMoveForColor(&b, sq("e4"), sq(target), "white")
		if err != nil {
			t.Errorf("king e4-%s should be legal: %v", target, err)
		}
	}

	// Cannot move two squares
	err := ValidateMoveForColor(&b, sq("e4"), sq("e6"), "white")
	if err == nil {
		t.Error("king should not move two squares")
	}
}

func TestCannotCaptureOwnPiece(t *testing.T) {
	b := emptyBoard()
	b.Set(sq("e1"), WhiteKing)
	b.Set(sq("e8"), BlackKing)
	b.Set(sq("d1"), WhiteQueen)
	b.Set(sq("d2"), WhitePawn) // Own piece

	err := ValidateMoveForColor(&b, sq("d1"), sq("d2"), "white")
	if err == nil {
		t.Error("should not capture own piece")
	}
}

func TestIsInCheck(t *testing.T) {
	b := emptyBoard()
	b.Set(sq("e1"), WhiteKing)
	b.Set(sq("e8"), BlackKing)
	b.Set(sq("e5"), BlackRook) // Rook attacks white king through e-file

	if !IsInCheck(&b, "white") {
		t.Error("white king should be in check from rook on e5")
	}
	if IsInCheck(&b, "black") {
		t.Error("black king should not be in check")
	}
}

func TestCheckmate(t *testing.T) {
	// Back-rank checkmate: white rook delivers mate on 8th rank
	b := emptyBoard()
	b.Set(sq("g8"), BlackKing) // King cornered
	b.Set(sq("f7"), BlackPawn) // Pawns block escape
	b.Set(sq("g7"), BlackPawn)
	b.Set(sq("h7"), BlackPawn)
	b.Set(sq("a8"), WhiteRook) // Rook delivers mate on 8th rank
	b.Set(sq("e1"), WhiteKing)

	status := GameStatus(&b, "black")
	if status != "checkmate" {
		t.Errorf("expected checkmate, got %s", status)
	}
}

func TestStalemate(t *testing.T) {
	b := emptyBoard()
	b.Set(sq("a8"), BlackKing)
	b.Set(sq("b6"), WhiteQueen)
	b.Set(sq("c7"), WhiteKing)

	// Black king has no legal moves but is not in check
	status := GameStatus(&b, "black")
	if status != "stalemate" {
		t.Errorf("expected stalemate, got %s", status)
	}
}

func TestMoveIntoCheck(t *testing.T) {
	b := emptyBoard()
	b.Set(sq("e1"), WhiteKing)
	b.Set(sq("e8"), BlackKing)
	b.Set(sq("a1"), BlackRook) // Controls rank 1

	// King should not be able to move to d1 (still on rank 1, attacked by rook)
	err := ValidateMoveForColor(&b, sq("e1"), sq("d1"), "white")
	if err == nil {
		t.Error("should not be able to move king into check")
	}

	// King can move to e2 (off rank 1)
	err = ValidateMoveForColor(&b, sq("e1"), sq("e2"), "white")
	if err != nil {
		t.Errorf("king should be able to escape to e2: %v", err)
	}
}

func TestPawnPromotion(t *testing.T) {
	svc := NewService()
	game, _ := svc.CreateGame(1, 1, 2)

	// Set up a promotion scenario
	svc.mu.Lock()
	game.Board = emptyBoard()
	game.Board.Set(sq("e1"), WhiteKing)
	game.Board.Set(sq("e8"), BlackKing)
	game.Board.Set(sq("a7"), WhitePawn) // One square from promotion
	game.BoardJSON = game.Board.ToJSON()
	svc.mu.Unlock()

	// Move pawn to promotion square
	result, err := svc.MakeMove(game.ID, 1, "a7", "a8")
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	// Should be promoted to queen
	promotedPiece := result.Board.Get(sq("a8"))
	if promotedPiece != WhiteQueen {
		t.Errorf("expected pawn to promote to queen, got %q", promotedPiece)
	}
}

// ─────────────────────── Service Tests ───────────────────────

func TestServiceCreateGame(t *testing.T) {
	svc := NewService()

	t.Run("valid game", func(t *testing.T) {
		game, err := svc.CreateGame(1, 1, 2)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if game.ID != 1 {
			t.Errorf("expected ID 1, got %d", game.ID)
		}
		if game.Turn != "white" {
			t.Errorf("expected turn white, got %s", game.Turn)
		}
		if game.Status != "playing" {
			t.Errorf("expected status playing, got %s", game.Status)
		}
	})

	t.Run("same player both sides", func(t *testing.T) {
		_, err := svc.CreateGame(1, 1, 1)
		if err == nil {
			t.Error("expected error for same player both sides")
		}
	})

	t.Run("missing player id", func(t *testing.T) {
		_, err := svc.CreateGame(1, 0, 2)
		if err == nil {
			t.Error("expected error for missing white_id")
		}
	})
}

func TestServiceMakeMove(t *testing.T) {
	svc := NewService()
	game, _ := svc.CreateGame(1, 1, 2)

	t.Run("valid opening move", func(t *testing.T) {
		result, err := svc.MakeMove(game.ID, 1, "e2", "e4")
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if result.Turn != "black" {
			t.Errorf("expected turn black after white's move, got %s", result.Turn)
		}
		if result.MoveCount != 1 {
			t.Errorf("expected move count 1, got %d", result.MoveCount)
		}
	})

	t.Run("wrong turn", func(t *testing.T) {
		// It's black's turn now, white tries to move
		_, err := svc.MakeMove(game.ID, 1, "d2", "d4")
		if err == nil {
			t.Error("expected error for wrong turn")
		}
	})

	t.Run("black's move", func(t *testing.T) {
		result, err := svc.MakeMove(game.ID, 2, "e7", "e5")
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if result.Turn != "white" {
			t.Errorf("expected turn white after black's move, got %s", result.Turn)
		}
	})

	t.Run("game not found", func(t *testing.T) {
		_, err := svc.MakeMove(999, 1, "e2", "e4")
		if err == nil {
			t.Error("expected error for non-existing game")
		}
	})

	t.Run("invalid square", func(t *testing.T) {
		_, err := svc.MakeMove(game.ID, 1, "z9", "e4")
		if err == nil {
			t.Error("expected error for invalid square")
		}
	})
}

func TestServiceResign(t *testing.T) {
	svc := NewService()
	game, _ := svc.CreateGame(1, 1, 2)

	t.Run("valid resign", func(t *testing.T) {
		result, err := svc.Resign(game.ID, 1)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if result.Status != "resigned" {
			t.Errorf("expected status resigned, got %s", result.Status)
		}
	})

	t.Run("resign after game over", func(t *testing.T) {
		_, err := svc.Resign(game.ID, 2)
		if err == nil {
			t.Error("expected error for resigning after game is over")
		}
	})

	t.Run("game not found", func(t *testing.T) {
		_, err := svc.Resign(999, 1)
		if err == nil {
			t.Error("expected error for non-existing game")
		}
	})

	t.Run("player not in game", func(t *testing.T) {
		game2, _ := svc.CreateGame(2, 3, 4)
		_, err := svc.Resign(game2.ID, 99)
		if err == nil {
			t.Error("expected error for player not in game")
		}
	})
}

func TestServiceGetGame(t *testing.T) {
	svc := NewService()
	game, _ := svc.CreateGame(1, 1, 2)

	t.Run("existing game", func(t *testing.T) {
		result, err := svc.GetGame(game.ID)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if result == nil {
			t.Fatal("expected game, got nil")
		}
		if result.ID != game.ID {
			t.Errorf("expected ID %d, got %d", game.ID, result.ID)
		}
	})

	t.Run("non-existing game", func(t *testing.T) {
		result, err := svc.GetGame(999)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if result != nil {
			t.Errorf("expected nil for non-existing game, got %+v", result)
		}
	})
}

// ─────────────────────── Handler Tests ───────────────────────

func newTestChessRouter() *mux.Router {
	svc := NewService()
	h := NewHandler(svc)
	r := mux.NewRouter()
	h.RegisterRoutes(r)
	return r
}

func doChessRequest(r *mux.Router, method, path string, body interface{}) *httptest.ResponseRecorder {
	var reqBody *bytes.Buffer
	if body != nil {
		b, _ := json.Marshal(body)
		reqBody = bytes.NewBuffer(b)
	} else {
		reqBody = bytes.NewBuffer(nil)
	}
	req := httptest.NewRequest(method, path, reqBody)
	req.Header.Set("Content-Type", "application/json")
	rr := httptest.NewRecorder()
	r.ServeHTTP(rr, req)
	return rr
}

func TestChessHandlerCreateGame(t *testing.T) {
	r := newTestChessRouter()

	rr := doChessRequest(r, "POST", "/api/chess", map[string]int{
		"session_id": 1, "white_id": 1, "black_id": 2,
	})
	if rr.Code != http.StatusCreated {
		t.Errorf("expected 201, got %d: %s", rr.Code, rr.Body.String())
	}

	var resp GameResponse
	json.NewDecoder(rr.Body).Decode(&resp)
	if resp.Turn != "white" {
		t.Errorf("expected turn white, got %s", resp.Turn)
	}
	if resp.Status != "playing" {
		t.Errorf("expected status playing, got %s", resp.Status)
	}
}

func TestChessHandlerGetGame(t *testing.T) {
	r := newTestChessRouter()

	// Create a game first
	doChessRequest(r, "POST", "/api/chess", map[string]int{
		"session_id": 1, "white_id": 1, "black_id": 2,
	})

	rr := doChessRequest(r, "GET", "/api/chess/1", nil)
	if rr.Code != http.StatusOK {
		t.Errorf("expected 200, got %d: %s", rr.Code, rr.Body.String())
	}

	var resp GameResponse
	json.NewDecoder(rr.Body).Decode(&resp)
	if resp.ID != 1 {
		t.Errorf("expected ID 1, got %d", resp.ID)
	}
}

func TestChessHandlerMakeMove(t *testing.T) {
	r := newTestChessRouter()

	// Create a game
	doChessRequest(r, "POST", "/api/chess", map[string]int{
		"session_id": 1, "white_id": 1, "black_id": 2,
	})

	// Make a move
	rr := doChessRequest(r, "POST", "/api/chess/1/move", map[string]interface{}{
		"player_id": 1, "from": "e2", "to": "e4",
	})
	if rr.Code != http.StatusOK {
		t.Errorf("expected 200, got %d: %s", rr.Code, rr.Body.String())
	}

	var resp GameResponse
	json.NewDecoder(rr.Body).Decode(&resp)
	if resp.Turn != "black" {
		t.Errorf("expected turn black, got %s", resp.Turn)
	}
	if resp.MoveCount != 1 {
		t.Errorf("expected move count 1, got %d", resp.MoveCount)
	}
}

func TestChessHandlerResign(t *testing.T) {
	r := newTestChessRouter()

	// Create a game
	doChessRequest(r, "POST", "/api/chess", map[string]int{
		"session_id": 1, "white_id": 1, "black_id": 2,
	})

	// Resign
	rr := doChessRequest(r, "POST", "/api/chess/1/resign", map[string]int{
		"player_id": 1,
	})
	if rr.Code != http.StatusOK {
		t.Errorf("expected 200, got %d: %s", rr.Code, rr.Body.String())
	}

	var resp GameResponse
	json.NewDecoder(rr.Body).Decode(&resp)
	if resp.Status != "resigned" {
		t.Errorf("expected status resigned, got %s", resp.Status)
	}
}

func TestChessHandlerNotFound(t *testing.T) {
	r := newTestChessRouter()
	rr := doChessRequest(r, "GET", "/api/chess/999", nil)
	if rr.Code != http.StatusNotFound {
		t.Errorf("expected 404, got %d", rr.Code)
	}
}

func TestChessHandlerInvalidBody(t *testing.T) {
	r := newTestChessRouter()
	req := httptest.NewRequest("POST", "/api/chess", bytes.NewBufferString("not json"))
	req.Header.Set("Content-Type", "application/json")
	rr := httptest.NewRecorder()
	r.ServeHTTP(rr, req)
	if rr.Code != http.StatusBadRequest {
		t.Errorf("expected 400, got %d", rr.Code)
	}
}

func TestChessHandlerIllegalMove(t *testing.T) {
	r := newTestChessRouter()

	doChessRequest(r, "POST", "/api/chess", map[string]int{
		"session_id": 1, "white_id": 1, "black_id": 2,
	})

	// Try illegal move (pawn backward)
	rr := doChessRequest(r, "POST", "/api/chess/1/move", map[string]interface{}{
		"player_id": 1, "from": "e2", "to": "e1",
	})
	if rr.Code != http.StatusBadRequest {
		t.Errorf("expected 400 for illegal move, got %d: %s", rr.Code, rr.Body.String())
	}
}

// ─────────────────────── Test Helpers ───────────────────────

// sq is a shorthand for ParseSquare that panics on error (for tests only).
func sq(s string) Square {
	sq, err := ParseSquare(s)
	if err != nil {
		panic(err)
	}
	return sq
}

// emptyBoard returns a board with no pieces.
func emptyBoard() Board {
	var b Board
	for r := 0; r < 8; r++ {
		for c := 0; c < 8; c++ {
			b[r][c] = Empty
		}
	}
	return b
}
