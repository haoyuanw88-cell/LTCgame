using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "LTC/Challenge Rush Assets")]
public sealed class LTCChallengeAssets : ScriptableObject
{
    public TMP_FontAsset font;
    public Sprite background;
    public Sprite panel;
    public Sprite player;
    public Sprite[] playerAttack;
    public Sprite[] playerHit;
    public Sprite cardBack;
    public Sprite[] cardFaces;
    public Sprite[] products;
    public Sprite mole;
    public Sprite bomb;
    public AudioClip music;
    public CardLevelCatalog enemies;
}
