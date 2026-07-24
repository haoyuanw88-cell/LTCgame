# ADR-001：玩家身分與登入供應商分離

- 狀態：Accepted
- 日期：2026-07-20

## 背景

Unity 目前使用可修改的玩家名稱識別認知資料，Google 登入也只取得使用者資訊而未建立可信任的後端身分。未來產品需同時支援 Windows、Android、訪客、Google，以及訪客升級正式帳號。

## 決策

1. PostgreSQL 的 `player_id bigint` 是內部主鍵，適合關聯與人工查詢。
2. `player_code` 是不可變、可讀且不可依序猜測的業務識別碼，格式為 `LTC-U-XXXXXXXXXX`。
3. 外部登入放在 `auth_identity`，以 `(provider, subject_hash)` 唯一對應玩家；Email、姓名都不是登入主鍵。
4. 外部 subject 以伺服器 HMAC-SHA256 後保存，避免資料庫外洩時直接暴露 Google/Firebase UID。
5. 訪客安裝識別碼由 Unity 使用密碼學亂數產生一次並保存；它不是密碼，可在正式登入時合併至相同玩家。
6. API 核發高熵的 opaque access token；資料庫只保存 SHA-256 token hash，原始 token 不落庫。
7. Google/Firebase 是可替換的身分供應商，遊戲、認知評估、商店與寵物只依賴 `player_id`。

## 後果

- 改姓名、Email 或登入供應商不會改變玩家或歷史資料。
- Windows 與 Android 可以使用不同登入前端，但最後連到同一玩家。
- 訪客資料可以安全升級，不必複製測驗紀錄。
- 需要妥善保存 `Identity:SubjectHashKey`，且正式環境必須使用 HTTPS。

## 不採用方案

- 玩家名稱／Email 當主鍵：可修改、可能重複。
- Google `sub` 或 Firebase UID 直接當所有資料表外鍵：造成供應商綁定並暴露外部識別碼。
- 每張表都使用 UUID：可分散產生，但人工查詢與高密度子表索引不理想。
