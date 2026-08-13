# LTC Cognitive API

Unity 不直接連 PostgreSQL，而是透過此 API 登入、上傳評估與讀取趨勢。資料庫採 9 張必要實體表；登入工作階段改為 1 天效期的簽章 Token，不另建 Session 表。

## 啟動

```powershell
dotnet ef database update --project backend/LtcCognitive.Api/LtcCognitive.Api.csproj
dotnet run --project backend/LtcCognitive.Api/LtcCognitive.Api.csproj --urls http://127.0.0.1:5077
```

連線字串與簽章金鑰存於 .NET User Secrets，不放入 Git。

## API

- `GET /health`：後端與資料庫健康檢查。
- `POST /api/v2/auth/guest`：以安裝識別碼登入；日後可將 provider 改接 Google。
- `GET /api/v2/auth/me`：讀取目前登入玩家。
- `POST /api/v2/auth/logout`：本機登出；Token 最長 1 天後失效。
- `POST /api/v1/assessments`：上傳一場遊戲評估。
- `GET /api/v1/assessments/mine/history`：目前玩家的評估歷史。
- `GET /api/v1/assessments/mine/trends?days=30`：目前玩家的認知趨勢。

完整表格與欄位翻譯見 [9-table-data-dictionary.md](../docs/database/9-table-data-dictionary.md)。
