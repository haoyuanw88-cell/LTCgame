using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LTC.Identity;
using UnityEngine;

namespace LTCCognitiveAssessment
{
    [Serializable]
    public sealed class LTCProgressionEntry
    {
        public string id;
        public string title;
        public string description;
        public string gameId;
        public float progress;
        public float target;
        public int rewardCoins;
        public bool completed;
        public bool rewardGranted;
        public long completedAtUnixMs;

        public string ProgressLabel
        {
            get
            {
                if (target <= 1f) return completed ? "已完成" : "未完成";
                return Mathf.Min(progress, target).ToString("0") + " / " + target.ToString("0");
            }
        }
    }

    [Serializable]
    public sealed class LTCGameProgress
    {
        public string gameId;
        public int completedCount;
        public float bestScore;
        public float bestAccuracy;
    }

    [Serializable]
    sealed class LTCProgressionSaveData
    {
        public int schemaVersion = 1;
        public string playerKey;
        public string dayKey;
        public List<LTCGameProgress> games = new List<LTCGameProgress>();
        public List<LTCProgressionEntry> achievements = new List<LTCProgressionEntry>();
        public List<LTCProgressionEntry> dailyQuests = new List<LTCProgressionEntry>();
        public List<string> dailyUniqueGames = new List<string>();
        public int dailyCompletedGames;
    }

    /// <summary>
    /// Persistent, per-player achievement and daily quest service.
    /// Game completion is recorded by CognitiveAssessmentService, so every registered game
    /// contributes without requiring individual game-manager integrations.
    /// </summary>
    public static class LTCProgressionService
    {
        sealed class GameInfo
        {
            public readonly string Id;
            public readonly string Name;
            public GameInfo(string id, string name) { Id = id; Name = name; }
        }

        static readonly GameInfo[] Games =
        {
            new GameInfo("stroop_color_match", "顏色文字判斷"),
            new GameInfo("number_order", "數字由小到大"),
            new GameInfo("number_sum", "數字組合加總"),
            new GameInfo("gopher_reaction", "手勢打地鼠"),
            new GameInfo("card_memory_battle", "翻牌記憶"),
            new GameInfo("pipe_connection", "旋轉接水管"),
            new GameInfo("supermarket_shopping", "超市採購"),
            new GameInfo("true_false_life_quiz", "文字判斷")
        };

        static LTCProgressionSaveData data;
        static string loadedPlayerKey;
        public static event Action Changed;

        public static IReadOnlyList<LTCProgressionEntry> Achievements
        {
            get { EnsureLoaded(); EvaluateAchievements(false); return data.achievements; }
        }

        public static IReadOnlyList<LTCProgressionEntry> DailyQuests
        {
            get { EnsureLoaded(); EnsureDailyQuests(); return data.dailyQuests; }
        }

        public static int AchievementCompletedCount
        {
            get { EnsureLoaded(); EvaluateAchievements(false); return data.achievements.Count(x => x.completed); }
        }

        public static int AchievementTotalCount => Games.Length * 3;

        public static void RecordCompletedGame(CognitiveAssessmentSession session)
        {
            if (session == null || !session.completed || session.result == null || string.IsNullOrEmpty(session.gameId))
                return;

            EnsureLoaded();
            EnsureDailyQuests();

            LTCGameProgress game = GetOrCreateGameProgress(session.gameId);
            game.completedCount++;
            game.bestScore = Mathf.Max(game.bestScore, session.result.performanceScore);
            game.bestAccuracy = Mathf.Max(game.bestAccuracy, session.result.accuracy);

            data.dailyCompletedGames++;
            if (!data.dailyUniqueGames.Contains(session.gameId))
                data.dailyUniqueGames.Add(session.gameId);

            int reward = EvaluateAchievements(true);
            reward += EvaluateDailyQuests(session.gameId, true);
            Save();
            if (reward > 0) CoinData.AddCoins(reward);
            Changed?.Invoke();
        }

        public static string GameDisplayName(string gameId)
        {
            GameInfo game = Games.FirstOrDefault(item => item.Id == gameId);
            return game == null ? gameId : game.Name;
        }

        public static void RefreshForToday()
        {
            EnsureLoaded();
            bool changed = EnsureDailyQuests();
            if (changed)
            {
                Save();
                Changed?.Invoke();
            }
        }

        static void EnsureLoaded()
        {
            string playerKey = ResolvePlayerKey();
            if (data != null && loadedPlayerKey == playerKey) return;

            loadedPlayerKey = playerKey;
            string path = SavePath(playerKey);
            try
            {
                data = File.Exists(path)
                    ? JsonUtility.FromJson<LTCProgressionSaveData>(File.ReadAllText(path))
                    : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("成就進度讀取失敗，將建立新資料：" + exception.Message);
                data = null;
            }

            if (data == null) data = new LTCProgressionSaveData();
            data.playerKey = playerKey;
            if (data.games == null) data.games = new List<LTCGameProgress>();
            if (data.achievements == null) data.achievements = new List<LTCProgressionEntry>();
            if (data.dailyQuests == null) data.dailyQuests = new List<LTCProgressionEntry>();
            if (data.dailyUniqueGames == null) data.dailyUniqueGames = new List<string>();
            EnsureAchievementDefinitions();
            bool dailyChanged = EnsureDailyQuests();
            if (dailyChanged || !File.Exists(path)) Save();
        }

        static void EnsureAchievementDefinitions()
        {
            foreach (GameInfo game in Games)
            {
                EnsureAchievement(game.Id + "_first", game.Name + "・初次完成",
                    "完成 1 次「" + game.Name + "」", game.Id, 1f, 10);
                EnsureAchievement(game.Id + "_veteran", game.Name + "・熟能生巧",
                    "累計完成 5 次「" + game.Name + "」", game.Id, 5f, 25);
                EnsureAchievement(game.Id + "_expert", game.Name + "・高分挑戰",
                    "在「" + game.Name + "」取得 80 分以上", game.Id, 80f, 20);
            }
        }

        static void EnsureAchievement(string id, string title, string description, string gameId,
            float target, int reward)
        {
            LTCProgressionEntry entry = data.achievements.FirstOrDefault(item => item.id == id);
            if (entry == null)
            {
                entry = new LTCProgressionEntry { id = id };
                data.achievements.Add(entry);
            }
            entry.title = title;
            entry.description = description;
            entry.gameId = gameId;
            entry.target = target;
            entry.rewardCoins = reward;
        }

        static int EvaluateAchievements(bool grantRewards)
        {
            int reward = 0;
            foreach (GameInfo gameInfo in Games)
            {
                LTCGameProgress game = data.games.FirstOrDefault(item => item.gameId == gameInfo.Id);
                int count = game == null ? 0 : game.completedCount;
                float score = game == null ? 0f : game.bestScore;
                reward += UpdateEntry(gameInfo.Id + "_first", count, grantRewards);
                reward += UpdateEntry(gameInfo.Id + "_veteran", count, grantRewards);
                reward += UpdateEntry(gameInfo.Id + "_expert", score, grantRewards);
            }
            return reward;
        }

        static bool EnsureDailyQuests()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (data.dayKey == today && data.dailyQuests.Count == 3) return false;

            data.dayKey = today;
            data.dailyCompletedGames = 0;
            data.dailyUniqueGames.Clear();
            data.dailyQuests.Clear();

            GameInfo focus = Games[DateTime.Now.DayOfYear % Games.Length];
            data.dailyQuests.Add(NewDaily("daily_warmup", "今日暖身", "完成任意遊戲 1 次", string.Empty, 1f, 10));
            data.dailyQuests.Add(NewDaily("daily_variety", "多元訓練", "完成 2 種不同遊戲", string.Empty, 2f, 20));
            data.dailyQuests.Add(NewDaily("daily_focus_" + focus.Id, "今日指定：" + focus.Name,
                "完成 1 次「" + focus.Name + "」", focus.Id, 1f, 15));
            return true;
        }

        static LTCProgressionEntry NewDaily(string id, string title, string description, string gameId,
            float target, int reward)
        {
            return new LTCProgressionEntry
            {
                id = id, title = title, description = description, gameId = gameId,
                target = target, rewardCoins = reward
            };
        }

        static int EvaluateDailyQuests(string completedGameId, bool grantRewards)
        {
            int reward = 0;
            foreach (LTCProgressionEntry quest in data.dailyQuests)
            {
                float progress;
                if (quest.id == "daily_warmup") progress = data.dailyCompletedGames;
                else if (quest.id == "daily_variety") progress = data.dailyUniqueGames.Count;
                else progress = quest.gameId == completedGameId || quest.completed ? 1f : 0f;
                reward += UpdateEntry(quest, progress, grantRewards);
            }
            return reward;
        }

        static int UpdateEntry(string id, float progress, bool grantRewards)
        {
            LTCProgressionEntry entry = data.achievements.FirstOrDefault(item => item.id == id);
            return entry == null ? 0 : UpdateEntry(entry, progress, grantRewards);
        }

        static int UpdateEntry(LTCProgressionEntry entry, float progress, bool grantRewards)
        {
            entry.progress = Mathf.Max(entry.progress, progress);
            if (!entry.completed && entry.progress >= entry.target)
            {
                entry.completed = true;
                entry.completedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
            if (grantRewards && entry.completed && !entry.rewardGranted)
            {
                entry.rewardGranted = true;
                return entry.rewardCoins;
            }
            return 0;
        }

        static LTCGameProgress GetOrCreateGameProgress(string gameId)
        {
            LTCGameProgress progress = data.games.FirstOrDefault(item => item.gameId == gameId);
            if (progress != null) return progress;
            progress = new LTCGameProgress { gameId = gameId };
            data.games.Add(progress);
            return progress;
        }

        static string ResolvePlayerKey()
        {
            if (!Application.isPlaying) return "editor-preview";
            string key = PlayerIdentityService.Current.StableLocalPlayerKey;
            return string.IsNullOrWhiteSpace(key) ? "local-player" : key;
        }

        static string SavePath(string playerKey)
        {
            string directory = Path.Combine(Application.persistentDataPath, "LTCProgression");
            Directory.CreateDirectory(directory);
            string safe = Hash128.Compute(playerKey ?? "local-player").ToString();
            return Path.Combine(directory, "progression-" + safe + ".json");
        }

        static void Save()
        {
            try
            {
                string path = SavePath(loadedPlayerKey);
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporary, path);
            }
            catch (Exception exception)
            {
                Debug.LogError("成就進度儲存失敗：" + exception.Message);
            }
        }
    }
}
