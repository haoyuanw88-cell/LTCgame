using System;
using System.IO;
using SQLite4Unity3d;
using UnityEngine;

[Table("play_records")]
public class PlaySessionRecord
{
    [PrimaryKey, AutoIncrement, Column("id")]
    public int Id { get; set; }

    [Column("play_date")]
    public string PlayDate { get; set; }

    [Column("play_count")]
    public int PlayCount { get; set; }

    [Column("play_time_seconds")]
    public double PlayTimeSeconds { get; set; }

    [Column("failure_count")]
    public int FailureCount { get; set; }
}

public static class PlayHistoryDatabase
{
    private const string DatabaseName = "SupermarketGame.db";

    public static string DatabasePath => Path.Combine(Application.persistentDataPath, DatabaseName);

    public static void EnsureCreated()
    {
        try
        {
            using (SQLiteConnection connection = new SQLiteConnection(DatabasePath))
            {
                connection.CreateTable<PlaySessionRecord>();
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to create play history database: {exception.Message}");
        }
    }

    public static void SavePlaySession(float playTimeSeconds, int failureCount)
    {
        try
        {
            DateTime now = DateTime.Now;
            using (SQLiteConnection connection = new SQLiteConnection(DatabasePath))
            {
                connection.CreateTable<PlaySessionRecord>();
                int playCount = connection.Table<PlaySessionRecord>().Count() + 1;
                connection.Insert(new PlaySessionRecord
                {
                    PlayDate = now.ToString("yyyy-MM-dd"),
                    PlayCount = playCount,
                    PlayTimeSeconds = Math.Round(playTimeSeconds, 2),
                    FailureCount = failureCount
                });
            }

            Debug.Log($"Play history saved to {DatabasePath}");
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to save play history: {exception.Message}");
        }
    }
}
