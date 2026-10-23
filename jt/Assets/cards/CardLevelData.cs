using UnityEngine;
[CreateAssetMenu(menuName="LTC/Cards/Level")]
public sealed class CardLevelData : ScriptableObject {
 public string displayName="新關卡";
 [Range(2,40)] public int cardCount=12;
 [Range(2,8)] public int columns=4;
 [Min(.5f)] public float previewSeconds=5f;
 [Min(1)] public int playerHealth=20;
 public string enemyName="敵人";
 public Sprite enemySprite;
 [Min(1)] public int enemyHealth=20;
 [Min(0)] public int enemyAttack=3;
 [Range(1f,5f)] public float enemyHeight=3f;
 public bool enemyFacesLeft;
 public void Validate(){cardCount=Mathf.Clamp(cardCount,2,40); if(cardCount%2!=0)cardCount++; columns=Mathf.Clamp(columns,2,Mathf.Min(8,cardCount)); previewSeconds=Mathf.Max(.5f,previewSeconds); playerHealth=Mathf.Max(1,playerHealth); enemyHealth=Mathf.Max(1,enemyHealth); enemyAttack=Mathf.Max(0,enemyAttack);}
 void OnValidate(){Validate();}
}
