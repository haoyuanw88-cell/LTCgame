# 超市關卡編輯器
1. 開啟 Tools > LTC > Supermarket Level Editor。
2. 按「新建」儲存關卡，或選擇既有關卡。
3. 設定關卡名稱、清單顯示秒數、必買商品與數量。點擊商品預覽可加入清單，重複點擊可增加數量。
4. 按「儲存並加入選關」。編輯器會檢查空清單、無圖片、數量小於一、重複商品及場景中無法購買的商品。
5. 執行 SupermarketGame 場景，即可選關遊玩。

預設關卡：Assets/supermarket/Levels。
目錄：Assets/Resources/SupermarketLevelCatalog.asset，可調整 levels 的順序或移除項目。
新關卡加入目錄後不需重建介面。重建介面選單：Tools > LTC > Bake Supermarket Level Selection。
附有日常採買、水果採買及早餐採買三關。
此編輯器是 Unity 開發工具，不包含於遊戲執行檔。
