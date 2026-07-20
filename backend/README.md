# LTC Cognitive API

這是 Unity 與 PostgreSQL 中間的後端。Unity 不直接持有資料庫密碼，只把每次遊戲的逐題紀錄與計算指標送到此 API。

## 第一次設定

在倉庫根目錄執行：

```powershell
dotnet user-secrets set "ConnectionStrings:LtcDatabase" "Host=localhost;Port=5432;Database=ltc_cognitive;Username=ltc_app;Password=你的ltc_app密碼" --project backend\LtcCognitive.Api\LtcCognitive.Api.csproj
dotnet tool restore
dotnet tool run dotnet-ef database update --project backend\LtcCognitive.Api\LtcCognitive.Api.csproj
dotnet run --project backend\LtcCognitive.Api\LtcCognitive.Api.csproj --urls http://localhost:5077
```

密碼會存放在 Windows 使用者的 Secret Manager，不會進入 Git。

## 目前 API

- `GET /health`：確認後端與 PostgreSQL 都正常。
- `POST /api/v1/participants/resolve`：建立或更新匿名受測者。
- `POST /api/v1/assessments`：一次上傳完整測驗、逐題反應和計算指標；相同 Session ID 重送不會重複寫入。
- `GET /api/v1/assessments/participants/{code}/history`：取得最近測驗。
- `GET /api/v1/assessments/participants/{code}/trends?days=30`：取得圖表用趨勢資料。

可直接用 `LtcCognitive.Api/LtcCognitive.Api.http` 測試範例資料。

## 為何保留這些欄位

- `taskVersion`：遊戲規則或難度修改後仍可區分資料。
- `schemaVersion`：Unity 上傳格式可逐步升級。
- `calculationVersion`：評分公式更新後不會把新舊分數誤當同一尺度。
- `device`：保留螢幕、DPI、平台，未來可研究裝置差異，而不是直接假設所有裝置相同。
- `trials`：保留每一題正誤、反應時間與刺激條件，日後能重新計算指標和做效度分析。
- `qualityFlag`：排除中途離開、反應過快或資料不完整的測驗。

此系統目前定位為研究／健康促進原型，不是醫療診斷工具。正式對外服務前仍需加入身分驗證、HTTPS、備份與個資同意流程。
