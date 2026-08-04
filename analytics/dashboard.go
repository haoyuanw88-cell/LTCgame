package analytics

import (
	"context"
	"html/template"
	"net/http"
	"time"
)

// GetDashboardOverview returns the lightweight aggregate used by the admin page.
// It intentionally exposes no player-level or health-identifying information.
//
//encore:api public method=GET path=/api/v1/admin/overview
func GetDashboardOverview(ctx context.Context) (*DashboardOverview, error) {
	return loadDashboardOverview(ctx)
}

func loadDashboardOverview(ctx context.Context) (*DashboardOverview, error) {
	overview := &DashboardOverview{
		GeneratedAtUTC:    time.Now().UTC().Format(time.RFC3339),
		CognitiveAverages: make([]DomainAverage, 0),
	}
	if err := db.QueryRow(ctx, `
		SELECT
			COUNT(*) FILTER (WHERE last_seen_ts >= NOW() - INTERVAL '5 minutes'),
			COUNT(*)
		FROM player
	`).Scan(&overview.OnlineUsers, &overview.TotalPlayers); err != nil {
		return nil, err
	}
	if err := db.QueryRow(ctx, `SELECT COUNT(*) FROM game_session`).Scan(&overview.CompletedSessions); err != nil {
		return nil, err
	}
	rows, err := db.Query(ctx, `
		SELECT m.dmn_name, ROUND(AVG(m.m_val)::numeric, 1)::float8, COUNT(*)
		FROM metric m
		JOIN game_session s ON s.s_id = m.s_id
		WHERE m.metric_name = 'task_performance_index'
		  AND m.valid_yn = TRUE
		  AND s.done_dt >= CURRENT_DATE - 29
		GROUP BY m.dmn_name
		ORDER BY m.dmn_name
	`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	for rows.Next() {
		var item DomainAverage
		if err = rows.Scan(&item.Domain, &item.AverageScore, &item.RecordCount); err != nil {
			return nil, err
		}
		overview.CognitiveAverages = append(overview.CognitiveAverages, item)
	}
	if err = rows.Err(); err != nil {
		return nil, err
	}
	return overview, nil
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
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>LTC 認知遊戲營運儀表板</title>
<style>
:root{--bg:#f6f2e9;--card:#fffdf8;--ink:#263238;--muted:#687477;--green:#3e8d83;--orange:#e7903c;--blue:#527fa4;--purple:#7772b3;--line:#d9dfdc}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font-family:"Microsoft JhengHei","Noto Sans TC",sans-serif}
.wrap{max-width:1180px;margin:auto;padding:38px 28px 60px}.eyebrow{color:var(--green);font-weight:800;letter-spacing:.08em}.head{display:flex;align-items:flex-end;justify-content:space-between;gap:20px;margin-bottom:28px}h1{margin:8px 0 6px;font-size:34px}.sub{color:var(--muted);font-size:16px}.live{display:flex;align-items:center;gap:9px;background:#e9f5f0;padding:10px 16px;border-radius:999px;font-weight:800}.dot{width:11px;height:11px;border-radius:50%;background:#33a06f;box-shadow:0 0 0 6px #33a06f22}
.cards{display:grid;grid-template-columns:repeat(3,1fr);gap:18px}.card,.panel{background:var(--card);border:1px solid #e6e0d5;border-radius:18px;box-shadow:0 8px 28px #695f4c12}.card{padding:22px 24px;border-left:7px solid var(--green)}.card:nth-child(2){border-left-color:var(--orange)}.card:nth-child(3){border-left-color:var(--blue)}.label{color:var(--muted);font-weight:700}.value{font-size:42px;font-weight:900;margin-top:10px}.panel{margin-top:22px;padding:26px}.panel-head{display:flex;align-items:center;justify-content:space-between}.panel h2{margin:0;font-size:24px}.range{color:var(--muted);font-weight:700}.chart{height:360px;margin-top:22px;position:relative;border-left:2px solid var(--line);border-bottom:2px solid var(--line);background:repeating-linear-gradient(to top,transparent 0,transparent calc(25% - 1px),var(--line) 25%)}
.bars{height:100%;display:flex;align-items:flex-end;justify-content:space-around;padding:24px 7% 0;gap:48px}.bar-wrap{height:100%;width:min(190px,27%);display:flex;flex-direction:column;justify-content:flex-end;align-items:center}.bar-value{font-weight:900;margin-bottom:8px}.bar{width:70%;min-height:0;border-radius:12px 12px 0 0;transition:height .35s;background:var(--green)}.bar[data-domain="processing_speed"]{background:var(--orange)}.bar[data-domain="executive_reasoning"]{background:var(--purple)}.bar-label{text-align:center;font-weight:800;min-height:56px;padding-top:10px}.empty{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;text-align:center;color:var(--muted);font-size:20px;font-weight:700;padding:30px}.foot{margin-top:16px;color:var(--muted);font-size:14px}.error{color:#b5473d}
@media(max-width:760px){.head{align-items:flex-start;flex-direction:column}.cards{grid-template-columns:1fr}.chart{height:300px}.bars{gap:12px;padding-inline:2%}.bar-wrap{width:31%}h1{font-size:28px}}
</style></head>
<body><main class="wrap">
<div class="head"><div><div class="eyebrow">LTC COGNITIVE GAME</div><h1>營運與認知資料概覽</h1><div class="sub">僅呈現彙總資料，不顯示個別玩家的敏感資訊</div></div><div class="live"><span class="dot"></span><span id="updated">正在連線</span></div></div>
<section class="cards"><article class="card"><div class="label">目前在線人數</div><div class="value" id="online">—</div></article><article class="card"><div class="label">累積玩家</div><div class="value" id="players">—</div></article><article class="card"><div class="label">完成遊戲紀錄</div><div class="value" id="sessions">—</div></article></section>
<section class="panel"><div class="panel-head"><h2>認知能力平均分布</h2><div class="range">最近 30 天・有效紀錄</div></div><div class="chart"><div class="bars" id="bars"></div><div class="empty" id="empty">尚無足夠資料<br>完成遊戲並通過資料品質檢查後，圖表會自動出現</div></div><div class="foot">分數用於觀察群體遊戲表現，不代表醫療診斷。線上定義：最近 5 分鐘內有心跳。</div></section>
</main><script>
const labels={attention_inhibition:'注意力與抑制控制',processing_speed:'處理速度與視覺搜尋',executive_reasoning:'執行功能與數字推理'};
async function refresh(){try{const r=await fetch('/api/v1/admin/overview',{cache:'no-store'});if(!r.ok)throw new Error('HTTP '+r.status);const d=await r.json();online.textContent=d.onlineUsers;players.textContent=d.totalPlayers;sessions.textContent=d.completedSessions;updated.textContent='資料已更新';updated.classList.remove('error');bars.innerHTML='';const rows=(d.cognitiveAverages||[]).filter(x=>labels[x.domain]);empty.style.display=rows.length?'none':'flex';for(const x of rows){const w=document.createElement('div');w.className='bar-wrap';const v=document.createElement('div');v.className='bar-value';v.textContent=Number(x.averageScore).toFixed(1)+' / 100';const b=document.createElement('div');b.className='bar';b.dataset.domain=x.domain;b.style.height=Math.max(0,Math.min(100,x.averageScore))+'%';const l=document.createElement('div');l.className='bar-label';l.textContent=labels[x.domain]+'（'+x.recordCount+'筆）';w.append(v,b,l);bars.append(w)}}catch(e){updated.textContent='暫時無法取得資料';updated.classList.add('error')}}refresh();setInterval(refresh,30000);
</script></body></html>`
