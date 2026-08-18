using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using LTC.Identity;
using UnityEngine;
using UnityEngine.Networking;

public sealed class CoinCloudService : MonoBehaviour
{
    const string DefaultApiBaseUrl = "https://staging-hello-8shi.encr.app";
    const string LastDailyLoginKey = "LTC_LastDailyLoginDate";
    static CoinCloudService instance;
    IPlayerIdentityProvider identity;
    Coroutine refreshRoutine;

    string ApiBaseUrl => PlayerPrefs.GetString(
        PlayerIdentityService.ApiBaseUrlPlayerPrefsKey, DefaultApiBaseUrl).Trim().TrimEnd('/');

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize() { EnsureInstance(); }

    static CoinCloudService EnsureInstance()
    {
        if (instance != null) return instance;
        var host = new GameObject("Coin Cloud Service");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<CoinCloudService>();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        identity = PlayerIdentityService.Current;
        identity.IdentityChanged += OnIdentityChanged;
        OnIdentityChanged();
    }

    void OnDestroy()
    {
        if (identity != null) identity.IdentityChanged -= OnIdentityChanged;
        if (instance == this) instance = null;
    }

    void OnIdentityChanged()
    {
        if (refreshRoutine != null) StopCoroutine(refreshRoutine);
        refreshRoutine = identity != null && identity.IsReady ? StartCoroutine(RefreshWalletRoutine()) : null;
    }

    public static void RefreshWallet()
    {
        var service = EnsureInstance();
        if (service.refreshRoutine != null) service.StopCoroutine(service.refreshRoutine);
        if (PlayerIdentityService.Current.IsReady)
            service.refreshRoutine = service.StartCoroutine(service.RefreshWalletRoutine());
    }

    public static void ClaimDailyReward(Action<bool, bool, string> completed)
    {
        var service = EnsureInstance();
        if (!PlayerIdentityService.Current.IsReady)
        {
            completed?.Invoke(false, false, "尚未連上玩家帳號，請稍後再試");
            return;
        }
        service.StartCoroutine(service.ClaimDailyRoutine(completed));
    }

    public static void Purchase(string itemCode, int quantity, Action<CoinPurchaseResult> completed)
    {
        var service = EnsureInstance();
        if (!PlayerIdentityService.Current.IsReady)
        {
            completed?.Invoke(CoinPurchaseResult.Failed("尚未連上玩家帳號，請稍後再試"));
            return;
        }
        service.StartCoroutine(service.PurchaseRoutine(itemCode, quantity, completed));
    }

    IEnumerator RefreshWalletRoutine()
    {
        using (var request = UnityWebRequest.Get(ApiBaseUrl + "/api/v1/wallet"))
        {
            AddAuthorization(request);
            request.timeout = 10;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                WalletResponse response = null;
                try { response = JsonUtility.FromJson<WalletResponse>(request.downloadHandler.text); }
                catch (Exception exception) { Debug.LogWarning("雲端錢包資料格式錯誤：" + exception.Message); }
                if (response != null && response.balance >= 0)
                {
                    CoinData.SetCoins(response.balance);
                    if (response.dailyClaimed)
                    {
                        PlayerPrefs.SetString(LastDailyLoginKey, DateTime.Now.ToString("yyyy-MM-dd"));
                        PlayerPrefs.Save();
                    }
                    else if (PlayerPrefs.GetString(LastDailyLoginKey, string.Empty) == DateTime.Now.ToString("yyyy-MM-dd"))
                    {
                        PlayerPrefs.DeleteKey(LastDailyLoginKey);
                        PlayerPrefs.Save();
                    }
                }
            }
            else
            {
                Debug.LogWarning("雲端錢包暫時無法同步，本機顯示值會保留：" + request.error);
            }
        }
        refreshRoutine = null;
    }

    IEnumerator ClaimDailyRoutine(Action<bool, bool, string> completed)
    {
        using (var request = CreatePost(ApiBaseUrl + "/api/v1/wallet/daily", "{}"))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                completed?.Invoke(false, false, "每日獎勵暫時無法領取，請確認網路後重試");
                yield break;
            }
            DailyRewardResponse response = null;
            try { response = JsonUtility.FromJson<DailyRewardResponse>(request.downloadHandler.text); }
            catch (Exception exception) { Debug.LogWarning("每日獎勵回應格式錯誤：" + exception.Message); }
            if (response == null)
            {
                completed?.Invoke(false, false, "每日獎勵回應無法讀取");
                yield break;
            }
            CoinData.SetCoins(response.balance);
            PlayerPrefs.SetString(LastDailyLoginKey, DateTime.Now.ToString("yyyy-MM-dd"));
            PlayerPrefs.Save();
            completed?.Invoke(true, response.claimed,
                response.claimed ? "已領取 20 金幣" : "今天的登入獎勵已經領取");
        }
    }

    IEnumerator PurchaseRoutine(string itemCode, int quantity, Action<CoinPurchaseResult> completed)
    {
        var payload = new PurchaseRequest
        {
            operationId = CreateOperationId(),
            itemCode = (itemCode ?? string.Empty).Trim().ToUpperInvariant(),
            quantity = Mathf.Max(1, quantity)
        };
        using (var request = CreatePost(ApiBaseUrl + "/api/v1/store/purchase", JsonUtility.ToJson(payload)))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                string message = request.responseCode == 400
                    ? "金幣不足或商品資料尚未上架"
                    : "購買未完成，請確認網路後再試";
                completed?.Invoke(CoinPurchaseResult.Failed(message));
                yield break;
            }
            PurchaseResponse response = null;
            try { response = JsonUtility.FromJson<PurchaseResponse>(request.downloadHandler.text); }
            catch (Exception exception) { Debug.LogWarning("購買回應格式錯誤：" + exception.Message); }
            if (response == null)
            {
                completed?.Invoke(CoinPurchaseResult.Failed("購買回應無法讀取"));
                yield break;
            }
            CoinData.SetCoins(response.balance);
            completed?.Invoke(new CoinPurchaseResult
            {
                success = true,
                message = response.created ? "購買成功" : "這筆購買已完成",
                itemQuantity = response.itemQuantity,
                balance = response.balance
            });
        }
    }

    UnityWebRequest CreatePost(string url, string json)
    {
        var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        AddAuthorization(request);
        request.timeout = 10;
        return request;
    }

    static void AddAuthorization(UnityWebRequest request)
    {
        request.SetRequestHeader("Authorization", "Bearer " + PlayerIdentityService.Current.AccessToken);
    }

    static string CreateOperationId()
    {
        var bytes = new byte[4];
        using (var generator = RandomNumberGenerator.Create()) generator.GetBytes(bytes);
        uint value = BitConverter.ToUInt32(bytes, 0) % 100000000u;
        return "B" + value.ToString("D8");
    }

    [Serializable] sealed class WalletResponse
    {
        public int balance;
        public bool dailyClaimed;
    }

    [Serializable] sealed class DailyRewardResponse
    {
        public bool claimed;
        public int reward;
        public int balance;
    }

    [Serializable] sealed class PurchaseRequest
    {
        public string operationId;
        public string itemCode;
        public int quantity;
    }

    [Serializable] sealed class PurchaseResponse
    {
        public string transactionId;
        public string itemCode;
        public int itemQuantity;
        public int spent;
        public int balance;
        public bool created;
    }
}

public sealed class CoinPurchaseResult
{
    public bool success;
    public string message;
    public int itemQuantity;
    public int balance;

    public static CoinPurchaseResult Failed(string message)
    {
        return new CoinPurchaseResult { success = false, message = message };
    }
}
