using UnityEngine;
using System.Net;
using System;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine.UI;

public class googlelogin : MonoBehaviour
{
    [Header("Google 設定")]
    public string clientID = "969364101892-1nvsfsd5immh713ac.apps.googleusercontent.com";
    public string redirectURI = "http://localhost:5000/";

    [Header("UI 連結")]
    public TextMeshProUGUI statusText;
    public Image loginButtonImage;    // 指派你的按鈕 Image 組件
    public Sprite loggedInSprite;     // 指派那張「登入完成」的圖片

    private HttpListener listener;
    private static Queue<Action> _mainThreadQueue = new Queue<Action>();

    void Update()
    {
        lock (_mainThreadQueue)
        {
            while (_mainThreadQueue.Count > 0) _mainThreadQueue.Dequeue().Invoke();
        }
    }

    public void StartLogin()
    {
        if (statusText == null)
        {
            Debug.LogError("🔴 尚未指派 Status Text！");
            return;
        }

        try
        {
            StopListener();
            listener = new HttpListener();
            listener.Prefixes.Add(redirectURI);
            listener.Start();
            listener.BeginGetContext(new AsyncCallback(OnRequestReceived), listener);

            Debug.Log("🌐 啟動 Google 登入，開啟瀏覽器...");

            string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?response_type=token&client_id={clientID}&redirect_uri={redirectURI}&scope=https://www.googleapis.com/auth/userinfo.profile";
            Application.OpenURL(authUrl);
        }
        catch (Exception e)
        {
            Debug.LogError("❌ 啟動失敗: " + e.Message);
            statusText.text = "連線出錯";
        }
    }

    private void OnRequestReceived(IAsyncResult result)
    {
        if (listener == null || !listener.IsListening) return;

        var context = listener.EndGetContext(result);
        string url = context.Request.Url.ToString();

        if (url.Contains("token="))
        {
            string accessToken = url.Split(new[] { "token=" }, StringSplitOptions.None)[1].Split('&')[0];
            SendHtml(context, "<html><body style='text-align:center;'><h1>登入成功！</h1><p>請回到 Unity 遊戲視窗。</p></body></html>");

            lock (_mainThreadQueue)
            {
                _mainThreadQueue.Enqueue(() => {
                    Debug.Log("🔑 已取得 Token，正在背景請求資料...");
                    StartCoroutine(FetchGoogleProfile(accessToken));
                });
            }
            StopListener();
        }
        else
        {
            string js = "<html><script>if(window.location.hash) window.location.href='/token?'+window.location.hash.substring(1);</script><body>正在導向...</body></html>";
            SendHtml(context, js);
            listener.BeginGetContext(new AsyncCallback(OnRequestReceived), listener);
        }
    }

    private IEnumerator FetchGoogleProfile(string accessToken)
    {
        // 請求使用者資訊的 URL
        string profileUrl = "https://www.googleapis.com/oauth2/v3/userinfo?access_token=" + accessToken;

        using (UnityWebRequest webRequest = UnityWebRequest.Get(profileUrl))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = webRequest.downloadHandler.text;

                // --- 解析 User ID (Google 內部欄位名稱為 sub) ---
                string userId = ParseJsonValue(jsonResponse, "sub");
                string userName = ParseJsonValue(jsonResponse, "name");

                // --- 在控制台回傳顯示 ---
                Debug.Log("✅ [Google Login Success]");
                Debug.Log("🆔 User ID (sub): " + userId);
                Debug.Log("👤 User Name: " + userName);

                // --- 更新 UI ---
                if (statusText != null) statusText.gameObject.SetActive(false);

                if (loginButtonImage != null && loggedInSprite != null)
                {
                    loginButtonImage.sprite = loggedInSprite;
                }
            }
            else
            {
                Debug.LogError("❌ 資料抓取失敗: " + webRequest.error);
                if (statusText != null) statusText.text = "登入失敗";
            }
        }
    }

    // 簡易 JSON 解析器 (抓取 key 對應的字串值)
    private string ParseJsonValue(string json, string key)
    {
        string search = "\"" + key + "\": \"";
        int start = json.IndexOf(search);
        if (start == -1) return "未找到";
        start += search.Length;
        int end = json.IndexOf("\"", start);
        return json.Substring(start, end - start);
    }

    private void SendHtml(HttpListenerContext context, string html)
    {
        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html";
        context.Response.ContentEncoding = System.Text.Encoding.UTF8;
        context.Response.ContentLength64 = buffer.Length;
        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.OutputStream.Close();
    }

    private void StopListener()
    {
        if (listener != null)
        {
            try { listener.Stop(); listener.Close(); } catch { }
            listener = null;
        }
    }

    private void OnDestroy() => StopListener();
}