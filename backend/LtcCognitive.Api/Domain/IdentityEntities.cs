namespace LtcCognitive.Api.Domain;

public sealed class Player
{
    public long PlayerId { get; set; }
    public required string PlayerCode { get; set; }
    public required string AuthProvider { get; set; }
    public required string AuthSubjectHash { get; set; }
    public required string DisplayName { get; set; }
    public short? BirthYear { get; set; }
    public string? EducationLevelCode { get; set; }
    public string Status { get; set; } = "active";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastLoginAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<AssessmentSession> Assessments { get; set; } = [];
    public List<CoinTransaction> CoinTransactions { get; set; } = [];
    public List<PlayerInventory> Inventory { get; set; } = [];
}

public sealed class CoinTransaction
{
    public long TransactionId { get; set; }
    public long PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    public int Amount { get; set; }
    public required string TransactionType { get; set; }
    public required string ReferenceKey { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Item
{
    public int ItemId { get; set; }
    public required string ItemCode { get; set; }
    public required string ItemType { get; set; }
    public required string NameZhTw { get; set; }
    public int Price { get; set; }
    public bool IsActive { get; set; } = true;
    public List<PlayerInventory> Owners { get; set; } = [];
}

public sealed class PlayerInventory
{
    public long PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public int Quantity { get; set; }
    public DateTimeOffset AcquiredAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
