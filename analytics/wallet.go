package analytics

import (
	"context"
	"errors"
	"strings"
	"time"

	"encore.dev/storage/sqldb"
)

const dailyLoginCoins = 20

// GetWallet returns the authoritative balance for the signed-in player.
//
//encore:api auth method=GET path=/api/v1/wallet
func GetWallet(ctx context.Context) (*WalletResponse, error) {
	playerID, err := currentPlayerID()
	if err != nil {
		return nil, err
	}
	if _, err = db.Exec(ctx, `INSERT INTO wallet (p_id) VALUES ($1) ON CONFLICT (p_id) DO NOTHING`, playerID); err != nil {
		return nil, err
	}

	todayOperationID := dailyOperationID(time.Now())
	var response WalletResponse
	response.PlayerID = playerID
	if err = db.QueryRow(ctx, `
		SELECT w.bal, EXISTS (
			SELECT 1 FROM coin_tx t
			WHERE t.p_id = w.p_id AND t.src_cd = 'DAY' AND t.op_id = $2
		)
		FROM wallet w WHERE w.p_id = $1
	`, playerID, todayOperationID).Scan(&response.Balance, &response.DailyClaimed); err != nil {
		return nil, err
	}
	return &response, nil
}

// ClaimDailyReward grants the fixed daily reward at most once per Taipei date.
//
//encore:api auth method=POST path=/api/v1/wallet/daily
func ClaimDailyReward(ctx context.Context) (*DailyRewardResponse, error) {
	playerID, err := currentPlayerID()
	if err != nil {
		return nil, err
	}
	tx, err := db.Begin(ctx)
	if err != nil {
		return nil, err
	}
	defer tx.Rollback()

	var balance int
	if err = tx.QueryRow(ctx, `SELECT bal FROM wallet WHERE p_id = $1 FOR UPDATE`, playerID).Scan(&balance); err != nil {
		return nil, err
	}
	opID := dailyOperationID(time.Now())
	result, err := tx.Exec(ctx, `
		INSERT INTO coin_tx (p_id, amt, src_cd, op_id)
		VALUES ($1, $2, 'DAY', $3)
		ON CONFLICT (p_id, op_id) DO NOTHING
	`, playerID, dailyLoginCoins, opID)
	if err != nil {
		return nil, err
	}
	claimed := result.RowsAffected() == 1
	if claimed {
		if err = tx.QueryRow(ctx, `
			UPDATE wallet SET bal = bal + $2 WHERE p_id = $1 RETURNING bal
		`, playerID, dailyLoginCoins).Scan(&balance); err != nil {
			return nil, err
		}
	}
	if err = tx.Commit(); err != nil {
		return nil, err
	}
	return &DailyRewardResponse{Claimed: claimed, Reward: dailyLoginCoins, Balance: balance}, nil
}

// PurchaseItem atomically deducts the server-owned price and updates inventory.
// The short operation id makes a retry return the original transaction instead
// of charging the player twice.
//
//encore:api auth method=POST path=/api/v1/store/purchase
func PurchaseItem(ctx context.Context, p *PurchaseRequest) (*PurchaseResponse, error) {
	playerID, err := currentPlayerID()
	if err != nil {
		return nil, err
	}
	if p == nil || !validOperationID(strings.TrimSpace(p.OperationID), 'B') {
		return nil, invalidArgument("operationId must use the B00000000 format")
	}
	itemCode := strings.ToUpper(strings.TrimSpace(p.ItemCode))
	if itemCode == "" || len(itemCode) > 12 || p.Quantity < 1 || p.Quantity > 99 {
		return nil, invalidArgument("itemCode or quantity is invalid")
	}

	tx, err := db.Begin(ctx)
	if err != nil {
		return nil, err
	}
	defer tx.Rollback()

	var balance int
	if err = tx.QueryRow(ctx, `SELECT bal FROM wallet WHERE p_id = $1 FOR UPDATE`, playerID).Scan(&balance); err != nil {
		return nil, err
	}

	opID := strings.TrimSpace(p.OperationID)
	var existingTransaction string
	if err = tx.QueryRow(ctx, `
		SELECT COALESCE((SELECT TRIM(tx_id) FROM coin_tx WHERE p_id = $1 AND op_id = $2), '')
	`, playerID, opID).Scan(&existingTransaction); err != nil {
		return nil, err
	}
	if existingTransaction != "" {
		var quantity, spent int
		if err = tx.QueryRow(ctx, `
			SELECT inv.qty, -t.amt
			FROM coin_tx t
			JOIN coin_tx_item d ON d.tx_id = t.tx_id
			JOIN item i ON i.i_id = d.i_id
			JOIN inventory inv ON inv.p_id = t.p_id AND inv.i_id = i.i_id
			WHERE t.p_id = $1 AND t.op_id = $2 AND i.item_cd = $3
		`, playerID, opID, itemCode).Scan(&quantity, &spent); err != nil {
			return nil, err
		}
		if err = tx.Commit(); err != nil {
			return nil, err
		}
		return &PurchaseResponse{
			TransactionID: existingTransaction, ItemCode: itemCode, ItemQuantity: quantity,
			Spent: spent, Balance: balance, Created: false,
		}, nil
	}

	var itemID int64
	var unitPrice int
	if err = tx.QueryRow(ctx, `SELECT i_id, price FROM item WHERE item_cd = $1`, itemCode).Scan(&itemID, &unitPrice); err != nil {
		if errors.Is(err, sqldb.ErrNoRows) {
			return nil, invalidArgument("itemCode is not sold by the server")
		}
		return nil, err
	}
	total := unitPrice * p.Quantity
	if total <= 0 || balance < total {
		return nil, invalidArgument("wallet balance is insufficient")
	}

	var transactionID string
	if err = tx.QueryRow(ctx, `
		INSERT INTO coin_tx (p_id, amt, src_cd, op_id)
		VALUES ($1, $2, 'BUY', $3)
		RETURNING TRIM(tx_id)
	`, playerID, -total, opID).Scan(&transactionID); err != nil {
		return nil, err
	}
	if err = tx.QueryRow(ctx, `
		UPDATE wallet SET bal = bal - $2 WHERE p_id = $1 RETURNING bal
	`, playerID, total).Scan(&balance); err != nil {
		return nil, err
	}
	if _, err = tx.Exec(ctx, `
		INSERT INTO coin_tx_item (tx_id, i_id, qty, unit_price) VALUES ($1, $2, $3, $4)
	`, transactionID, itemID, p.Quantity, unitPrice); err != nil {
		return nil, err
	}
	var itemQuantity int
	if err = tx.QueryRow(ctx, `
		INSERT INTO inventory (p_id, i_id, qty) VALUES ($1, $2, $3)
		ON CONFLICT (p_id, i_id) DO UPDATE SET qty = inventory.qty + EXCLUDED.qty
		RETURNING qty
	`, playerID, itemID, p.Quantity).Scan(&itemQuantity); err != nil {
		return nil, err
	}
	if err = tx.Commit(); err != nil {
		return nil, err
	}
	return &PurchaseResponse{
		TransactionID: transactionID, ItemCode: itemCode, ItemQuantity: itemQuantity,
		Spent: total, Balance: balance, Created: true,
	}, nil
}

func dailyOperationID(now time.Time) string {
	location, err := time.LoadLocation("Asia/Taipei")
	if err != nil {
		location = time.FixedZone("Asia/Taipei", 8*60*60)
	}
	return "D" + now.In(location).Format("20060102")
}

func validOperationID(value string, prefix byte) bool {
	if len(value) != 9 || value[0] != prefix {
		return false
	}
	for _, character := range value[1:] {
		if character < '0' || character > '9' {
			return false
		}
	}
	return true
}

func calculateGameReward(gameCode string, trials []TrialRequest) int {
	correct, completedRounds := 0, 0
	score := 0
	for _, trial := range trials {
		event := strings.ToUpper(strings.TrimSpace(trial.EventCode))
		outcome := strings.ToUpper(strings.TrimSpace(trial.OutcomeCode))
		switch compactGameCode(gameCode) {
		case "STP":
			if event != "RSP" {
				continue
			}
			if outcome == "COR" {
				correct++
				score += 10
			} else if outcome == "INC" {
				score = maxInt(0, score-5)
			}
		case "ORD":
			if event == "RSP" && outcome == "COR" {
				correct++
			}
			if event == "RND" && outcome == "COR" {
				completedRounds++
			}
		case "SUM":
			if event == "RND" && outcome == "COR" {
				completedRounds++
				score += 20
			} else if event == "SEL" && outcome == "INC" {
				score = maxInt(0, score-5)
			}
		}
	}

	reward := 0
	switch compactGameCode(gameCode) {
	case "STP":
		reward = correct + score/20
	case "ORD":
		reward = correct + completedRounds*2
	case "SUM":
		reward = completedRounds*3 + score/10
	}
	if reward < 0 || reward > 200 {
		return 0
	}
	return reward
}
