using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class CardLevelEditorWindow : EditorWindow
{
    private CardLevelData level;
    private UnityEditor.Editor inspector;
    private Vector2 scroll;
    [MenuItem("Tools/LTC/Card Level Editor")]
    public static void Open() { GetWindow<CardLevelEditorWindow>("卡牌關卡編輯器"); }

    private void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }
    private void OnGUI()
    {
        EditorGUILayout.LabelField("卡牌關卡編輯器", EditorStyles.boldLabel);
        var selected = (CardLevelData)EditorGUILayout.ObjectField("關卡", level, typeof(CardLevelData), false);
        if (selected != level) { level = selected; if (inspector != null) DestroyImmediate(inspector); inspector = null; }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("新建關卡"))
            {
                string path = EditorUtility.SaveFilePanelInProject("建立卡牌關卡", "CardLevel", "asset", "選擇儲存位置");
                if (!string.IsNullOrEmpty(path))
                {
                    level = CreateInstance<CardLevelData>();
                    AssetDatabase.CreateAsset(level, path);
                    if (inspector != null) DestroyImmediate(inspector);
                    inspector = null;
                }
            }
            if (GUILayout.Button("建立／更新選關介面")) CardLevelSelectionBaker.Bake();
        }
        if (level == null) { EditorGUILayout.HelpBox("新建或選擇關卡後，拖入敵人圖片並設定牌數與數值。", MessageType.Info); return; }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        Rect drop = GUILayoutUtility.GetRect(100, 145, GUILayout.ExpandWidth(true));
        GUI.Box(drop, "把 Sprite 或圖片拖到這裡，作為本關敵人");
        if (level.enemySprite != null)
        {
            var sprite = level.enemySprite;
            Rect preview = new Rect(drop.x + 8, drop.y + 28, drop.width - 16, drop.height - 36);
            Rect uv = new Rect(sprite.rect.x / sprite.texture.width, sprite.rect.y / sprite.texture.height,
                sprite.rect.width / sprite.texture.width, sprite.rect.height / sprite.texture.height);
            float ratio = sprite.rect.width / sprite.rect.height;
            float width = Mathf.Min(preview.width, preview.height * ratio);
            preview = new Rect(preview.center.x - width / 2, preview.y, width, width / ratio);
            GUI.DrawTextureWithTexCoords(preview, sprite.texture, uv);
        }
        Event e = Event.current;
        if (drop.Contains(e.mousePosition) && (e.type == EventType.DragUpdated || e.type == EventType.DragPerform))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                string path = null;
                Sprite sprite = DragAndDrop.objectReferences.OfType<Sprite>().FirstOrDefault();
                if (sprite == null)
                {
                    var texture = DragAndDrop.objectReferences.OfType<Texture2D>().FirstOrDefault();
                    if (texture != null) path = AssetDatabase.GetAssetPath(texture);
                    else if (DragAndDrop.paths.Length > 0)
                    {
                        string source = DragAndDrop.paths[0];
                        string extension = System.IO.Path.GetExtension(source).ToLowerInvariant();
                        if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
                        {
                            if (!AssetDatabase.IsValidFolder("Assets/cards/Enemies")) AssetDatabase.CreateFolder("Assets/cards", "Enemies");
                            path = AssetDatabase.GenerateUniqueAssetPath("Assets/cards/Enemies/" + System.IO.Path.GetFileName(source));
                            if (AssetDatabase.Contains(DragAndDrop.objectReferences.FirstOrDefault()))
                                path = AssetDatabase.GetAssetPath(DragAndDrop.objectReferences[0]);
                            else System.IO.File.Copy(source, path);
                            AssetDatabase.ImportAsset(path);
                        }
                    }
                    if (!string.IsNullOrEmpty(path))
                    {
                        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (importer != null)
                        {
                            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.SaveAndReimport(); }
                            sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
                        }
                    }
                }
                if (sprite != null) { Undo.RecordObject(level, "設定敵人圖片"); level.enemySprite = sprite; level.enemyName = sprite.name; EditorUtility.SetDirty(level); }
                else ShowNotification(new GUIContent("請拖入 Sprite、PNG 或 JPG 圖片"));
            }
            e.Use();
        }
        if (inspector == null) inspector = UnityEditor.Editor.CreateEditor(level);
        inspector.OnInspectorGUI();
        EditorGUILayout.HelpBox("牌數為 2–40 的偶數，奇數會自動補成偶數。敵人攻擊與受擊使用抖動；死亡後倒下 90 度並消失。", MessageType.Info);
        if (GUILayout.Button("儲存並加入選關"))
        {
            level.Validate();
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            CardLevelSelectionBaker.AddLevel(level);
            ShowNotification(new GUIContent("已儲存並加入選關"));
        }
        EditorGUILayout.Space();
        var catalog = CardLevelSelectionBaker.GetOrCreateCatalog();
        EditorGUILayout.LabelField("選關順序（可移動或移除）", EditorStyles.boldLabel);
        for (int i = 0; i < catalog.levels.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var item = catalog.levels[i];
                if (GUILayout.Button((i + 1) + ". " + (item != null ? item.displayName : "遺失關卡"))) { level = item; if (inspector != null) DestroyImmediate(inspector); inspector = null; }
                if (i > 0 && GUILayout.Button("↑", GUILayout.Width(28))) { Undo.RecordObject(catalog, "調整關卡順序"); catalog.levels[i] = catalog.levels[i - 1]; catalog.levels[i - 1] = item; EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); break; }
                if (GUILayout.Button("移除", GUILayout.Width(45))) { Undo.RecordObject(catalog, "移除選關"); catalog.levels.RemoveAt(i); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); break; }
            }
        }
        EditorGUILayout.EndScrollView();
    }
}
