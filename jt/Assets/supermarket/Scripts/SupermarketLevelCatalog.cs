using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName = "LTC/Supermarket Level Catalog", fileName = "SupermarketLevelCatalog")]
public sealed class SupermarketLevelCatalog : ScriptableObject
{
    public const string ResourceName = "SupermarketLevelCatalog";
    public List<SupermarketLevelData> levels = new List<SupermarketLevelData>();
}