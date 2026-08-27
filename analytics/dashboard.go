package analytics

import (
	"context"
	"fmt"
	"html/template"
	"net/http"
	"strings"
	"time"
)

// GetDashboardOverview returns the aggregate data used by the admin page.
// It intentionally exposes no player names, auth identifiers, or raw answers.
//
//encore:api public method=GET path=/api/v1/admin/overview
func GetDashboardOverview(ctx context.Context) (*DashboardOverview, error) {
	return loadDashboardOverview(ctx)
}

func loadDashboardOverview(ctx context.Context) (*DashboardOverview, error) {
	overview := &DashboardOverview{
		GeneratedAtUTC:    time.Now().UTC().Format(time.RFC3339),
		CognitiveAverages: make([]DomainAverage, 0),
		WeeklyTrend:       make([]DashboardTrendPoint, 0),
		Tasks:             make([]DashboardTask, 0),
		Games:             make([]DashboardGameStatus, 0),
		RecentSessions:    make([]DashboardRecentSession, 0),
	}
	if err := db.QueryRow(ctx, `
		SELECT
			COUNT(*) FILTER (WHERE last_seen_ts >= NOW() - INTERVAL '5 minutes'),
			COUNT(*)
		FROM player
	`).Scan(&overview.OnlineUsers, &overview.TotalPlayers); err != nil {
		return nil, err
	}
	if err := db.QueryRow(ctx, `
		SELECT
			COUNT(*),
			COUNT(*) FILTER (WHERE done_dt = CURRENT_DATE)
		FROM game_session
	`).Scan(&overview.CompletedSessions, &overview.TodaySessions); err != nil {
		return nil, err
	}
	if err := db.QueryRow(ctx, `
		SELECT
			COUNT(*) FILTER (WHERE valid_yn = FALSE OR m_val < 60),
			COALESCE(ROUND(AVG(m_val))::int, 0),
			COALESCE(ROUND((100.0 * COUNT(*) FILTER (WHERE valid_yn = TRUE) / NULLIF(COUNT(*), 0))::numeric, 1)::float8, 0)
		FROM metric
		WHERE metric_cd = 'TPI'
	`).Scan(&overview.ActiveAlerts, &overview.AverageScore, &overview.CompletionRate); err != nil {
		return nil, err
	}

	var err error
	overview.CognitiveAverages, err = loadDomainAverages(ctx)
	if err != nil {
		return nil, err
	}
	overview.WeeklyTrend, err = loadWeeklyTrend(ctx)
	if err != nil {
		return nil, err
	}
	overview.Tasks, err = loadDashboardTasks(ctx)
	if err != nil {
		return nil, err
	}
	overview.Games, err = loadGameStatuses(ctx)
	if err != nil {
		return nil, err
	}
	overview.RecentSessions, err = loadRecentSessions(ctx)
	if err != nil {
		return nil, err
	}
	return overview, nil
}

func loadDomainAverages(ctx context.Context) ([]DomainAverage, error) {
	rows, err := db.Query(ctx, `
		SELECT
			m.dmn_cd,
			ROUND(AVG(m.m_val)::numeric, 1)::float8,
			COUNT(*),
			BOOL_OR(m.valid_yn = FALSE OR m.m_val < 60)
		FROM metric m
		WHERE m.metric_cd = 'TPI'
		GROUP BY m.dmn_cd
		ORDER BY m.dmn_cd
	`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	items := make([]DomainAverage, 0)
	for rows.Next() {
		var item DomainAverage
		if err = rows.Scan(&item.Domain, &item.AverageScore, &item.RecordCount, &item.HasInvalid); err != nil {
			return nil, err
		}
		item.Label = dashboardDomainLabel(item.Domain)
		item.Trend = "實際資料"
		item.Status = dashboardScoreStatus(item.AverageScore)
		items = append(items, item)
	}
	return items, rows.Err()
}

func loadWeeklyTrend(ctx context.Context) ([]DashboardTrendPoint, error) {
	rows, err := db.Query(ctx, `
		SELECT
			TO_CHAR(done_dt, 'MM/DD') AS label,
			session_count,
			score
		FROM (
			SELECT
				s.done_dt,
				COUNT(DISTINCT s.s_id)::int AS session_count,
				COALESCE(ROUND(AVG(m.m_val))::int, 0) AS score
			FROM game_session s
			LEFT JOIN metric m
				ON m.s_id = s.s_id
				AND m.metric_cd = 'TPI'
			GROUP BY s.done_dt
			ORDER BY s.done_dt DESC
			LIMIT 7
		) recent_days
		ORDER BY done_dt ASC
	`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	points := make([]DashboardTrendPoint, 0)
	for rows.Next() {
		var point DashboardTrendPoint
		if err = rows.Scan(&point.Date, &point.Sessions, &point.Score); err != nil {
			return nil, err
		}
		points = append(points, point)
	}
	return points, rows.Err()
}

func loadDashboardTasks(ctx context.Context) ([]DashboardTask, error) {
	rows, err := db.Query(ctx, `
		SELECT s.s_id, s.game_cd, m.dmn_cd, m.m_val, m.valid_yn
		FROM metric m
		JOIN game_session s ON s.s_id = m.s_id
		WHERE m.metric_cd = 'TPI'
		  AND (m.valid_yn = FALSE OR m.m_val < 60)
		ORDER BY s.done_dt DESC, s.s_id ASC
		LIMIT 4
	`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	tasks := make([]DashboardTask, 0)
	for rows.Next() {
		var sessionID, gameName, domain string
		var score float64
		var valid bool
		if err = rows.Scan(&sessionID, &gameName, &domain, &score, &valid); err != nil {
			return nil, err
		}
		title := fmt.Sprintf("複核 %s 的 %s 指標", dashboardGameLabel(gameName), dashboardDomainLabel(domain))
		priority := "中"
		status := "待處理"
		if !valid {
			priority = "高"
			status = "需複核"
		} else if score < 60 {
			priority = "高"
		}
		tasks = append(tasks, DashboardTask{
			ID:       "T-" + shortDashboardID(sessionID),
			Title:    title,
			Owner:    "照護管理員",
			Priority: priority,
			Due:      "今天",
			Status:   status,
		})
	}
	if err = rows.Err(); err != nil {
		return nil, err
	}
	if len(tasks) == 0 {
		tasks = append(tasks, DashboardTask{
			ID:       "T-READY",
			Title:    "目前沒有待複核的低分或品質異常紀錄",
			Owner:    "系統",
			Priority: "低",
			Due:      "本週",
			Status:   "已同步",
		})
	}
	return tasks, nil
}

func loadGameStatuses(ctx context.Context) ([]DashboardGameStatus, error) {
	rows, err := db.Query(ctx, `
		SELECT
			s.game_cd,
			COUNT(DISTINCT s.s_id)::int AS sessions,
			COALESCE(ROUND((100.0 * COUNT(m.m_id) FILTER (WHERE m.valid_yn = TRUE) / NULLIF(COUNT(m.m_id), 0))::numeric)::int, 0) AS valid_rate,
			COALESCE(BOOL_OR(m.valid_yn = FALSE OR m.m_val < 60), FALSE) AS has_alert,
			COALESCE(MAX(m.dmn_cd), 'UNK') AS domain
		FROM game_session s
		LEFT JOIN metric m
			ON m.s_id = s.s_id
			AND m.metric_cd = 'TPI'
		GROUP BY s.game_cd
		ORDER BY sessions DESC, s.game_cd ASC
	`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	games := make([]DashboardGameStatus, 0)
	for rows.Next() {
		var gameName, domain string
		var sessions, validRate int
		var hasAlert bool
		if err = rows.Scan(&gameName, &sessions, &validRate, &hasAlert, &domain); err != nil {
			return nil, err
		}
		status := "上線"
		nextAction := dashboardGameNextAction(gameName, false)
		if hasAlert {
			status = "需調整"
			nextAction = dashboardGameNextAction(gameName, true)
		} else if validRate < 80 {
			status = "待優化"
			nextAction = dashboardGameNextAction(gameName, true)
		}
		games = append(games, DashboardGameStatus{
			ID:             gameName,
			Name:           dashboardGameLabel(gameName),
			Domain:         dashboardDomainLabel(domain),
			Sessions:       sessions,
			CompletionRate: validRate,
			Status:         status,
			NextAction:     nextAction,
		})
	}
	return games, rows.Err()
}

func dashboardGameNextAction(gameName string, needsAdjustment bool) string {
	if !needsAdjustment {
		return "維持追蹤"
	}

	switch strings.ToUpper(strings.TrimSpace(gameName)) {
	case "PIP", "PIPE_CONNECTION", "PIPE_PUZZLE":
		return "減少管線段數，增加旋轉提示與成功判定檢查"
	case "STP", "STROOP_COLOR_MATCH", "STROOP_COLOR":
		return "放慢題目切換，先用 2 色版本降低干擾"
	case "SUP", "SUPERMARKET_SHOPPING", "SUPERMARKET":
		return "清單先降到 1-2 項，加入貨架分類提示"
	case "CRD", "CARD_MEMORY_BATTLE", "MEMORY_CARDS":
		return "減少卡片數，延長翻牌記憶時間"
	case "ORD", "NUMBER_ORDER", "TRAIL_MAKING":
		return "減少數字節點，放大按鈕與路徑提示"
	case "SUM", "NUMBER_SUM":
		return "降低數字範圍，增加步驟提示"
	case "GOP", "GOPHER_REACTION", "BODY_WHACK_A_MOLE":
		return "放慢出現速度，放大目標點擊範圍"
	case "QIZ", "TRUE_FALSE_LIFE_QUIZ", "LIFE_QUIZ":
		return "簡化題目文字，補上生活情境提示"
	case "VPT", "VIRTUAL_PET":
		return "減少同時任務，加入下一步提醒"
	default:
		return "降低難度，檢查提示、時間與有效資料判定"
	}
}

func loadRecentSessions(ctx context.Context) ([]DashboardRecentSession, error) {
	rows, err := db.Query(ctx, `
		SELECT
			s.p_id,
			s.game_cd,
			COALESCE(ROUND(AVG(m.m_val))::int, 0) AS score,
			s.dur_ms,
			TO_CHAR(s.done_dt, 'MM/DD') AS played_at,
			COALESCE(BOOL_OR(m.valid_yn = FALSE OR m.m_val < 60), FALSE) AS has_alert
		FROM game_session s
		LEFT JOIN metric m
			ON m.s_id = s.s_id
			AND m.metric_cd = 'TPI'
		GROUP BY s.s_id, s.p_id, s.game_cd, s.dur_ms, s.done_dt
		ORDER BY s.done_dt DESC, s.s_id DESC
		LIMIT 6
	`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	sessions := make([]DashboardRecentSession, 0)
	for rows.Next() {
		var score, durationMS int
		var playerID string
		var gameName, playedAt string
		var hasAlert bool
		if err = rows.Scan(&playerID, &gameName, &score, &durationMS, &playedAt, &hasAlert); err != nil {
			return nil, err
		}
		status := "完成"
		if hasAlert {
			status = "需複核"
		}
		sessions = append(sessions, DashboardRecentSession{
			PlayerID: strings.TrimSpace(playerID),
			Game:     dashboardGameLabel(gameName),
			Score:    score,
			Duration: formatDashboardDuration(durationMS),
			PlayedAt: playedAt,
			Status:   status,
		})
	}
	return sessions, rows.Err()
}

func dashboardDomainLabel(domain string) string {
	switch strings.ToUpper(strings.TrimSpace(domain)) {
	case "ATT", "ATTENTION_INHIBITION":
		return "注意力與抑制控制"
	case "SPD", "PROCESSING_SPEED":
		return "處理速度"
	case "EXE", "EXECUTIVE_REASONING", "EXECUTIVE_FUNCTION":
		return "執行功能"
	case "MEM", "MEMORY":
		return "記憶與日常任務"
	case "VWM", "VISUAL_WORKING_MEMORY":
		return "視覺工作記憶"
	case "VSP", "VISUOSPATIAL_PLANNING":
		return "視覺空間規劃"
	case "EPM", "EPISODIC_MEMORY":
		return "情節記憶"
	case "LNG", "LANGUAGE":
		return "語言能力"
	case "ORI", "ORIENTATION":
		return "定向能力"
	case "SPR", "SPATIAL_REASONING":
		return "空間推理"
	case "MOT", "MOTOR_COORDINATION":
		return "動作協調"
	case "WEL", "WELLBEING":
		return "參與與情緒回饋"
	case "UNK", "UNKNOWN", "":
		return "未分類"
	default:
		return strings.ReplaceAll(domain, "_", " ")
	}
}

func dashboardGameLabel(gameName string) string {
	switch strings.ToUpper(strings.TrimSpace(gameName)) {
	case "STP", "STROOP_COLOR_MATCH", "STROOP_COLOR":
		return "顏色文字判斷"
	case "ORD", "NUMBER_ORDER", "TRAIL_MAKING":
		return "數字由小到大"
	case "SUM", "NUMBER_SUM":
		return "數字加總"
	case "GOP", "GOPHER_REACTION", "BODY_WHACK_A_MOLE":
		return "動作打地鼠"
	case "CRD", "CARD_MEMORY_BATTLE", "MEMORY_CARDS":
		return "翻牌記憶"
	case "PIP", "PIPE_CONNECTION", "PIPE_PUZZLE":
		return "旋轉接水管"
	case "QIZ", "TRUE_FALSE_LIFE_QUIZ", "LIFE_QUIZ":
		return "生活常識判斷"
	case "SUP", "SUPERMARKET_SHOPPING", "SUPERMARKET":
		return "超市購物"
	case "VPT", "VIRTUAL_PET":
		return "虛擬寵物"
	default:
		return strings.ReplaceAll(gameName, "_", " ")
	}
}

func dashboardScoreStatus(score float64) string {
	switch {
	case score < 60:
		return "高風險"
	case score < 75:
		return "整體偏低"
	default:
		return "表現良好"
	}
}

func shortDashboardID(value string) string {
	value = strings.TrimSpace(value)
	if len(value) <= 6 {
		return strings.ToUpper(value)
	}
	return strings.ToUpper(value[len(value)-6:])
}

func formatDashboardDuration(value int) string {
	if value <= 0 {
		return "-"
	}
	totalSeconds := value / 1000
	minutes := totalSeconds / 60
	seconds := totalSeconds % 60
	if minutes == 0 {
		return fmt.Sprintf("%d秒", seconds)
	}
	return fmt.Sprintf("%d分%02d秒", minutes, seconds)
}

// Dashboard serves a no-build, same-origin dashboard for the graduation demo.
//
//encore:api public raw method=GET path=/admin/dashboard
func Dashboard(w http.ResponseWriter, req *http.Request) {
	w.Header().Set("Content-Type", "text/html; charset=utf-8")
	w.Header().Set("Cache-Control", "no-store")
	w.Header().Set("X-Content-Type-Options", "nosniff")
	w.Header().Set("Content-Security-Policy", "default-src 'self'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; connect-src 'self'")
	if err := dashboardTemplate.Execute(w, nil); err != nil {
		http.Error(w, "dashboard render failed", http.StatusInternalServerError)
	}
}

var dashboardTemplate = template.Must(template.New("dashboard").Parse(dashboardHTML))

const dashboardHTML = `<!doctype html>
<html lang="zh-Hant">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>LTC 認知遊戲後台管理系統</title>
<style>
:root{--bg:#f4f6f8;--surface:#fff;--ink:#1d2935;--muted:#63717c;--line:#dce3e8;--primary:#24786f;--primary-dark:#195c55;--blue:#3b72a7;--amber:#b7791f;--red:#b04444;--sidebar:#1f3438;--shadow:0 14px 38px rgba(23,38,51,.08)}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font-family:"Microsoft JhengHei","Noto Sans TC",system-ui,sans-serif}button,select{font:inherit}.shell{min-height:100vh;display:grid;grid-template-columns:248px minmax(0,1fr)}.sidebar{background:var(--sidebar);color:#eef7f5;padding:24px 18px;position:sticky;top:0;height:100vh}.brand{font-size:20px;font-weight:900;line-height:1.25;margin-bottom:24px}.brand span{display:block;color:#9fd2c9;font-size:12px;letter-spacing:.12em;margin-top:5px}.nav{display:grid;gap:6px}.nav button{width:100%;display:flex;align-items:center;gap:10px;color:#dbe9e7;background:transparent;border:0;border-radius:8px;padding:11px 12px;text-align:left;cursor:pointer}.nav button:hover,.nav button.active{background:#31555a;color:#fff}.mark{width:8px;height:8px;border-radius:50%;background:#79c7bb;flex:0 0 auto}.side-foot{position:absolute;left:18px;right:18px;bottom:20px;border-top:1px solid rgba(255,255,255,.14);padding-top:16px;color:#b6cfcb;font-size:13px;line-height:1.55}.main{padding:26px 30px 48px;min-width:0}.topbar{display:flex;align-items:center;justify-content:space-between;gap:18px;margin-bottom:22px}.eyebrow{color:var(--primary);font-size:12px;font-weight:900;letter-spacing:.12em}h1{margin:5px 0 0;font-size:30px;letter-spacing:0}.actions{display:flex;align-items:center;gap:10px;flex-wrap:wrap}.control{border:1px solid var(--line);background:#fff;border-radius:8px;padding:10px 12px;color:var(--ink);min-height:42px}.icon-btn,.primary-btn{border:0;border-radius:8px;min-height:42px;cursor:pointer;font-weight:800}.icon-btn{width:42px;background:#fff;border:1px solid var(--line);color:var(--ink)}.icon-btn:hover{background:#f7fafb;border-color:#b8c6ce}.primary-btn{background:var(--primary);color:#fff;padding:10px 15px}.primary-btn:hover{background:var(--primary-dark)}.live{display:inline-flex;align-items:center;gap:9px;font-weight:800;color:var(--primary-dark)}.dot{width:10px;height:10px;border-radius:50%;background:#28a16d;box-shadow:0 0 0 5px rgba(40,161,109,.14)}.grid{display:grid;gap:16px}.stats{grid-template-columns:repeat(3,minmax(0,1fr))}.card,.panel{background:var(--surface);border:1px solid var(--line);border-radius:8px;box-shadow:var(--shadow)}.stat{padding:18px;min-width:0;overflow:hidden}.label{color:var(--muted);font-size:13px;font-weight:800}.value{font-size:31px;font-weight:950;margin-top:9px}.delta{display:block;max-width:100%;font-size:13px;color:var(--primary);font-weight:800;line-height:1.35;margin-top:6px;white-space:normal;overflow-wrap:anywhere}.layout{grid-template-columns:minmax(0,1.55fr) minmax(330px,.9fr);align-items:start;margin-top:16px}.panel{padding:20px}.panel.is-focus{outline:3px solid rgba(36,120,111,.24);outline-offset:3px}.panel-head{display:flex;align-items:flex-start;justify-content:space-between;gap:14px;margin-bottom:16px}h2{font-size:19px;margin:0}.muted{color:var(--muted)}.tabs{display:flex;gap:8px;flex-wrap:wrap}.tab{border:1px solid var(--line);background:#fff;color:var(--ink);border-radius:8px;padding:8px 10px;font-size:13px;font-weight:800;cursor:pointer}.tab.active{background:var(--primary);border-color:var(--primary);color:#fff}.domain-list{display:grid;gap:10px}.domain-row{display:grid;grid-template-columns:minmax(155px,1fr) minmax(130px,1.2fr) 92px 92px;gap:12px;align-items:center;padding:12px;border:1px solid var(--line);border-radius:8px;background:#fbfcfd}.bar-track{height:10px;border-radius:999px;background:#e8edf1;overflow:hidden}.bar-fill{height:100%;border-radius:999px;background:var(--primary);width:0;transition:width .35s}.domain-row.warn .bar-fill{background:var(--amber)}.domain-row.risk .bar-fill{background:var(--red)}.score{font-weight:950;text-align:right}.pill{display:inline-flex;align-items:center;justify-content:center;border-radius:999px;padding:5px 9px;font-size:12px;font-weight:900;background:#e9f4f1;color:var(--primary-dark);white-space:nowrap}.pill.warn{background:#fff5df;color:#8a5a13}.pill.risk{background:#fdebea;color:#9c3333}.pill.neutral{background:#edf1f6;color:#4d6275}.trend-wrap{height:232px;display:flex;align-items:flex-end;gap:10px;border-left:1px solid var(--line);border-bottom:1px solid var(--line);padding:16px 8px 0;margin-top:8px}.trend-col{flex:1;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:7px;height:100%;min-width:0}.trend-bar{width:100%;max-width:34px;background:linear-gradient(180deg,var(--blue),var(--primary));border-radius:6px 6px 0 0}.trend-bar.zero{background:transparent}.trend-label{font-size:12px;color:var(--muted);white-space:nowrap}table{width:100%;border-collapse:collapse;font-size:14px}th,td{text-align:left;padding:12px 10px;border-bottom:1px solid var(--line);vertical-align:middle}th{color:var(--muted);font-size:12px;letter-spacing:.04em}tbody tr:hover{background:#f8fafb}.task-list{display:grid;gap:10px}.task{display:grid;grid-template-columns:1fr auto;gap:12px;border:1px solid var(--line);border-radius:8px;padding:12px;background:#fbfcfd}.task strong{display:block;margin-bottom:5px}.task-meta{color:var(--muted);font-size:13px}.view{display:block}.view[hidden]{display:none}.ai-page{margin-top:16px}.ai-scope{display:flex;gap:8px;flex-wrap:wrap;margin:14px 0 0}.ai-list{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px}.ai-card{padding:16px;display:grid;gap:10px}.ai-card-head{display:flex;align-items:center;justify-content:space-between;gap:10px}.ai-card h3{margin:0;font-size:17px}.ai-card p{margin:0;color:var(--muted);line-height:1.7}.ai-source{font-size:12px;font-weight:900;color:var(--primary);letter-spacing:.04em}.empty,.error{display:none;text-align:center;color:var(--muted);padding:30px 10px;font-weight:800}.error{color:var(--red)}
@media(max-width:1040px){.shell{grid-template-columns:1fr}.sidebar{position:relative;height:auto}.side-foot{position:static;margin-top:22px}.layout,.stats,.ai-list{grid-template-columns:1fr}.main{padding:22px 18px 38px}}@media(max-width:680px){.topbar,.panel-head{flex-direction:column;align-items:stretch}.actions{width:100%}.control,.primary-btn{width:100%}.domain-row{grid-template-columns:1fr}.score{text-align:left}.hide-sm{display:none}h1{font-size:25px}.value{font-size:28px}}
</style>
</head>
<body>
<div class="shell">
  <aside class="sidebar">
    <div class="brand">LTC 認知遊戲<span>ADMIN OPERATIONS</span></div>
    <nav class="nav" aria-label="後台模組">
      <button class="active" type="button" data-section="dashboardStats"><span class="mark"></span>營運儀表板</button>
      <button type="button" data-section="gameManagement"><span class="mark"></span>遊戲內容管理</button>
      <button type="button" data-section="cognitiveTrends"><span class="mark"></span>認知趨勢分析</button>
      <button type="button" data-view="ai"><span class="mark"></span>AI 數據分析</button>
      <button type="button" data-section="taskPanel"><span class="mark"></span>照護追蹤任務</button>
      <button type="button" data-action="export"><span class="mark"></span>報表匯出</button>
      <button type="button"><span class="mark"></span>權限與稽核</button>
    </nav>
    <div class="side-foot"><div>資料範圍：staging</div><div>個資顯示：已遮蔽</div><div>更新頻率：30 秒</div></div>
  </aside>
  <main class="main">
    <header class="topbar">
      <div><div class="eyebrow" id="pageEyebrow">LONG-TERM CARE GAME ANALYTICS</div><h1 id="pageTitle">後台管理系統</h1></div>
      <div class="actions">
        <select class="control" aria-label="資料期間"><option>近 7 天</option><option>全部資料</option></select>
        <button class="icon-btn" id="refreshButton" type="button" title="重新整理" aria-label="重新整理">↻</button>
        <button class="primary-btn" id="exportButton" type="button">匯出報表</button>
      </div>
    </header>
    <div class="view" id="dashboardPage">
    <section class="grid stats" id="dashboardStats" aria-label="營運摘要">
      <article class="card stat"><div class="label">目前在線人數</div><div class="value" id="online">-</div><div class="delta">近 5 分鐘的玩家人數</div></article>
      <article class="card stat"><div class="label">完成遊戲紀錄</div><div class="value" id="sessions">-</div><div class="delta"><span id="todaySessions">-</span> 筆今日新增</div></article>
      <article class="card stat"><div class="label">待追蹤警示</div><div class="value" id="alerts">-</div><div class="delta">低分與品質異常資料</div></article>
    </section>
    <section class="grid layout">
      <div class="grid">
        <article class="panel">
          <div class="panel-head"><div><h2>認知能力分布</h2><div class="muted">平均分數、樣本數與資料品質狀態</div></div><div class="tabs" id="domainTabs"><button class="tab active" data-filter="all" type="button">全部</button><button class="tab" data-filter="review" type="button">整體偏低</button><button class="tab" data-filter="good" type="button">表現良好</button></div></div>
          <div class="domain-list" id="domainList"></div><div class="empty" id="domainEmpty">目前沒有符合條件的認知資料</div>
        </article>
        <article class="panel" id="gameManagement">
          <div class="panel-head"><div><h2>遊戲內容管理</h2><div class="muted">上線狀態、有效率與下一步調整</div></div></div>
          <div style="overflow:auto"><table><thead><tr><th>遊戲</th><th>領域</th><th>有效率</th><th>狀態</th><th>下一步</th></tr></thead><tbody id="gamesBody"></tbody></table></div>
        </article>
      </div>
      <aside class="grid">
        <article class="panel" id="cognitiveTrends">
          <div class="panel-head"><div><h2>最近 7 個有紀錄日期</h2><div class="muted">每日完成次數與平均分</div></div><span class="live"><span class="dot"></span><span id="updated">連線中</span></span></div>
          <div class="trend-wrap" id="trendChart"></div><div class="empty" id="trendEmpty">尚無趨勢資料</div><div class="error" id="loadError">暫時無法取得 dashboard 資料</div>
        </article>
        <article class="panel" id="taskPanel">
          <div class="panel-head"><div><h2>待處理事項</h2><div class="muted">依優先度排序</div></div></div><div class="task-list" id="taskList"></div>
        </article>
        <article class="panel">
          <div class="panel-head"><div><h2>最近遊戲紀錄</h2><div class="muted">依完成時間排序</div></div></div>
          <div style="overflow:auto"><table><thead><tr><th>時間</th><th>遊戲</th><th>分數</th><th>狀態</th></tr></thead><tbody id="recentBody"></tbody></table></div>
        </article>
      </aside>
    </section>
    </div>
    <section class="view ai-page" id="aiPage" hidden>
      <article class="panel">
        <div class="panel-head">
          <div>
            <h2>AI 數據分析</h2>
            <div class="muted">依目前後台資料產生簡短建議</div>
            <div class="ai-scope" aria-label="分析範圍">
              <span class="pill neutral">認知能力分布</span>
              <span class="pill neutral">遊戲內容管理</span>
              <span class="pill neutral">最近 7 個有紀錄日期</span>
              <span class="pill neutral">待處理事項</span>
            </div>
          </div>
          <span class="live"><span class="dot"></span><span id="aiUpdated">等待資料</span></span>
        </div>
        <div class="ai-list" id="aiInsightList"></div>
        <div class="empty" id="aiEmpty">目前資料不足，暫時沒有建議</div>
      </article>
    </section>
  </main>
</div>
<script>
const state={filter:'all',data:null};const formatter=new Intl.NumberFormat('zh-TW');function byId(id){return document.getElementById(id)}function setText(id,value){byId(id).textContent=value}
function tone(item){if(item.averageScore<60)return 'risk';if(item.averageScore<75)return 'warn';return ''}
function renderStats(data){setText('online',formatter.format(data.onlineUsers||0));setText('sessions',formatter.format(data.completedSessions||0));setText('todaySessions',formatter.format(data.todaySessions||0));setText('alerts',formatter.format(data.activeAlerts||0));const stamp=data.generatedAtUtc?new Date(data.generatedAtUtc):new Date();const updated='已更新 '+stamp.toLocaleTimeString('zh-TW',{hour:'2-digit',minute:'2-digit'});setText('updated',updated);setText('aiUpdated',updated)}
function renderDomains(){const list=byId('domainList');list.innerHTML='';const rows=(state.data?.cognitiveAverages||[]).filter(item=>{const t=tone(item);if(state.filter==='review')return t;if(state.filter==='good')return !t;return true});byId('domainEmpty').style.display=rows.length?'none':'block';for(const item of rows){const t=tone(item);const row=document.createElement('div');row.className='domain-row '+t;row.innerHTML='<div><strong>'+(item.label||item.domain)+'</strong><div class="muted">'+(item.recordCount||0)+' 筆遊戲紀錄</div></div><div class="bar-track"><div class="bar-fill" style="width:'+Math.max(0,Math.min(100,item.averageScore||0))+'%"></div></div><div class="score">'+Number(item.averageScore||0).toFixed(1)+'</div><div><span class="pill '+(t||'neutral')+'">'+(item.status||'已同步')+'</span></div>';list.append(row)}}
function renderTrend(data){const chart=byId('trendChart');chart.innerHTML='';const points=data.weeklyTrend||[];byId('trendEmpty').style.display=points.length?'none':'block';for(const point of points){const score=Math.max(0,Math.min(100,Number(point.score)||0));const height=score===0?0:Math.max(6,score);const col=document.createElement('div');col.className='trend-col';col.innerHTML='<div class="muted hide-sm">'+score+'分</div><div class="trend-bar '+(score===0?'zero':'')+'" title="'+point.sessions+' 筆完成" style="height:'+height+'%"></div><div class="trend-label">'+point.date+'</div>';chart.append(col)}}
function renderTasks(data){const list=byId('taskList');list.innerHTML='';for(const task of data.tasks||[]){const t=task.priority==='高'?'risk':task.priority==='中'?'warn':'neutral';const item=document.createElement('div');item.className='task';item.innerHTML='<div><strong>'+task.title+'</strong><div class="task-meta">'+task.id+' · '+task.owner+' · '+task.due+'</div></div><div><span class="pill '+t+'">'+task.status+'</span></div>';list.append(item)}}
function renderGames(data){byId('gamesBody').innerHTML=(data.games||[]).map(game=>'<tr><td><strong>'+game.name+'</strong><div class="muted">'+game.sessions+' 次完成</div></td><td>'+game.domain+'</td><td>'+game.completionRate+'%</td><td><span class="pill '+(game.status==='需調整'?'risk':game.status==='待優化'?'warn':'')+'">'+game.status+'</span></td><td>'+game.nextAction+'</td></tr>').join('')}
function renderRecent(data){byId('recentBody').innerHTML=(data.recentSessions||[]).map(item=>'<tr><td>'+item.playedAt+'</td><td>'+item.game+'</td><td><strong>'+item.score+'</strong></td><td><span class="pill '+(item.status==='需複核'?'risk':'')+'">'+item.status+'</span></td></tr>').join('')}
function insightTone(level){if(level==='risk')return 'risk';if(level==='warn')return 'warn';return 'neutral'}
function domainAdvice(item){if((item.averageScore||0)<60)return (item.label||item.domain)+'整體分數偏低，建議檢查反應速度與錯誤次數。';if((item.averageScore||0)<75)return (item.label||item.domain)+'整體略低，建議持續比較後續資料。';return (item.label||item.domain)+'表現穩定，維持目前訓練。'}
function buildAIInsights(data){const insights=[];const domains=[...(data.cognitiveAverages||[])].sort((a,b)=>(a.averageScore||0)-(b.averageScore||0));if(domains.length){const lowest=domains[0];insights.push({source:'認知能力分布',title:lowest.label||lowest.domain,body:domainAdvice(lowest),level:(lowest.averageScore||0)<60?'risk':(lowest.averageScore||0)<75?'warn':'normal'})}const game=(data.games||[]).find(item=>item.status==='需調整'||item.completionRate<70)||(data.games||[]).find(item=>item.status==='待優化'||item.completionRate<80);if(game){insights.push({source:'遊戲內容管理',title:game.name,body:game.nextAction||'降低難度，檢查提示、時間與有效資料判定。',level:game.status==='需調整'||game.completionRate<70?'risk':'warn'})}else{insights.push({source:'遊戲內容管理',title:'遊戲狀態',body:'遊戲內容目前穩定，維持追蹤即可。',level:'normal'})}const trend=data.weeklyTrend||[];if(trend.length>=2){const previous=trend[trend.length-2];const latest=trend[trend.length-1];const diff=(latest.score||0)-(previous.score||0);insights.push({source:'最近 7 個有紀錄日期',title:'分數趨勢',body:diff<0?'最近分數有下降，建議持續追蹤。':'最近分數穩定，維持目前訓練。',level:diff<0?'warn':'normal'})}const tasks=data.tasks||[];const urgent=tasks.filter(task=>task.priority==='高'||task.status==='需複核').length;insights.push({source:'待處理事項',title:'追蹤任務',body:tasks.length?('目前有 '+tasks.length+' 件待處理，優先複核低分資料。'):'目前沒有待處理事項。',level:urgent?'risk':tasks.length?'warn':'normal'});return insights}
function renderAIInsights(data){const list=byId('aiInsightList');list.innerHTML='';const insights=buildAIInsights(data);byId('aiEmpty').style.display=insights.length?'none':'block';for(const insight of insights){const t=insightTone(insight.level);const item=document.createElement('article');item.className='card ai-card';item.innerHTML='<div class="ai-card-head"><div class="ai-source">'+insight.source+'</div><span class="pill '+t+'">'+(t==='risk'?'整體偏低':t==='warn'?'整體略低':'穩定')+'</span></div><h3>'+insight.title+'</h3><p>'+insight.body+'</p>';list.append(item)}}
function renderAll(data){state.data=data;renderStats(data);renderDomains();renderTrend(data);renderTasks(data);renderGames(data);renderRecent(data);renderAIInsights(data)}
async function refresh(){byId('loadError').style.display='none';try{const response=await fetch('/api/v1/admin/overview',{cache:'no-store'});if(!response.ok)throw new Error('HTTP '+response.status);renderAll(await response.json())}catch(error){byId('loadError').style.display='block';setText('updated','連線失敗')}}
byId('domainTabs').addEventListener('click',event=>{const button=event.target.closest('.tab');if(!button)return;state.filter=button.dataset.filter;for(const tab of byId('domainTabs').querySelectorAll('.tab'))tab.classList.toggle('active',tab===button);renderDomains()});
document.querySelector('.nav').addEventListener('click',event=>{const button=event.target.closest('button');if(!button)return;if(button.dataset.action==='export'){byId('exportButton').click();return}if(button.dataset.view==='ai'){setView('ai');for(const item of document.querySelectorAll('.nav button'))item.classList.toggle('active',item===button);return}const section=byId(button.dataset.section);if(!section)return;setView('dashboard');for(const item of document.querySelectorAll('.nav button'))item.classList.toggle('active',item===button);section.scrollIntoView({behavior:'smooth',block:'start'});if(section.classList.contains('panel')){section.classList.add('is-focus');setTimeout(()=>section.classList.remove('is-focus'),1200)}});
function setView(view){const isAI=view==='ai';byId('dashboardPage').hidden=isAI;byId('aiPage').hidden=!isAI;setText('pageTitle',isAI?'AI 數據分析':'後台管理系統');setText('pageEyebrow',isAI?'AI DATA ANALYSIS':'LONG-TERM CARE GAME ANALYTICS');window.scrollTo({top:0,behavior:'smooth'})}
byId('refreshButton').addEventListener('click',()=>window.location.reload());byId('exportButton').addEventListener('click',()=>{if(!state.data)return;const blob=new Blob([JSON.stringify(state.data,null,2)],{type:'application/json'});const url=URL.createObjectURL(blob);const link=document.createElement('a');link.href=url;link.download='ltc-dashboard-overview.json';link.click();URL.revokeObjectURL(url)});refresh();setInterval(refresh,30000);
</script>
</body>
</html>`
