# LTC 樂齡認知遊戲｜新專案交接摘要

> 目的：將長對話濃縮成可帶到新專案的單一背景文件。若本文件與程式碼不一致，以目前程式碼、Migration 與最新 Git 紀錄為準。

## 一、專案定位

Unity 製作的長照族群認知訓練遊戲系統。玩家透過多款小遊戲練習注意力、處理速度、執行功能、記憶與生活判斷；系統保存逐題作答資料，提供個人趨勢、同齡參考、各遊戲表現，以及後端管理頁面觀察整體遊玩狀況。寵物、商店、金幣、背包與圖鑑是持續遊玩的輕量化回饋機制。

## 二、目前技術組成

- **Unity/C#**：遊戲場景、題目流程、計時、答案判定、暫停、寵物動畫、商店、背包、圖鑑與統計 UI。
- **Unity UI**：Canvas、錨點、ScrollRect、Grid/Layout、TextMeshPro；目前 UI 以溫馨手繪風為主，需優先兼顧手機／平板可讀性。
- **ASP.NET Core API**：`backend/LtcCognitive.Api`，Unity 不直接連資料庫。
- **PostgreSQL + EF Core**：關聯式資料、Migration 管理結構，資料表位於 `ltc` 與 `cognitive` schema。
- **登入**：目前 API 支援 guest 安裝識別登入；Google 登入可作為後續 provider，玩家識別必須以穩定的 `auth_subject_hash` 綁定，避免重複產生玩家。
- **統計**：由逐題事件整理正確率、反應時間、完成狀態與衍生指標；圖表需區分玩家、遊戲、日期與認知能力。
- **目前主要平台**：Windows 桌面版。Android 曾進行打包與模擬器測試，但不是目前穩定交付基準；切回 Windows 不應移除 Android 設定。

## 三、重要路徑

| 內容 | 路徑 |
|---|---|
| Unity 專案 | `jt/` |
| API | `backend/LtcCognitive.Api/` |
| 資料庫 Migration | `backend/LtcCognitive.Api/Data/Migrations/` |
| EF DbContext | `backend/LtcCognitive.Api/Data/LtcDbContext.cs` |
| Domain 實體 | `backend/LtcCognitive.Api/Domain/Entities.cs`、`IdentityEntities.cs` |
| API 說明 | `backend/README.md` |
| 資料庫摘要 | `docs/database/current-schema-summary-zh-tw.md` |
| 完整欄位字典 | `docs/database/9-table-data-dictionary.md` |
| 玩家識別規則 | `docs/database/player-identity-data-dictionary.md` |
| Unity 遊戲素材 | `jt/Assets/`（新增素材統一放對應的 `GameAssets`／遊戲素材資料夾） |

## 四、後端啟動與 API

```powershell
dotnet ef database update --project backend/LtcCognitive.Api/LtcCognitive.Api.csproj
dotnet run --project backend/LtcCognitive.Api/LtcCognitive.Api.csproj --urls http://127.0.0.1:5077
```

目前主要端點：

- `GET /health`：API 與資料庫健康檢查。
- `POST /api/v2/auth/guest`：以安裝識別碼建立／取得玩家。
- `GET /api/v2/auth/me`：取得目前玩家。
- `POST /api/v2/auth/logout`：本機登出。
- `POST /api/v1/assessments`：上傳一場評估與逐題資料。
- `GET /api/v1/assessments/mine/history`：目前玩家歷史。
- `GET /api/v1/assessments/mine/trends?days=30`：個人趨勢。

連線字串與簽章金鑰使用 .NET User Secrets，不提交到 Git。

## 五、目前資料庫架構（9 張必要實體表）

### `ltc` schema

1. **`ltc.player` 玩家／帳號**：`player_id` PK、`player_code`、`auth_provider`、`auth_subject_hash`、`display_name`、`birth_year`、`education_level_code`、`status`、建立與最後登入時間。
2. **`ltc.coin_transaction` 金幣異動**：`transaction_id` PK、`player_id` FK、`amount`、`transaction_type`、`reference_key`、時間。`reference_key` 用於冪等，避免獎勵重複入帳。
3. **`ltc.item` 商品／物品**：`item_id` PK、`item_code`、`item_type`、中文名稱、價格、上架狀態。
4. **`ltc.player_inventory` 玩家背包**：`player_id + item_id` 複合 PK、數量、首次取得時間；分別 FK 到玩家與商品。

### `cognitive` schema

5. **`cognitive.cognitive_domain` 認知能力分類**：能力 UUID、代碼、中文名稱、說明。
6. **`cognitive.game` 遊戲定義**：遊戲 UUID、代碼、中文名稱、主要能力 FK、`mapping_version`、依據說明、啟用狀態。
7. **`cognitive.assessment_session` 評估場次**：場次 UUID、玩家 FK、遊戲 FK、`task_version`、`schema_version`、開始／結束／接收時間、完成狀態與離開原因。
8. **`cognitive.trial_event` 逐題事件**：複合 PK `session_id + trial_index`；保存題型、刺激 JSON、正確答案、玩家答案、正確與否、反應時間、呈現時間。
9. **`cognitive.derived_metric` 衍生指標**：指標 PK、場次 FK、能力 FK、指標代碼、數值、單位、計算版本、資料品質旗標。

## 六、資料關聯與統計原則

```text
player 1 ──< assessment_session >── 1 game >── 1 cognitive_domain
                  │
                  ├──< trial_event
                  └──< derived_metric >── cognitive_domain

player 1 ──< coin_transaction
player 1 ──< player_inventory >── 1 item
```

- 一次遊戲先建立 `assessment_session`，每題寫入 `trial_event`，結算後寫入 `derived_metric`。
- 不把不同遊戲的原始分數直接混算；統計必須先依 `game_code`、`task_version`、日期與認知能力分組。
- 同齡參考需有足夠樣本才顯示；樣本不足時顯示「參考玩家不足」，不要製造虛假的比較結果。
- 圖表日期以 `started_at_utc` 轉換後的玩家／系統時區顯示；伺服器仍統一保存 UTC。
- 公式變更要增加 `calculation_version`；上傳格式變更要增加 `schema_version`，避免歷史資料失去可比性。

## 七、目前 UI／互動共識

- 統計頁三個主要分頁：**個人趨勢、同齡參考、各遊戲表現**。
- 玩家 ID 顯示在「我的」頁面；遊戲頁保留必要的金幣與玩家識別資訊，但不要在每個頁面重複顯示過多帳號資訊。
- 老年使用者優先：大字級、高對比、足夠點擊區、手機／平板自適應；小字只用於非必要輔助資訊。
- 寵物系統包含狀態視窗、飢餓肉圖示、餵食確認視窗、垂直可滑動清單、背包與圖鑑。
- 盲盒獲得的寵物必須走與既有寵物相同的解鎖、動畫、碰撞、比例與圖鑑流程，不能覆蓋原有寵物。

## 八、版本與交接狀態

- Git 分支：`main`。
- 最近已處理：`KAIU_Dynamic.asset` 動態字型快取衝突；已完成合併並推送遠端。
- 新專案開始時先閱讀本文件，再閱讀 `backend/README.md`、`docs/database/current-schema-summary-zh-tw.md` 與完整資料字典。
- 不要直接刪除 Unity 的 `.meta`、Migration 或 ProjectSettings；任何資料庫欄位調整都應新增 Migration。

## 九、新專案第一輪工作順序

1. 先確認 Git 工作樹乾淨、Unity 能開啟 `jt/` 並以 Windows 平台編譯。
2. 啟動 API，呼叫 `/health`，確認資料庫連線。
3. 用測試玩家完成一場遊戲，確認場次、逐題事件與衍生指標都有保存。
4. 再檢查統計三分頁與後端管理頁的資料來源，不先改 UI 圖片。
5. 若要調整資料結構，先更新 Domain／DbContext，再建立 Migration，最後測試既有資料相容性。
