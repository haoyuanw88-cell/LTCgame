# 第三階段安全後端代理契約

Unity 不得直接呼叫 OpenAI，也不得保存 OpenAI API Key。所有視覺請求都必須先通過 LTC 後端驗證玩家 Access Token。

## Endpoint

`POST /api/v1/assistant/analyze`

Headers:

- `Authorization: Bearer <LTC player access token>`
- `Content-Type: application/json`

Request:

```json
{
  "requestId": "client-generated-id",
  "schemaVersion": "1.0",
  "sourceName": "selected game window",
  "capturedAtUtc": "2026-11-10T00:00:00.0000000Z",
  "imageMediaType": "image/jpeg",
  "imageBase64": "...",
  "language": "zh-TW"
}
```

Success response:

```json
{
  "success": true,
  "analysis": {
    "schemaVersion": "1.0",
    "summary": "目前畫面的簡短摘要",
    "overallConfidence": 0.78,
    "uncertain": true,
    "uncertaintyReason": "部分文字太小",
    "observations": [
      {
        "label": "有未完成的任務",
        "evidence": "右側任務區顯示 1/3",
        "confidence": 0.73,
        "uncertain": false
      }
    ],
    "ocrTexts": [
      {
        "text": "任務進度 1/3",
        "confidence": 0.69,
        "region": "右上方"
      }
    ],
    "events": [],
    "suggestedAdvice": [
      "你可以確認右側任務區，但辨識結果可能不完整。"
    ]
  },
  "error": ""
}
```

Failure response:

```json
{
  "success": false,
  "analysis": null,
  "error": "human-readable error"
}
```

## Backend responsibilities

1. 驗證 LTC Access Token、玩家身分與速率限制。
2. 限制 JPEG MIME、解碼後尺寸與 request body 大小。
3. 從伺服器環境變數讀取 OpenAI API Key；不得回傳給 Unity。
4. 使用支援影像輸入的 OpenAI Responses API。
5. 使用 `text.format.type = json_schema`、`strict = true`，讓輸出符合上述結構。
6. Schema 中所有物件使用 `additionalProperties: false`，所有 UI 需要的欄位設為 required。
7. 處理 refusal、逾時、不完整輸出與 schema 驗證失敗。
8. 預設不保存原始遊戲截圖；若未來需要記錄，必須先取得明確同意並設定保留期限。
9. 模型名稱與 OpenAI Key 均由後端設定，不由 Unity 客戶端指定。

## Safety rules

- 模型只能提供建議，不能要求 Unity 注入滑鼠或鍵盤操作。
- 無證據或低可信度內容必須設定 `uncertain: true`。
- 每個觀察與事件都必須附帶 `confidence` 與 `evidence`。
- OCR 文字不得自動視為正確；Unity 仍會再次套用最低可信度門檻。
