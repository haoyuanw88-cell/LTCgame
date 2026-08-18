using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace LTC.Identity
{
    public interface IPlayerIdentityProvider
    {
        bool IsReady { get; }
        long PlayerId { get; }
        string PlayerCode { get; }
        string AccessToken { get; }
        string InstallationUid { get; }
        string StableLocalPlayerKey { get; }
        event Action IdentityChanged;
        void Refresh();
    }

    public sealed class PlayerIdentityService : MonoBehaviour, IPlayerIdentityProvider
    {
        public const string ApiBaseUrlPlayerPrefsKey = "LTC_CognitiveApiBaseUrl";
        const string DefaultApiBaseUrl = "https://staging-hello-8shi.encr.app";
        const int MaximumRetryDelaySeconds = 60;
        const int HeartbeatIntervalSeconds = 60;
        static PlayerIdentityService instance;
        Coroutine signInRoutine;
        Coroutine heartbeatRoutine;
        Coroutine profileSyncRoutine;
        Coroutine googleSignInRoutine;
        bool connectionWarningLogged;

        public static IPlayerIdentityProvider Current
        {
            get
            {
                if (instance == null)
                {
                    var host = new GameObject("Player Identity Service");
                    DontDestroyOnLoad(host);
                    instance = host.AddComponent<PlayerIdentityService>();
                }
                return instance;
            }
        }

        public bool IsReady { get; private set; }
        public long PlayerId { get; private set; }
        public string PlayerCode { get; private set; } = string.Empty;
        public string AccessToken { get; private set; } = string.Empty;
        public string InstallationUid { get; private set; } = string.Empty;
        public string AuthProvider { get; private set; } = "guest";
        public string StableLocalPlayerKey => "guest-" + InstallationUid;
        public event Action IdentityChanged;

        string ApiBaseUrl => PlayerPrefs.GetString(ApiBaseUrlPlayerPrefsKey, DefaultApiBaseUrl).Trim().TrimEnd('/');
        string IdentityDirectory => Path.Combine(Application.persistentDataPath, "Identity");
        string InstallationFilePath => Path.Combine(IdentityDirectory, "installation.id");
        string SessionFilePath => Path.Combine(IdentityDirectory, "player.session.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize() { _ = Current; }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            InstallationUid = LoadOrCreateInstallationUid();
            if (!TryRestoreCachedSession()) Refresh();
        }

        void OnDisable()
        {
            StopAllCoroutines();
            signInRoutine = null;
            heartbeatRoutine = null;
            profileSyncRoutine = null;
            googleSignInRoutine = null;
        }

        void OnDestroy()
        {
            StopAllCoroutines();
            heartbeatRoutine = null;
            profileSyncRoutine = null;
            googleSignInRoutine = null;
            if (instance == this) instance = null;
        }


        public void Refresh()
        {
            if (signInRoutine != null) StopCoroutine(signInRoutine);
            if (heartbeatRoutine != null) StopCoroutine(heartbeatRoutine);
            if (googleSignInRoutine != null) StopCoroutine(googleSignInRoutine);
            IsReady = false;
            AccessToken = string.Empty;
            heartbeatRoutine = null;
            googleSignInRoutine = null;
            signInRoutine = StartCoroutine(SignInWithRetry());
        }

        public static void SignInWithGoogle(string idToken, string nonce, Action<bool, string> completed)
        {
            var service = Current as PlayerIdentityService;
            if (service == null)
            {
                completed?.Invoke(false, "玩家身分服務尚未啟動");
                return;
            }
            if (service.googleSignInRoutine != null)
            {
                completed?.Invoke(false, "Google 登入正在處理中");
                return;
            }
            service.googleSignInRoutine = service.StartCoroutine(
                service.GoogleSignInRoutine(idToken, nonce, completed));
        }

        public static void SyncCurrentProfile()
        {
            var service = Current as PlayerIdentityService;
            if (service == null || !service.IsReady) return;
            if (service.profileSyncRoutine != null) service.StopCoroutine(service.profileSyncRoutine);
            service.profileSyncRoutine = service.StartCoroutine(service.SyncProfileRoutine());
        }

        IEnumerator SignInWithRetry()
        {
            int delaySeconds = 2;
            while (isActiveAndEnabled && !IsReady)
            {
                var payload = new GuestSignInRequest
                {
                    installationUid = InstallationUid,
                    displayName = ResolveDisplayName()
                };
                byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

                using (var request = new UnityWebRequest(ApiBaseUrl + "/api/v2/auth/guest", UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = 10;
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        PlayerSessionResponse response = null;
                        try { response = JsonUtility.FromJson<PlayerSessionResponse>(request.downloadHandler.text); }
                        catch (Exception exception) { Debug.LogWarning("玩家身分服務回應格式無法解析：" + exception.Message); }

                        if (response != null && response.playerId > 0 &&
                            !string.IsNullOrWhiteSpace(response.playerCode) &&
                            !string.IsNullOrWhiteSpace(response.accessToken))
                        {
                            ApplySession(response, "guest", true);
                            break;
                        }
                        if (response != null) Debug.LogWarning("玩家身分服務回傳了不完整的登入資料。");
                    }
                    else if (!connectionWarningLogged)
                    {
                        connectionWarningLogged = true;
                        Debug.LogWarning($"玩家身分服務暫時無法連線，資料仍會保存在本機：{request.responseCode} {request.error}");
                    }
                }

                if (!IsReady && isActiveAndEnabled)
                {
                    yield return new WaitForSecondsRealtime(delaySeconds);
                    delaySeconds = Mathf.Min(delaySeconds * 2, MaximumRetryDelaySeconds);
                }
            }
            signInRoutine = null;
        }

        IEnumerator GoogleSignInRoutine(string idToken, string nonce, Action<bool, string> completed)
        {
            if (string.IsNullOrWhiteSpace(idToken) || string.IsNullOrWhiteSpace(nonce))
            {
                googleSignInRoutine = null;
                completed?.Invoke(false, "Google 登入憑證不完整");
                yield break;
            }

            var payload = new GoogleSignInRequest
            {
                idToken = idToken,
                nonce = nonce,
                installationUid = InstallationUid,
                displayName = ResolveDisplayName()
            };
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using (var request = new UnityWebRequest(ApiBaseUrl + "/api/v2/auth/google", UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 15;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    PlayerSessionResponse response = null;
                    try { response = JsonUtility.FromJson<PlayerSessionResponse>(request.downloadHandler.text); }
                    catch (Exception exception) { Debug.LogWarning("Google 登入回應格式無法解析：" + exception.Message); }

                    if (IsCompleteSession(response))
                    {
                        if (signInRoutine != null)
                        {
                            StopCoroutine(signInRoutine);
                            signInRoutine = null;
                        }
                        ApplySession(response, "google", true);
                        googleSignInRoutine = null;
                        completed?.Invoke(true, string.IsNullOrWhiteSpace(response.displayName)
                            ? "Google 登入成功"
                            : response.displayName + "，歡迎回來");
                        yield break;
                    }
                }

                string message = request.responseCode == 401
                    ? "Google 身分驗證失敗，請重新登入"
                    : "暫時無法連接登入服務";
                Debug.LogWarning($"Google 登入未完成：{request.responseCode} {request.error}");
                googleSignInRoutine = null;
                completed?.Invoke(false, message);
            }
        }

        IEnumerator HeartbeatLoop()
        {
            while (isActiveAndEnabled && IsReady)
            {
                using (var request = new UnityWebRequest(ApiBaseUrl + "/api/v1/presence/heartbeat", UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerRaw(Array.Empty<byte>());
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Authorization", "Bearer " + AccessToken);
                    request.timeout = 10;
                    yield return request.SendWebRequest();
                    if (request.responseCode == 401)
                    {
                        string expiredProvider = AuthProvider;
                        IsReady = false;
                        AccessToken = string.Empty;
                        DeleteCachedSession();
                        heartbeatRoutine = null;
                        IdentityChanged?.Invoke();
                        if (expiredProvider == "guest")
                        {
                            if (signInRoutine != null) StopCoroutine(signInRoutine);
                            signInRoutine = StartCoroutine(SignInWithRetry());
                        }
                        else
                        {
                            Debug.LogWarning("Google 登入已過期，請在登入頁重新登入。");
                        }
                        yield break;
                    }
                }
                yield return new WaitForSecondsRealtime(HeartbeatIntervalSeconds);
            }
            heartbeatRoutine = null;
        }

        IEnumerator SyncProfileRoutine()
        {
            var payload = new ProfileUpdateRequest
            {
                displayName = ResolveDisplayName(),
                birthDate = PlayerPrefs.GetString("LTC_ProfileBirthDate", string.Empty),
                sexCode = PlayerPrefs.GetString("LTC_ProfileGender", string.Empty),
                educationYears = EducationYears(PlayerPrefs.GetString("LTC_ProfileEducation", string.Empty))
            };
            if (string.IsNullOrWhiteSpace(payload.birthDate) || string.IsNullOrWhiteSpace(payload.sexCode))
            {
                profileSyncRoutine = null;
                yield break;
            }

            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using (var request = new UnityWebRequest(ApiBaseUrl + "/api/v1/profile", UnityWebRequest.kHttpVerbPUT))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + AccessToken);
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    Debug.LogWarning("玩家基本資料尚未同步至後端：" + request.error);
            }
            profileSyncRoutine = null;
        }

        static bool HasCompletedProfile()
        {
            return !string.IsNullOrWhiteSpace(PlayerPrefs.GetString("LTC_ProfileBirthDate", string.Empty)) &&
                   !string.IsNullOrWhiteSpace(PlayerPrefs.GetString("LTC_ProfileGender", string.Empty));
        }

        static int EducationYears(string code)
        {
            switch (code)
            {
                case "primary": return 6;
                case "junior_high": return 9;
                case "senior_high": return 12;
                case "college": return 16;
                case "graduate": return 18;
                default: return 0;
            }
        }

        void ApplySession(PlayerSessionResponse response, string provider, bool persist)
        {
            PlayerId = response.playerId;
            PlayerCode = response.playerCode;
            AccessToken = response.accessToken;
            AuthProvider = provider;
            IsReady = true;
            connectionWarningLogged = false;
            if (!string.IsNullOrWhiteSpace(response.displayName))
                PlayerPrefs.SetString("SavedPlayerName", response.displayName.Trim());
            if (persist) SaveCachedSession(response, provider);
            if (heartbeatRoutine != null) StopCoroutine(heartbeatRoutine);
            heartbeatRoutine = StartCoroutine(HeartbeatLoop());
            if (HasCompletedProfile()) SyncCurrentProfile();
            IdentityChanged?.Invoke();
        }

        bool TryRestoreCachedSession()
        {
            try
            {
                if (!File.Exists(SessionFilePath)) return false;
                var cached = JsonUtility.FromJson<CachedSession>(File.ReadAllText(SessionFilePath));
                if (cached == null || cached.playerId <= 0 ||
                    string.IsNullOrWhiteSpace(cached.playerCode) ||
                    string.IsNullOrWhiteSpace(cached.accessToken) ||
                    !DateTime.TryParse(cached.expiresAtUtc, null,
                        System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime expiresAt) ||
                    expiresAt <= DateTime.UtcNow.AddMinutes(1))
                {
                    DeleteCachedSession();
                    return false;
                }

                ApplySession(new PlayerSessionResponse
                {
                    playerId = cached.playerId,
                    playerCode = cached.playerCode,
                    displayName = cached.displayName,
                    accessToken = cached.accessToken,
                    expiresAtUtc = cached.expiresAtUtc,
                    isNewPlayer = false
                }, cached.authProvider == "google" ? "google" : "guest", false);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("本機登入狀態無法讀取，將重新登入：" + exception.Message);
                DeleteCachedSession();
                return false;
            }
        }

        void SaveCachedSession(PlayerSessionResponse response, string provider)
        {
            try
            {
                Directory.CreateDirectory(IdentityDirectory);
                var cached = new CachedSession
                {
                    playerId = response.playerId,
                    playerCode = response.playerCode,
                    displayName = response.displayName,
                    accessToken = response.accessToken,
                    expiresAtUtc = response.expiresAtUtc,
                    authProvider = provider
                };
                string temporaryPath = SessionFilePath + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(cached), Encoding.UTF8);
                if (File.Exists(SessionFilePath)) File.Delete(SessionFilePath);
                File.Move(temporaryPath, SessionFilePath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("登入成功，但無法保存本機登入狀態：" + exception.Message);
            }
        }

        void DeleteCachedSession()
        {
            try { if (File.Exists(SessionFilePath)) File.Delete(SessionFilePath); }
            catch (Exception exception) { Debug.LogWarning("無法清除過期登入狀態：" + exception.Message); }
        }

        static bool IsCompleteSession(PlayerSessionResponse response)
        {
            return response != null && response.playerId > 0 &&
                   !string.IsNullOrWhiteSpace(response.playerCode) &&
                   !string.IsNullOrWhiteSpace(response.accessToken) &&
                   !string.IsNullOrWhiteSpace(response.expiresAtUtc);
        }



        string LoadOrCreateInstallationUid()
        {
            Directory.CreateDirectory(IdentityDirectory);
            if (File.Exists(InstallationFilePath))
            {
                string existing = File.ReadAllText(InstallationFilePath).Trim();
                if (IsValidInstallationUid(existing)) return existing;
            }

            string created = CreateRandomInstallationUid();
            string temporaryPath = InstallationFilePath + ".tmp";
            File.WriteAllText(temporaryPath, created, Encoding.UTF8);
            if (File.Exists(InstallationFilePath)) File.Delete(InstallationFilePath);
            File.Move(temporaryPath, InstallationFilePath);
            return created;
        }

        static string CreateRandomInstallationUid()
        {
            byte[] random = new byte[24];
            using (var generator = RandomNumberGenerator.Create()) generator.GetBytes(random);
            return Convert.ToBase64String(random).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        static bool IsValidInstallationUid(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 16 || value.Length > 64) return false;
            foreach (char character in value)
                if (!(char.IsLetterOrDigit(character) || character == '-' || character == '_')) return false;
            return true;
        }

        static string ResolveDisplayName()
        {
            string value = PlayerPrefs.GetString("SavedPlayerName", PlayerPrefs.GetString("AccountName", string.Empty));
            value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            return value != null && value.Length > 40 ? value.Substring(0, 40) : value;
        }

        [Serializable]
        sealed class GuestSignInRequest
        {
            public string installationUid;
            public string displayName;
        }

        [Serializable]
        sealed class GoogleSignInRequest
        {
            public string idToken;
            public string nonce;
            public string installationUid;
            public string displayName;
        }

        [Serializable]
        sealed class ProfileUpdateRequest
        {
            public string displayName;
            public string birthDate;
            public string sexCode;
            public int educationYears;
        }

        [Serializable]
        sealed class PlayerSessionResponse
        {
            public long playerId;
            public string playerCode;
            public string displayName;
            public string accessToken;
            public string expiresAtUtc;
            public bool isNewPlayer;
        }

        [Serializable]
        sealed class CachedSession
        {
            public long playerId;
            public string playerCode;
            public string displayName;
            public string accessToken;
            public string expiresAtUtc;
            public string authProvider;
        }
    }
}
