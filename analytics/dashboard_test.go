package analytics

import (
	"strings"
	"testing"
)

func TestDashboardHTMLIncludesAIAnalysisView(t *testing.T) {
	if !strings.Contains(dashboardHTML, "/api/v1/admin/overview") {
		t.Fatal("dashboard HTML should fetch the admin overview API")
	}
	if !strings.Contains(dashboardHTML, "AI 數據分析") {
		t.Fatal("dashboard HTML should render the AI analysis navigation item")
	}
	if !strings.Contains(dashboardHTML, "aiInsightList") {
		t.Fatal("dashboard HTML should render AI insight cards")
	}
}

func TestDashboardGameNextActionIsSpecific(t *testing.T) {
	tests := []struct {
		game string
		want string
	}{
		{game: "PIP", want: "減少管線段數，增加旋轉提示與成功判定檢查"},
		{game: "STP", want: "放慢題目切換，先用 2 色版本降低干擾"},
		{game: "SUP", want: "清單先降到 1-2 項，加入貨架分類提示"},
	}

	for _, tt := range tests {
		t.Run(tt.game, func(t *testing.T) {
			if got := dashboardGameNextAction(tt.game, true); got != tt.want {
				t.Fatalf("dashboardGameNextAction(%q, true) = %q, want %q", tt.game, got, tt.want)
			}
		})
	}

	if got := dashboardGameNextAction("PIP", false); got != "維持追蹤" {
		t.Fatalf("dashboardGameNextAction(PIP, false) = %q, want 維持追蹤", got)
	}
}
