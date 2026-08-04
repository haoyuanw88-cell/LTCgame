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
        public string StableLocalPlayerKey => "guest-" + InstallationUid;
        public event Action IdentityChanged;

        string ApiBaseUrl => PlayerPrefs.GetString(ApiBaseUrlPlayerPrefsKey, DefaultApiBaseUrl).Trim().TrimEnd('/');
        string IdentityDirectory => Path.Combine(Application.persistentDataPath, "Identity");
        string InstallationFilePath => Path.Combine(IdentityDirectory, "installation.id");

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
            Refresh();
        }

        void OnDisable()
        {
            StopAllCoroutines();
            signInRoutine = null;
            heartbeatRoutine = null;
            profileSyncRoutine = null;
        }

        void OnDestroy()
        {
            StopAllCoroutines();
            heartbeatRoutine = null;
            profileSyncRoutine = null;
            if (instance == this) instance = null;
        }


        public void Refresh()
        {
            if (signInRoutine != null) StopCoroutine(signInRoutine);
            if (heartbeatRoutine != null) StopCoroutine(heartbeatRoutine);
            IsReady = false;
            AccessToken = string.Empty;
            heartbeatRoutine = null;
            signInRoutine = StartCoroutine(SignInWithRetry());
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
                            PlayerId = response.playerId;
                            PlayerCode = response.playerCode;
                            AccessToken = response.accessToken;
                            IsReady = true;
                            connectionWarningLogged = false;
                            if (heartbeatRoutine != null) StopCoroutine(heartbeatRoutine);
                            heartbeatRoutine = StartCoroutine(HeartbeatLoop());
                            if (HasCompletedProfile()) SyncCurrentProfile();
                            IdentityChanged?.Invoke();
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
                        IsReady = false;
                        AccessToken = string.Empty;
                        heartbeatRoutine = null;
                        if (signInRoutine != null) StopCoroutine(signInRoutine);
                        signInRoutine = StartCoroutine(SignInWithRetry());
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
    }
}
