package hello

import "net/http"

// Index sends the app root to the LTC operations dashboard and provides a
// small fallback page for unknown routes.
//
//encore:api public raw path=/!path
func Index(w http.ResponseWriter, req *http.Request) {
	if req.URL.Path == "/" {
		http.Redirect(w, req, "/admin/dashboard", http.StatusTemporaryRedirect)
		return
	}
	w.Header().Set("Content-Type", "text/html; charset=utf-8")
	w.WriteHeader(http.StatusNotFound)
	_, _ = w.Write([]byte(`<!doctype html><html lang="zh-Hant"><meta charset="utf-8"><title>找不到頁面</title><body style="font-family:Microsoft JhengHei,sans-serif;padding:40px"><h1>找不到這個頁面</h1><p><a href="/admin/dashboard">返回 LTC 儀表板</a></p></body></html>`))
}
