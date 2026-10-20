using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LTC.DesktopAssistant
{
    /// <summary>
    /// AI 助理與既有寵物收藏系統之間的唯讀適配器。
    /// 此類別不會授予、刪除或購買任何寵物。
    /// </summary>
    public sealed class PetCatalogAdapter : MonoBehaviour
    {
        readonly List<LTCPetDefinition> availablePets = new List<LTCPetDefinition>();

        public IReadOnlyList<LTCPetDefinition> GetAvailablePets(string fallbackPetId)
        {
            availablePets.Clear();
            availablePets.AddRange(LTCPetCollectionService.GetOwnedPets());

            if (availablePets.Count == 0)
            {
                LTCPetDefinition fallback = LTCPetCollectionService.AllPets
                    .FirstOrDefault(pet => pet.id == fallbackPetId)
                    ?? LTCPetCollectionService.AllPets.FirstOrDefault();
                if (fallback != null) availablePets.Add(fallback);
            }

            return availablePets;
        }

        public Sprite GetIdleSprite(LTCPetDefinition pet)
        {
            return pet == null ? null : LTCPetCollectionService.GetPreviewSprite(pet);
        }

        public Sprite[] GetAnimationFrames(LTCPetDefinition pet)
        {
            return pet == null ? new Sprite[0] : LTCPetCollectionService.GetFrames(pet);
        }
    }
}
