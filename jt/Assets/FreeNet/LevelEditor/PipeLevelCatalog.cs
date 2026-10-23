using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FreeNet/Pipe Level Catalog", fileName = "PipeLevelCatalog")]
public sealed class PipeLevelCatalog : ScriptableObject
{
    public const string ResourceName = "PipeLevelCatalog";
    public List<PipeLevelData> levels = new List<PipeLevelData>();
}
