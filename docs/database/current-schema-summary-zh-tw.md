# 目前資料庫架構（9 張表）

> 本文件描述目前程式碼與既有 Migration；2026-08-03 本次工作沒有修改資料庫。

## `ltc.player` 玩家

`player_id` 玩家流水號（PK）、`player_code` 可讀玩家代碼、`auth_provider` 登入來源、
`auth_subject_hash` 登入識別雜湊、`display_name` 顯示名稱、`birth_year` 出生年、
`education_level_code` 教育程度代碼、`status` 帳號狀態、`created_at_utc` 建立時間、
`last_login_at_utc` 最後登入時間。無 FK。

## `ltc.coin_transaction` 金幣交易

`transaction_id` 交易流水號（PK）、`player_id` 玩家（FK → `ltc.player.player_id`）、
`amount` 金幣增減量、`transaction_type` 交易類型、`reference_key` 來源／冪等鍵、
`created_at_utc` 建立時間。

## `ltc.item` 商品

`item_id` 商品流水號（PK）、`item_code` 商品代碼、`item_type` 商品類型、
`name_zh_tw` 中文名稱、`price` 價格、`is_active` 是否上架。無 FK。

## `ltc.player_inventory` 玩家持有物

`player_id` 玩家（PK、FK → `ltc.player.player_id`）、
`item_id` 商品（PK、FK → `ltc.item.item_id`）、`quantity` 數量、
`acquired_at_utc` 首次取得時間。複合 PK：`player_id + item_id`。

## `cognitive.cognitive_domain` 認知能力分類

`domain_id` 能力 UUID（PK）、`domain_code` 能力代碼、`name_zh_tw` 中文名稱、
`description` 說明。無 FK。

## `cognitive.game` 遊戲定義

`game_id` 遊戲 UUID（PK）、`game_code` 遊戲代碼、`name_zh_tw` 中文名稱、
`domain_id` 主要能力（FK → `cognitive.cognitive_domain.domain_id`）、
`mapping_version` 能力對應版本、`evidence_note` 依據說明、`is_active` 是否啟用。

## `cognitive.assessment_session` 一次測驗場次

`session_id` 場次 UUID（PK）、`player_id` 玩家（FK → `ltc.player.player_id`）、
`game_id` 遊戲（FK → `cognitive.game.game_id`）、`task_version` 任務版本、
`schema_version` 上傳格式版本、`started_at_utc` 開始時間、`ended_at_utc` 結束時間、
`completion_status` 完成狀態、`exit_reason` 離開原因、`received_at_utc` 伺服器接收時間。

## `cognitive.trial_event` 題目／操作事件

`session_id` 場次（PK、FK → `cognitive.assessment_session.session_id`）、
`trial_index` 場次內題號（PK）、`trial_type` 條件／事件類型、`stimulus_json` 題目與程序資料、
`expected_response` 正確反應、`actual_response` 玩家反應、`is_correct` 是否正確、
`reaction_time_ms` 反應時間、`presentation_duration_ms` 題目呈現時間。
複合 PK：`session_id + trial_index`。

## `cognitive.derived_metric` 衍生指標

`metric_id` 指標流水號（PK）、`session_id` 場次（FK → `cognitive.assessment_session.session_id`）、
`domain_id` 能力（FK → `cognitive.cognitive_domain.domain_id`）、`metric_code` 指標代碼、
`value` 數值、`unit` 單位、`calculation_version` 計算版本、`quality_flag` 資料品質。

