# LTC 高齡認知遊戲：科學效度與驗證方案 v1.0

## 目前可以宣稱什麼

本系統目前是「遊戲化認知表現與個人趨勢追蹤原型」，不是失智症診斷工具，也尚未建立年齡或教育程度常模。任何 0–100 數值都只能稱為「任務指數」，不能稱為臨床認知分數、腦年齡或正常／異常判定。

數位認知測驗需要逐項建立可用性、重測信度、測量誤差及建構效度；不能因任務源自經典實驗，就直接宣稱自行開發的遊戲已具有相同效度。參考資料：

- [Digital Cognitive Assessment Tests for Older Adults: Systematic Review](https://pmc.ncbi.nlm.nih.gov/articles/PMC10746978/)
- [SMART older-adult web cognitive assessment validation](https://pmc.ncbi.nlm.nih.gov/articles/PMC8494835/)
- [Test–retest reliability and practice effects of a digital cognitive tool](https://pmc.ncbi.nlm.nih.gov/articles/PMC11735254/)
- [ICC selection and reporting guideline](https://pubmed.ncbi.nlm.nih.gov/27330520/)
- [FDA Digital Health Technologies guidance](https://www.fda.gov/regulatory-information/search-fda-guidance-documents/digital-health-technologies-remote-data-acquisition-clinical-investigations)

## 已鎖定的評估協定

| 遊戲 | 協定 ID | 主要建構 | 核心指標 | 最低品質要求 |
|---|---|---|---|---|
| 顏色文字判斷 | `LTC-ATT-STROOP-MATCH-01` | 選擇性注意與干擾控制 | 高低干擾反應時間差、干擾正確率差、正確率、RT MAD | 60 秒、至少 12 個有效正式試次、高低干擾各至少 4 題 |
| 數字排序 | `LTC-PS-NUMORDER-01` | 視覺搜尋、順序操作與處理速度 | 完成回合數、回合時間中位數、動作時間、錯誤率 | 60 秒、至少 3 個有效回合 |
| 數字組合 | `LTC-EF-NUMSUM-01` | 規則維持、數值工作記憶與規劃 | 完成回合率、無效操作率、每回合操作數、回合時間 | 60 秒、至少 3 個有效回合 |

所有正式比較必須使用：

- `taskVersion = 3.0.0`
- `protocolVersion = 3.0.0`
- `scoringVersion = 3.0.0`
- 固定 60 秒正式測驗條件
- 固定說明文字、按鈕配置與回饋方式

如果修改題目產生方式、時間、難度、提示或計分，必須提高版本號；不同主要版本不可直接放在同一組統計中。

## 資料品質與表現必須分開

低正確率可能正是需要觀察的認知表現，因此不得因低分直接刪除資料。只有以下技術或協定問題會讓資料不能進入趨勢：

- 遊戲版本與登錄協定不一致。
- 正式試次或回合數不足。
- 試次序號重複。
- 反應時間低於協定下限或高於上限的比例超過 20%。
- 超過 20% 的採樣點低於 20 FPS。
- 測驗時間短於 45 秒。
- Stroop 類任務的高低干擾條件不足或不平衡。

低正確率、高漏答率和接近滿分只產生「解讀警告」，不構成技術無效。這可避免把真正能力較弱的受測者系統性排除。

## 指標定義

### 所有遊戲共同指標

- `valid_trial_count`：通過技術檢查的正式試次或回合數。
- `excluded_trial_rate`：因反應時間或缺失被排除的比例。
- `completion_rate`：有實際回答的比例。
- `accuracy`：有效且已回答試次的正確率。
- `omission_rate`：漏答比例。
- `median_correct_rt`：正確反應時間中位數。
- `rt_mad`：反應時間中位絕對偏差。
- `robust_rt_variability`：MAD × 1.4826 的穩健變異估計。
- `inverse_efficiency`：正確率至少 50% 時，反應時間中位數除以正確率。
- `task_performance_index`：任務內描述性指數，不是常模分數。

### 干擾任務

- `low_interference_median_rt`
- `high_interference_median_rt`
- `interference_cost`＝高干擾 RT 中位數－低干擾 RT 中位數。
- `interference_ratio`＝高干擾 RT／低干擾 RT。
- `interference_accuracy_cost`＝低干擾正確率－高干擾正確率。

### 規劃任務

- `planning_invalid_action_rate`：造成超過目標的操作比例。
- `planning_actions_per_completed_round`：完成一個回合平均需要的操作數。

## 研究假設必須事先寫下

正式收案前先固定下列方向性假設，不可看完結果才挑有顯著的指標：

1. 干擾控制較佳者，高低干擾 RT 差與正確率差應較小。
2. 處理速度／視覺搜尋表現較佳者，數字排序完成回合較多、回合時間較短。
3. 執行與規劃表現較佳者，數字組合完成率較高、無效操作率較低。
4. 相同版本於 7–14 天內重測，主要指標應達到至少中等重測信度。
5. 第二次測驗可能有練習效應，因此需同時報告平均改變量，不能只報相關係數。

## 養老院可執行的三階段研究

### A. 可用性與內容效度

- 建議 8–12 位長者，加 2–3 位照護／高齡領域人員。
- 記錄完成率、需要協助次數、誤觸次數、說明理解度、文字可讀性及不適反應。
- 訪談任務是否符合預期能力、是否有文化或語言問題。
- 先修正操作障礙，再進行信度研究。

### B. 重測信度

- 目標至少 30 位完成兩次；間隔 7–14 天，兩次使用同一主要版本。
- 主要分析使用單次測量、絕對一致性的 ICC，報告 95% 信賴區間。
- 同時提供 Spearman 相關、Bland–Altman 圖與平均練習效應。
- 不只看 p 值；ICC 的信賴區間若很寬，必須誠實標示證據仍不確定。

### C. 初步建構／收斂效度

- 目標 40–60 人；若做不到，標示為 pilot study，不做診斷準確率宣稱。
- 由指導老師選擇可合法使用、有人具資格施測的既有工具。
- 在收案前指定每個遊戲的主要比較工具及主要指標。
- 使用 Spearman 或適當迴歸分析，報告效果量與 95% 信賴區間。
- 若沒有臨床診斷標籤，不計算敏感度、特異度或 AUC。

## 收案最小欄位

- 系統玩家代碼，不保存姓名於研究匯出資料。
- 出生年或年齡層。
- 教育程度代碼。
- 是否需要操作協助。
- 測驗日期、測驗輪次、協定與計分版本。
- 每題原始資料及所有衍生指標。
- 外部比較工具結果與施測者代碼。
- 同意狀態及退出狀態。

研究匯出資料不得包含 Google 原始 UID、登入雜湊或可直接辨識個人的資料。

## 評鑑時的正確說法

可以說：

> 本系統以版本化協定蒐集每題反應、反應時間、錯誤型態與執行品質，並將技術品質和能力表現分開。現階段提供個人趨勢觀察，接著以養老院樣本檢驗可用性、重測信度及與既有工具的關聯。

不可說：

> 玩一次即可診斷失智、任務指數等於認知能力百分數，或目前分數已具有醫療常模。
