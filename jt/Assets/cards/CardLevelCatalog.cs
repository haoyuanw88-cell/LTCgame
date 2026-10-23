using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="LTC/Cards/Catalog")]
public sealed class CardLevelCatalog : ScriptableObject {
 public const string ResourceName="CardLevelCatalog";
 public List<CardLevelData> levels=new List<CardLevelData>();
}
