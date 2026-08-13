# 玩家身分資料字典（V2 第一階段）

所有名稱採小寫 `snake_case`，在 pgAdmin 中不需要雙引號。所有時間保存為 UTC `timestamptz`；民國日期只在報表 View 顯示，不以字串取代日期型別。

## `ltc.player`

| 欄位 | 型別 | 必填 | 意義／規則 |
|---|---|---:|---|
| `player_id` | bigint identity | 是 | 內部 PK；不顯示給一般玩家 |
| `player_code` | varchar(16) | 是 | 唯一且不可變，`LTC-U-` 加 10 位 Base32 |
| `status` | varchar(16) | 是 | `active`、`suspended`、`deleted` |
| `created_at_utc` | timestamptz | 是 | 玩家建立時間 |
| `last_login_at_utc` | timestamptz | 是 | 最近成功登入時間 |

## `ltc.player_profile`

| 欄位 | 型別 | 必填 | 意義／規則 |
|---|---|---:|---|
| `player_id` | bigint | 是 | PK/FK → `player` |
| `display_name` | varchar(80) | 否 | 顯示名稱，可修改且不影響身分 |
| `birth_year` | smallint | 否 | 僅在取得同意後保存；合理範圍約束 |
| `education_level_code` | varchar(32) | 否 | 受控代碼，不保存自由文字 |
| `updated_at_utc` | timestamptz | 是 | 最近更新時間 |

## `ltc.auth_identity`

| 欄位 | 型別 | 必填 | 意義／規則 |
|---|---|---:|---|
| `auth_identity_id` | bigint identity | 是 | PK |
| `player_id` | bigint | 是 | FK → `player` |
| `provider` | varchar(32) | 是 | `guest`、`firebase`、`google` |
| `subject_hash` | char(64) | 是 | provider subject 的 HMAC-SHA256 |
| `created_at_utc` | timestamptz | 是 | 首次連結時間 |
| `last_used_at_utc` | timestamptz | 是 | 最近使用時間 |

`(provider, subject_hash)` 必須唯一，確保一個外部帳號不會對應兩位玩家。

## `ltc.device_installation`

| 欄位 | 型別 | 必填 | 意義／規則 |
|---|---|---:|---|
| `installation_id` | bigint identity | 是 | PK |
| `installation_uid` | varchar(64) | 是 | Unity 安裝識別碼，唯一 |
| `player_id` | bigint | 否 | 最近綁定玩家 |
| `platform` | varchar(32) | 否 | Windows、Android 等 |
| `device_model` | varchar(120) | 否 | 裝置型號 |
| `app_version` | varchar(32) | 否 | App 版本 |
| `first_seen_at_utc` | timestamptz | 是 | 首次出現 |
| `last_seen_at_utc` | timestamptz | 是 | 最近出現 |

## `ltc.auth_session`

| 欄位 | 型別 | 必填 | 意義／規則 |
|---|---|---:|---|
| `auth_session_id` | bigint identity | 是 | PK |
| `player_id` | bigint | 是 | FK → `player` |
| `installation_id` | bigint | 否 | FK → `device_installation` |
| `token_hash` | char(64) | 是 | Access token SHA-256；原始 token 不落庫 |
| `created_at_utc` | timestamptz | 是 | 核發時間 |
| `expires_at_utc` | timestamptz | 是 | 到期時間 |
| `last_seen_at_utc` | timestamptz | 是 | 最近使用時間 |
| `revoked_at_utc` | timestamptz | 否 | 登出／撤銷時間 |

## 常用查詢 View

`ltc.player_directory` 只呈現 `player_id`、`player_code`、顯示名稱、狀態與登入時間，不暴露 subject hash 或 token hash。
