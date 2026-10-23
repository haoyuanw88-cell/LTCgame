using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SupermarketLevelEditorWindow : EditorWindow
{
    [SerializeField] private SupermarketLevelData level;
    private SerializedObject serializedLevel;
    private Vector2 scroll;
    private List<Sprite> available;
    private string search = "";
    private string message = "";
    [MenuItem("Tools/LTC/Supermarket Level Editor")]
    public static void OpenWindow() { GetWindow<SupermarketLevelEditorWindow>("超市關卡編輯器"); }

    public static List<Sprite> SceneProducts()
    {
        const string path = "Assets/supermarket/Scenes/SupermarketGame.unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var game = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<SupermarketGame>(true)).First();
            var tutorial = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).FirstOrDefault(x => x.name == game.tutorialPanelName);
            return scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<SpriteRenderer>(true))
                .Where(x => x.gameObject.activeInHierarchy && game.IsPurchasable(x) && (tutorial == null || !x.transform.IsChildOf(tutorial)))
                .Select(x => x.sprite).Distinct().OrderBy(x => x.name).ToList();
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("超市關卡編輯器", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("設定商品與數量，儲存並加入選關後，下一次開啟選關畫面即可遊玩。商品可重複點擊購買指定數量。", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();
            var chosen = (SupermarketLevelData)EditorGUILayout.ObjectField("關卡", level, typeof(SupermarketLevelData), false);
            if (chosen != level) { level = chosen; serializedLevel = null; message = ""; }
            if (GUILayout.Button("新建", GUILayout.Width(60))) CreateLevel();
            EditorGUILayout.EndHorizontal();
            if (level == null) return;
            if (serializedLevel == null || serializedLevel.targetObject != level) serializedLevel = new SerializedObject(level);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            serializedLevel.Update();
            EditorGUILayout.PropertyField(serializedLevel.FindProperty("displayName"), new GUIContent("關卡名稱"));
            EditorGUILayout.PropertyField(serializedLevel.FindProperty("panelHideDelay"), new GUIContent("清單顯示秒數"));
            EditorGUILayout.PropertyField(serializedLevel.FindProperty("products"), new GUIContent("必買商品與數量"), true);
            serializedLevel.ApplyModifiedProperties();
            EditorGUILayout.LabelField("商品預覽（點擊加入）", EditorStyles.boldLabel);
            search = EditorGUILayout.TextField("搜尋商品", search);
            if (available == null || GUILayout.Button("重新讀取超市場景商品")) available = SceneProducts();
            var filtered = available.Where(x => x != null && x.name.ToLowerInvariant().Contains(search.ToLowerInvariant())).ToList();
            int columns = Mathf.Max(1, (int)(position.width - 35) / 110);
            for (int i = 0; i < filtered.Count; i += columns)
            {
                EditorGUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + columns, filtered.Count); j++)
                {
                    var sprite = filtered[j];
                    EditorGUILayout.BeginVertical(GUILayout.Width(104));
                    var rect = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(100));
                    var uv = sprite.textureRect;
                    uv = new Rect(uv.x / sprite.texture.width, uv.y / sprite.texture.height, uv.width / sprite.texture.width, uv.height / sprite.texture.height);
                    GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv, true);
                    if (GUILayout.Button(sprite.name, GUILayout.Width(104), GUILayout.Height(36)))
                    {
                        Undo.RecordObject(level, "Add Shopping Product");
                        var existing = level.products.Find(x => x != null && x.productImage == sprite);
                        if (existing != null) existing.quantity++;
                        else level.products.Add(new ShoppingRequirement { productImage = sprite, quantity = 1 });
                        EditorUtility.SetDirty(level);
                    }
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("共 " + level.products.Count + " 種商品 / " + level.TotalQuantity + " 件");
            if (GUILayout.Button("儲存並加入選關", GUILayout.Height(38)))
            {
                if (level.ValidateProducts(SceneProducts(), out message))
                {
                    level.panelHideDelay = Mathf.Max(.5f, level.panelHideDelay);
                    EditorUtility.SetDirty(level);
                    SupermarketLevelSelectionBaker.AddLevel(level);
                    AssetDatabase.SaveAssets();
                    message = "已儲存並加入選關。";
                }
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            if (GUILayout.Button("選取關卡目錄（可調整順序或移除關卡）"))
                Selection.activeObject = SupermarketLevelSelectionBaker.GetOrCreateCatalog();
            EditorGUILayout.EndScrollView();
        }
    }

    private void CreateLevel()
    {
        string path = EditorUtility.SaveFilePanelInProject("新增超市關卡", "SupermarketLevel", "asset", "選擇關卡儲存位置", "Assets/supermarket/Levels");
        if (string.IsNullOrEmpty(path)) return;
        level = CreateInstance<SupermarketLevelData>();
        AssetDatabase.CreateAsset(level, path);
        AssetDatabase.SaveAssets();
        serializedLevel = null; message = "";
        Selection.activeObject = level;
    }
}
