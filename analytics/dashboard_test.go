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
