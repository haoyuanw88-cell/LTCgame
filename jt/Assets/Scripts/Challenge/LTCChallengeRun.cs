using System;

/// <summary>Session-only RUSH state. Deliberately has no persistence or assessment dependencies.</summary>
public sealed class LTCChallengeRun
{
    public int Health { get; private set; } = 3;
    public int EnemyHealth { get; private set; }
    public int EnemyMaxHealth { get; private set; }
    public int Score { get; private set; }
    public int Cleared { get; private set; }
    public int Defeated { get; private set; }
    public float Multiplier { get; private set; } = 1f;
    public float BestMultiplier { get; private set; } = 1f;
    public bool IsOver => Health == 0;
    public bool NeedsEnemy => EnemyHealth == 0;
    public int LastAward { get; private set; }
    readonly Random random;

    public LTCChallengeRun(int seed) { random = new Random(seed); SpawnEnemy(); }
    public void SpawnEnemy()
    {
        if (IsOver || !NeedsEnemy) return;
        EnemyHealth = EnemyMaxHealth = random.Next(2, 9);
    }
    public bool Resolve(bool success)
    {
        if (IsOver || NeedsEnemy) return false;
        LastAward = 0;
        if (success)
        {
            EnemyHealth--;
            Cleared++;
            bool killed = EnemyHealth == 0;
            if (killed) Defeated++;
            LastAward = (int)((50 + (killed ? 100 : 0)) * Multiplier);
            Score += LastAward;
            Multiplier = Math.Min(5f, Multiplier + .5f);
            BestMultiplier = Math.Max(BestMultiplier, Multiplier);
        }
        else { Health--; Multiplier = 1f; }
        return true;
    }
}

public enum LTCChallengeKind { Cards, Stroop, NumberOrder, NumberSum, Pipes, Shopping, Quiz, Gopher }
