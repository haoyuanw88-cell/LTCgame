using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FreeNet/Pipe Palette", fileName = "PipePalette")]
public class PipePalette : ScriptableObject
{
    public List<PipeBrush> brushes = new List<PipeBrush>();

    private void OnValidate()
    {
        for (int i = 0; i < brushes.Count; i++)
        {
            if (brushes[i] != null)
            {
                brushes[i].Validate();
            }
        }
    }
}
