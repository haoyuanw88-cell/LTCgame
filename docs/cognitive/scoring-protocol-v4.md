# 三款核心遊戲計分協定 4.0

這些結果是與認知作業相對應的遊戲行為指標，不是失智診斷或臨床量表分數。尚未以目標族群建立常模前，不把單次結果換算為正常／異常。

## 顏色文字判斷：Stroop 干擾

主要結果：`stroop_rt_interference`（ms）

`高干擾正確反應時間中位數 − 低干擾正確反應時間中位數`

另存高干擾錯誤率減低干擾錯誤率。差值越大表示受到衝突刺激的干擾越明顯。依據為 Stroop 對衝突條件與基準條件反應時間的比較；速度與正確率分開保存，避免只依賴合成分數。

## 數字排序：類 Trail Making Test A

主要結果：`trail_total_completion_time`（ms）

`所有完成數字路徑的完成時間總和`

答錯不結束回合，玩家必須修正並繼續，錯誤造成的延遲會保留在總完成時間；另存順序錯誤數、完成路徑數與完成率。這與 TMT-A 以完成時間為主要分數、同時記錄錯誤的做法一致，但本遊戲不是正式 TMT。

## 數字組合：類 Tower of London 規劃指標

主要結果：`planning_optimal_solution_rate`（%）

`以理論最少操作數且無超額錯誤完成的回合數 ÷ 完成回合數 × 100`

每題以 subset-sum 搜尋理論最少選取數，另存：

- 超額步數＝實際操作數−理論最少操作數
- 首次思考時間＝出題到第一次操作
- 執行時間＝第一次操作到完成
- 規則違反＝總和超過目標的操作次數

這些對應 Tower of London 常見的最少步數解、超額移動、初始思考時間、執行時間與規則違反；本遊戲屬數字組合改編版，不可冒稱正式 Tower of London。

## 參考

- Stroop, J. R. (1935). *Studies of interference in serial verbal reactions*. Journal of Experimental Psychology, 18, 643–662.
- Bruyer, R., & Brysbaert, M. (2011). *Combining speed and accuracy in cognitive psychology*. Psychologica Belgica, 51(1), 5–13.
- Ashendorf, L. et al. (2008). *Trail Making Test errors in normal aging, mild cognitive impairment, and dementia*. Archives of Clinical Neuropsychology, 23(2), 129–137.
- Shallice, T. (1982). *Specific impairments of planning*. Philosophical Transactions of the Royal Society B, 298, 199–209.
