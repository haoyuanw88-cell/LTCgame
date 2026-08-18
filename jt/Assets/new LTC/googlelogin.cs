using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using LTC.Identity;

/// <summary>
/// Google OpenID Connect login for Unity Editor and desktop builds.
/// Uses Authorization Code + PKCE, validates state, and sends only the signed
/// Google ID token to the LTC backend. No Google client secret is stored here.
/// </summary>
public sealed class googlelogin : MonoBehaviour
{
    const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    [Header("Google 設定（桌面應用程式 OAuth 用戶端）")]
    public string clientID = "969364101892-1nvsfsd5immh713adnn04l87ss9vfo60.apps.googleusercontent.com";
    public string redirectURI = "http://localhost:5000/";

    [Header("UI 連結")]
    public TextMeshProUGUI statusText;
    public Image loginButtonImage;
    public Sprite loggedInSprite;

    readonly Queue<Action> mainThreadQueue = new Queue<Action>();
    HttpListener listener;
    string pendingState;
    string pendingNonce;
    string codeVerifier;
    bool loginInProgress;

    void Update()
    {
        lock (mainThreadQueue)
        {
            while (mainThreadQueue.Count > 0)
                mainThreadQueue.Dequeue()?.Invoke();
        }
    }

    public void StartLogin()
    {
        if (loginInProgress)
        {
            SetStatus("Google 登入正在進行中…");
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        SetStatus("Android 版需設定 Google 原生登入，請先使用電腦版");
        Debug.LogWarning("Android 請改接 Credential Manager / Google Sign-In 原生套件；不可使用桌面 localhost 回呼。");
        return;
#else
        if (!TryValidateSettings(out Uri callbackUri, out string validationError))
        {
            SetStatus(validationError);
            Debug.LogError(validationError);
            return;
        }

        try
        {
            StopListener();
            pendingState = CreateRandomUrlSafeValue(32);
            pendingNonce = CreateRandomUrlSafeValue(32);
            codeVerifier = CreateRandomUrlSafeValue(48);

            listener = new HttpListener();
            listener.Prefixes.Add(callbackUri.AbsoluteUri);
            listener.Start();
            listener.BeginGetContext(OnRequestReceived, listener);

            loginInProgress = true;
            SetButtonInteractable(false);
            SetStatus("請在瀏覽器選擇 Google 帳號…");
            Application.OpenURL(BuildAuthorizationUrl(callbackUri.AbsoluteUri));
        }
        catch (Exception exception)
        {
            FinishWithError("無法啟動 Google 登入");
            Debug.LogError("Google 登入啟動失敗：" + exception.Message);
        }
#endif
    }

    string BuildAuthorizationUrl(string callbackUri)
    {
        string challenge;
        using (var sha256 = SHA256.Create())
            challenge = ToBase64Url(sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier)));

        var parameters = new Dictionary<string, string>
        {
            { "client_id", clientID.Trim() },
            { "redirect_uri", callbackUri },
            { "response_type", "code" },
            { "scope", "openid email profile" },
            { "code_challenge", challenge },
            { "code_challenge_method", "S256" },
            { "state", pendingState },
            { "nonce", pendingNonce },
            { "prompt", "select_account" },
            { "include_granted_scopes", "true" }
        };

        var query = new StringBuilder();
        foreach (var pair in parameters)
        {
            if (query.Length > 0) query.Append('&');
            query.Append(Uri.EscapeDataString(pair.Key));
            query.Append('=');
            query.Append(Uri.EscapeDataString(pair.Value));
        }
        return AuthorizationEndpoint + "?" + query;
    }

    void OnRequestReceived(IAsyncResult asyncResult)
    {
        HttpListener activeListener = asyncResult.AsyncState as HttpListener;
        if (activeListener == null || !activeListener.IsListening) return;

        HttpListenerContext context;
        try { context = activeListener.EndGetContext(asyncResult); }
        catch (ObjectDisposedException) { return; }
        catch (HttpListenerException) { return; }
        catch (Exception exception)
        {
            EnqueueMainThread(() => FinishWithError("登入回呼讀取失敗：" + exception.Message));
            return;
        }

        string returnedState = context.Request.QueryString["state"];
        string code = context.Request.QueryString["code"];
        string oauthError = context.Request.QueryString["error"];

        if (!string.IsNullOrEmpty(oauthError))
        {
            SendHtml(context, "登入已取消", "你可以關閉此頁面並回到遊戲重新嘗試。");
            StopListener();
            EnqueueMainThread(() => FinishWithError("Google 登入已取消"));
            return;
        }

        if (string.IsNullOrEmpty(code))
        {
            SendHtml(context, "等待登入", "請回到 Google 登入頁完成帳號選擇。");
            TryListenAgain(activeListener);
            return;
        }

        if (string.IsNullOrEmpty(returnedState) || returnedState != pendingState)
        {
            SendHtml(context, "登入驗證失敗", "安全驗證資料不一致，請回到遊戲重新登入。");
            StopListener();
            EnqueueMainThread(() => FinishWithError("登入安全驗證失敗，請重新嘗試"));
            return;
        }

        SendHtml(context, "登入完成", "身分正在驗證，現在可以關閉此頁面並回到遊戲。");
        StopListener();
        EnqueueMainThread(() => StartCoroutine(ExchangeAuthorizationCode(code)));
    }

    IEnumerator ExchangeAuthorizationCode(string authorizationCode)
    {
        var form = new WWWForm();
        form.AddField("client_id", clientID.Trim());
        form.AddField("code", authorizationCode);
        form.AddField("code_verifier", codeVerifier);
        form.AddField("grant_type", "authorization_code");
        form.AddField("redirect_uri", redirectURI.Trim());

        using (UnityWebRequest request = UnityWebRequest.Post(TokenEndpoint, form))
        {
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Google 授權碼交換失敗：{request.responseCode} {request.error}");
                FinishWithError("Google 驗證失敗，請確認 OAuth 桌面用戶端設定");
                yield break;
            }

            GoogleTokenResponse tokenResponse = null;
            try { tokenResponse = JsonUtility.FromJson<GoogleTokenResponse>(request.downloadHandler.text); }
            catch (Exception exception) { Debug.LogWarning("Google Token 回應無法解析：" + exception.Message); }
            if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.id_token))
            {
                FinishWithError("Google 沒有回傳身分憑證，請重新登入");
                yield break;
            }

            SetStatus("正在建立遊戲帳號…");
            PlayerIdentityService.SignInWithGoogle(tokenResponse.id_token, pendingNonce, OnBackendLoginCompleted);
        }
    }

    void OnBackendLoginCompleted(bool success, string message)
    {
        loginInProgress = false;
        ClearTransientSecrets();
        SetButtonInteractable(true);
        SetStatus(message);
        if (!success) return;

        if (loginButtonImage != null && loggedInSprite != null)
            loginButtonImage.sprite = loggedInSprite;
        Debug.Log("Google 登入及 LTC 玩家身分驗證成功：" + PlayerIdentityService.Current.PlayerCode);
    }

    bool TryValidateSettings(out Uri callbackUri, out string error)
    {
        callbackUri = null;
        error = null;
        if (string.IsNullOrWhiteSpace(clientID) || !clientID.Trim().EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal))
        {
            error = "Google Client ID 尚未正確設定";
            return false;
        }
        if (!Uri.TryCreate(redirectURI.Trim(), UriKind.Absolute, out callbackUri) ||
            callbackUri.Scheme != Uri.UriSchemeHttp || !callbackUri.IsLoopback ||
            !callbackUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
        {
            error = "登入回呼網址必須是以 / 結尾的 localhost 網址";
            return false;
        }
        return true;
    }

    void FinishWithError(string message)
    {
        StopListener();
        loginInProgress = false;
        ClearTransientSecrets();
        SetButtonInteractable(true);
        SetStatus(message);
    }

    void EnqueueMainThread(Action action)
    {
        lock (mainThreadQueue) mainThreadQueue.Enqueue(action);
    }

    void TryListenAgain(HttpListener activeListener)
    {
        try
        {
            if (activeListener != null && activeListener.IsListening)
                activeListener.BeginGetContext(OnRequestReceived, activeListener);
        }
        catch { }
    }

    void SendHtml(HttpListenerContext context, string title, string message)
    {
        string html = "<!doctype html><html lang='zh-Hant'><head><meta charset='utf-8'>" +
                      "<meta name='viewport' content='width=device-width,initial-scale=1'>" +
                      "<title>" + WebUtility.HtmlEncode(title) + "</title></head>" +
                      "<body style='font-family:sans-serif;text-align:center;padding:56px;background:#f4f8f5;color:#173b36'>" +
                      "<h1>" + WebUtility.HtmlEncode(title) + "</h1><p>" + WebUtility.HtmlEncode(message) + "</p></body></html>";
        byte[] buffer = Encoding.UTF8.GetBytes(html);
        try
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        finally { context.Response.Close(); }
    }

    void SetStatus(string message)
    {
        if (statusText == null) return;
        statusText.gameObject.SetActive(true);
        statusText.text = message;
    }

    void SetButtonInteractable(bool interactable)
    {
        if (loginButtonImage == null) return;
        Button button = loginButtonImage.GetComponent<Button>();
        if (button != null) button.interactable = interactable;
    }

    static string CreateRandomUrlSafeValue(int byteCount)
    {
        byte[] bytes = new byte[byteCount];
        using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            generator.GetBytes(bytes);
        return ToBase64Url(bytes);
    }

    static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    void ClearTransientSecrets()
    {
        pendingState = null;
        pendingNonce = null;
        codeVerifier = null;
    }

    void StopListener()
    {
        if (listener == null) return;
        try { listener.Stop(); listener.Close(); }
        catch { }
        listener = null;
    }

    void OnDestroy()
    {
        StopListener();
        ClearTransientSecrets();
    }

    [Serializable]
    sealed class GoogleTokenResponse
    {
        public string access_token;
        public string id_token;
        public string token_type;
        public int expires_in;
        public string error;
        public string error_description;
    }
}
