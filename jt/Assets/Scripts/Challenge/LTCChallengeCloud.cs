using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LTC.Identity;
using UnityEngine;
using UnityEngine.Networking;

public sealed class LTCChallengeCloud : MonoBehaviour
{
    const string LegacyRunId = "00000000-0000-0000-0000-000000000000";
    const string Prefix = "LTC_ChallengeCloud_v1_";

    [Serializable]
    sealed class Result
    {
        public string runId;
        public int score;
        public string finishedAt;
    }

    [Serializable]
    sealed class Pending
    {
        public List<Result> runs = new List<Result>();
    }

    public static string ApiBaseUrl => PlayerPrefs.GetString(PlayerIdentityService.ApiBaseUrlPlayerPrefsKey,
        "https://staging-hello-8shi.encr.app").Trim().TrimEnd('/');

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        var host = new GameObject("Challenge Cloud Sync");
        DontDestroyOnLoad(host);
        host.AddComponent<LTCChallengeCloud>();
    }

    public static string CurrentOwner()
    {
        var identity = PlayerIdentityService.Current;
        if (identity.IsReady) return identity.PlayerId;
        var service = identity as PlayerIdentityService;
        if (service != null && !string.IsNullOrEmpty(service.ExpectedGooglePlayer))
            return service.ExpectedGooglePlayer;
        return identity.StableLocalPlayerKey;
    }

    public static void CaptureLegacyBest(string localKey)
    {
        string key = Prefix + "legacy_" + localKey;
        if (PlayerPrefs.HasKey(key)) return;
        PlayerPrefs.SetInt(key, LTCChallengeScores.Best(localKey));
        PlayerPrefs.Save();
    }

    public static void Enqueue(string baseUrl, string owner, int score)
    {
        string key = QueueKey(baseUrl, owner);
        var pending = Load(key);
        pending.runs.Add(new Result
        {
            runId = Guid.NewGuid().ToString(),
            score = score,
            finishedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        });
        Save(key, pending);
    }

    IEnumerator Start()
    {
        while (true)
        {
            var identity = PlayerIdentityService.Current;
            if (identity.IsReady && !string.IsNullOrEmpty(identity.AccessToken))
            {
                string baseUrl = ApiBaseUrl;
                string owner = identity.PlayerId;
                string localKey = identity.StableLocalPlayerKey;
                string token = identity.AccessToken;
                CaptureLegacyBest(localKey);
                int legacyScore = PlayerPrefs.GetInt(Prefix + "legacy_" + localKey);
                string syncedKey = QueueKey(baseUrl, owner) + "_legacySynced";
                if (legacyScore > PlayerPrefs.GetInt(syncedKey))
                {
                    var result = new Result { runId = LegacyRunId, score = legacyScore, finishedAt = "" };
                    yield return Upload(baseUrl, token, result, () =>
                    {
                        PlayerPrefs.SetInt(syncedKey, legacyScore);
                        PlayerPrefs.Save();
                    });
                }

                yield return Flush(baseUrl, owner, token, QueueKey(baseUrl, owner));
                if (identity is PlayerIdentityService service && service.AuthProvider == "guest" && localKey != owner)
                    yield return Flush(baseUrl, owner, token, QueueKey(baseUrl, localKey));
            }
            yield return new WaitForSecondsRealtime(5);
        }
    }

    static IEnumerator Flush(string baseUrl, string owner, string token, string key)
    {
        var identity = PlayerIdentityService.Current;
        while (identity.IsReady && identity.PlayerId == owner && identity.AccessToken == token && ApiBaseUrl == baseUrl)
        {
            var pending = Load(key);
            if (pending.runs.Count == 0) yield break;
            var result = pending.runs[0];
            bool uploaded = false;
            yield return Upload(baseUrl, token, result, () => uploaded = true);
            if (!uploaded) yield break;

            // A later run may have finished while this request was in flight.
            pending = Load(key);
            pending.runs.RemoveAll(run => run.runId == result.runId);
            Save(key, pending);
        }
    }

    static IEnumerator Upload(string baseUrl, string token, Result result, Action onSuccess)
    {
        using (var request = new UnityWebRequest(baseUrl + "/api/v1/challenge-results", UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(result)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.timeout = 10;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success) onSuccess();
        }
    }

    static string QueueKey(string baseUrl, string owner) => Prefix + baseUrl + "_" + owner;

    static Pending Load(string key) => PlayerPrefs.HasKey(key)
        ? JsonUtility.FromJson<Pending>(PlayerPrefs.GetString(key))
        : new Pending();

    static void Save(string key, Pending pending)
    {
        if (pending.runs.Count == 0) PlayerPrefs.DeleteKey(key);
        else PlayerPrefs.SetString(key, JsonUtility.ToJson(pending));
        PlayerPrefs.Save();
    }
}