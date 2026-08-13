using UnityEngine;
using UnityEngine.UI;

public class Mole : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite s1; // 洞
    public Sprite s2; // 一般地鼠
    public Sprite s3; // 一般地鼠被打
    public Sprite bombSprite; // 炸彈地鼠
    public Sprite explosionSprite; // 爆炸圖片

    [Header("Settings")]
    public Transform hp;
    public float range = 100f;
    public bool isUp = false;

    [HideInInspector] public MoleManager manager;

    private bool isHit = false;
    private bool isBomb = false;
    private Image img;
    private float timer = 0f;

    public bool IsBomb
    {
        get { return isBomb; }
    }

    void Awake()
    {
        img = GetComponent<Image>();
    }

    void Start()
    {
        Hide();
    }

    void Update()
    {
        if (isUp && !isHit)
        {
            timer -= Time.deltaTime;

            if (timer <= 0f)
            {
                if (manager != null)
                {
                    manager.OnMoleMissed(this);
                }

                Hide();
            }
        }
    }

    public void Pop()
    {
        Pop(3f, false);
    }

    public void Pop(float upTime)
    {
        Pop(upTime, false);
    }

    public void Pop(float upTime, bool bomb)
    {
        isUp = true;
        isHit = false;
        isBomb = bomb;
        timer = upTime;

        if (img != null)
        {
            img.sprite = isBomb ? bombSprite : s2;
        }
    }

    public void Hide()
    {
        isUp = false;
        isHit = false;
        isBomb = false;
        CancelInvoke(nameof(Hide));

        if (img != null)
        {
            img.sprite = s1;
        }
    }

    public void OnHit()
    {
        if (isHit) return;

        isHit = true;

        if (img != null)
        {
            img.sprite = isBomb ? explosionSprite : s3;
        }

        CancelInvoke(nameof(Hide));
        Invoke(nameof(Hide), 0.5f);
    }

    public void Check(Vector2 p, bool g)
    {
        if (!isUp || isHit || !g) return;

        Vector2 target = hp != null
            ? (Vector2)Camera.main.WorldToScreenPoint(hp.position)
            : (Vector2)Camera.main.WorldToScreenPoint(transform.position);

        if (Vector2.Distance(p, target) < range)
        {
            OnHit();
        }
    }
}