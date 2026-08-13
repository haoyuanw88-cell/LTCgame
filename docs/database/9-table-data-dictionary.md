# LTC 九表資料字典

原則：一個帳號就是一位玩家；只保存日後查詢、統計、稽核或商店真正會使用的資料。UUID 僅保留在評估場次與固定參照資料，平常查詢使用短的 `player_code`、`game_code`、`domain_code`。

## 1. `ltc.player`（玩家／帳號）

| 欄位 | 中文 | 用途 |
|---|---|---|
| `player_id` | 玩家流水號 | 內部主鍵，短且適合關聯 |
| `player_code` | 玩家代碼 | 客服、畫面與人工查詢使用 |
| `auth_provider` | 登入來源 | `guest`、未來的 `google` 等 |
| `auth_subject_hash` | 登入身分雜湊 | 不保存 Google 原始識別碼 |
| `display_name` | 顯示名稱 | 玩家可修改的名稱 |
| `birth_year` | 出生年 | 年齡分層與未來常模使用 |
| `education_level_code` | 教育程度代碼 | 教育程度修正使用 |
| `status` | 帳號狀態 | active / suspended / deleted |
| `created_at_utc` | 建立時間 | 帳號建立時間 |
| `last_login_at_utc` | 最後登入時間 | 使用狀況與客服查詢 |

## 2. `ltc.coin_transaction`（金幣異動）

| 欄位 | 中文 | 用途 |
|---|---|---|
| `transaction_id` | 異動流水號 | 主鍵 |
| `player_id` | 玩家流水號 | 金幣屬於誰 |
| `amount` | 異動數量 | 正數收入、負數支出 |
| `transaction_type` | 異動類型 | game_reward、purchase 等 |
| `reference_key` | 來源唯一碼 | 防止同一獎勵重複入帳 |
| `created_at_utc` | 發生時間 | 帳務稽核 |

金幣餘額不重複儲存，由 `ltc.coin_balance` 檢視加總。

## 3. `ltc.item`（商品／物品）

| 欄位 | 中文 | 用途 |
|---|---|---|
| `item_id` | 物品流水號 | 主鍵 |
| `item_code` | 物品代碼 | 程式與人工查詢使用 |
| `item_type` | 物品類型 | pet、food、decoration 等 |
| `name_zh_tw` | 中文名稱 | UI 顯示 |
| `price` | 金幣價格 | 商店售價 |
| `is_active` | 是否上架 | 停售時保留歷史資料 |

## 4. `ltc.player_inventory`（玩家背包）

| 欄位 | 中文 | 用途 |
|---|---|---|
| `player_id` | 玩家流水號 | 複合主鍵之一 |
| `item_id` | 物品流水號 | 複合主鍵之一 |
| `quantity` | 持有數量 | 玩家目前持有數 |
| `acquired_at_utc` | 首次取得時間 | 顯示與稽核 |

## 5. `cognitive.cognitive_domain`（認知能力分類）

| 欄位 | 中文 | 用途 |
|---|---|---|
| `domain_id` | 能力識別碼 | 固定 UUID 主鍵 |
| `domain_code` | 能力代碼 | 例如 attention_inhibition |
| `name_zh_tw` | 中文名稱 | UI 與報表顯示 |
| `description` | 說明 | 定義能力範圍 |

## 6. `cognitive.game`（認知遊戲）

| 欄位 | 中文 | 用途 |
|---|---|---|
| `game_id` | 遊戲識別碼 | 固定 UUID 主鍵 |
| `game_code` | 遊戲代碼 | Unity 上傳與查詢使用 |
| `name_zh_tw` | 中文名稱 | UI 與報表顯示 |
| `domain_id` | 主要認知能力 | 直接關聯認知分類 |
| `mapping_version` | 對應版本 | 未來修改理論對應時可追蹤 |
| `evidence_note` | 對應依據摘要 | 記錄遊戲為何衡量該能力 |
| `is_active` | 是否啟用 | 下架但保留歷史 |

## 7. `cognitive.assessment_session`（一次遊戲評估）

| 欄位 | 中文 | 用途 |
|---|---|---|
| `session_id` | 場次識別碼 | Unity 產生 UUID，防止重複上傳 |
| `player_id` | 玩家流水號 | 誰完成評估 |
| `game_id` | 遊戲識別碼 | 玩了哪個遊戲 |
| `task_version` | 遊戲規則版本 | 分數可比較性的依據 |
| `schema_version` | 上傳格式版本 | API 相容與資料遷移 |
| `started_at_utc` | 開始時間 | 趨勢與耗時計算 |
| `ended_at_utc` | 結束時間 | 趨勢與耗時計算 |
| `completion_status` | 完成狀態 | completed / aborted |
| `exit_reason` | 中止原因 | 非正常結束時使用 |
| `received_at_utc` | 伺服器收到時間 | 稽核與同步排錯 |

## 8. `cognitive.trial_event`（每一題／每一步作答）

| 欄位 | 中文 | 用途 |
|---|---|---|
| `session_id` | 場次識別碼 | 複合主鍵之一 |
| `trial_index` | 題目順序 | 複合主鍵之一，取代多餘 UUID |
| `trial_type` | 題型／條件 | congruent、incongruent 等 |
| `stimulus_json` | 刺激內容 | 不同遊戲的彈性欄位 |
| `expected_response` | 正確答案 | 判斷與重算依據 |
| `actual_response` | 玩家答案 | 行為資料 |
| `is_correct` | 是否正確 | 正確率計算 |
| `reaction_time_ms` | 反應時間毫秒 | 處理速度與離群檢查 |
| `presentation_duration_ms` | 題目呈現毫秒 | 確認測驗條件一致 |

## 9. `cognitive.derived_metric`（衍生認知指標）

| 欄位 | 中文 | 用途 |
|---|---|---|
| `metric_id` | 指標流水號 | 主鍵 |
| `session_id` | 場次識別碼 | 指標來自哪一場 |
| `domain_id` | 認知能力 | 指標屬於哪項能力 |
| `metric_code` | 指標代碼 | accuracy、median_rt 等 |
| `value` | 數值 | 指標結果 |
| `unit` | 單位 | percent、ms、score_0_100 等 |
| `calculation_version` | 計算版本 | 未來公式更新仍可追溯 |
| `quality_flag` | 資料品質 | valid / review / invalid |

## 給人查詢的四個檢視

- `ltc.player_directory`：玩家名冊，不顯示登入雜湊。
- `ltc.coin_balance`：玩家目前金幣餘額。
- `cognitive.assessment_summary`：一場一列，已串好玩家、遊戲、能力與耗時。
- `cognitive.cognitive_trend`：一個認知指標一列，適合依玩家與日期畫趨勢圖。
