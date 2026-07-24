using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PipeLevelEditorWindow : EditorWindow
{
    private enum PaintRole
    {
        BrushDefault,
        Normal,
        Start,
        End
    }

    private const int GridButtonSize = 42;
    private const int PaletteButtonSize = 54;
    private const string DefaultRotationLockSpritePath = "Assets/Grey/chain_shade3.png";
    private const string DefaultRotationLockSpriteName = "chain_shade3_0";
    private const string DefaultBlueEndpointTileSpritePath = "Assets/Grey/tileBlue_01.png";
    private const string DefaultBlueEndpointTileSpriteName = "tileBlue_01_0";
    private const string DefaultRedEndpointTileSpritePath = "Assets/Grey/tileRed_01.png";
    private const string DefaultRedEndpointTileSpriteName = "tileRed_01_0";

    private PipeLevelData level;
    private PipePalette palette;
    private Vector2 scrollPosition;
    private int selectedBrushIndex;
    private int rotationSteps;
    private bool eraserMode;
    private bool replaceExistingPipes = true;
    private bool paintRotationLocked;
    private PipeFlowColor paintPipeColor = PipeFlowColor.Blue;
    private PaintRole paintRole = PaintRole.BrushDefault;

    [MenuItem("Tools/Pipe Level Editor")]
    public static void Open()
    {
        GetWindow<PipeLevelEditorWindow>("Pipe Level Editor");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Pipe Level Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        DrawAssetControls();
        EditorGUILayout.Space(8);

        DrawSceneControls();
        EditorGUILayout.Space(8);

        DrawPalette();
        EditorGUILayout.Space(8);

        DrawLevelGrid();
    }

    private void DrawAssetControls()
    {
        EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            level = (PipeLevelData)EditorGUILayout.ObjectField("Level", level, typeof(PipeLevelData), false);
            if (GUILayout.Button("New", GUILayout.Width(70)))
            {
                CreateLevelAsset();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            palette = (PipePalette)EditorGUILayout.ObjectField("Palette", palette, typeof(PipePalette), false);
            if (GUILayout.Button("New", GUILayout.Width(70)))
            {
                CreatePaletteAsset();
            }
        }

        if (level != null)
        {
            EditorGUI.BeginChangeCheck();
            int newWidth = EditorGUILayout.IntField("Width", level.width);
            int newHeight = EditorGUILayout.IntField("Height", level.height);
            float newCellSize = EditorGUILayout.FloatField("Cell Size", level.cellSize);
            Sprite newRotationLockSprite = (Sprite)EditorGUILayout.ObjectField("Lock Chain Sprite", level.rotationLockSprite, typeof(Sprite), false);
            Sprite newBlueEndpointTileSprite = (Sprite)EditorGUILayout.ObjectField("Blue End Tile", level.blueEndpointTileSprite, typeof(Sprite), false);
            Sprite newRedEndpointTileSprite = (Sprite)EditorGUILayout.ObjectField("Red End Tile", level.redEndpointTileSprite, typeof(Sprite), false);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(level, "Edit Pipe Level Settings");
                level.width = Mathf.Max(1, newWidth);
                level.height = Mathf.Max(1, newHeight);
                level.cellSize = Mathf.Max(0.1f, newCellSize);
                level.rotationLockSprite = newRotationLockSprite;
                level.blueEndpointTileSprite = newBlueEndpointTileSprite;
                level.redEndpointTileSprite = newRedEndpointTileSprite;
                level.ClampPiecesToBounds();
                MarkLevelDirty();
            }

            EnsureLevelDefaultSprites();
        }
    }

    private void DrawSceneControls()
    {
        EditorGUILayout.LabelField("Scene Tools", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capture Scene -> Level"))
            {
                if (EnsureLevelAsset())
                {
                    CaptureSceneToLevel();
                }
            }

            if (GUILayout.Button("Build Palette From Scene"))
            {
                if (EnsurePaletteAsset())
                {
                    BuildPaletteFromScene();
                }
            }
        }

        replaceExistingPipes = EditorGUILayout.ToggleLeft("Replace existing scene pipes when building", replaceExistingPipes);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = level != null;
            if (GUILayout.Button("Build Scene From Level"))
            {
                BuildSceneFromLevel();
            }

            if (GUILayout.Button("Clear Scene Pipes"))
            {
                ClearScenePipes();
            }

            GUI.enabled = true;
        }

        if (GUILayout.Button("Apply Components And Sound To Scene Pipes"))
        {
            int changedCount = PipeComponentSetupUtility.ApplyToScenePipes();
            ShowNotification(new GUIContent("Updated pipes: " + changedCount));
        }

        if (palette != null && GUILayout.Button("Add Red Variants To Palette"))
        {
            int addedCount = AddRedVariantsToPalette();
            ShowNotification(new GUIContent("Added red brushes: " + addedCount));
        }
    }

    private void DrawPalette()
    {
        EditorGUILayout.LabelField("Brushes", EditorStyles.boldLabel);

        if (palette == null)
        {
            EditorGUILayout.HelpBox("Create a palette or build one from the current scene.", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            eraserMode = GUILayout.Toggle(eraserMode, "Eraser", "Button", GUILayout.Width(90), GUILayout.Height(26));

            GUI.enabled = !eraserMode;
            if (GUILayout.Button("Rotate", GUILayout.Width(90), GUILayout.Height(26)))
            {
                rotationSteps = PipeLevelUtility.NormalizeRotationSteps(rotationSteps + 1);
            }

            GUILayout.Label("Rotation: " + (-90 * rotationSteps) + " deg", GUILayout.Width(130));
            paintRole = (PaintRole)EditorGUILayout.EnumPopup("Role", paintRole, GUILayout.Width(230));
            paintPipeColor = (PipeFlowColor)EditorGUILayout.EnumPopup("Color", PipeLevelUtility.NormalizeFlowColor(paintPipeColor), GUILayout.Width(170));
            paintRotationLocked = GUILayout.Toggle(paintRotationLocked, "Locked", "Button", GUILayout.Width(90), GUILayout.Height(26));
            GUI.enabled = true;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = 0; i < palette.brushes.Count; i++)
            {
                PipeBrush brush = palette.brushes[i];
                GUIContent content = BuildBrushContent(brush, i);
                bool selected = !eraserMode && selectedBrushIndex == i;
                Color originalColor = GUI.backgroundColor;
                if (selected)
                {
                    GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
                }

                if (GUILayout.Button(content, GUILayout.Width(PaletteButtonSize), GUILayout.Height(PaletteButtonSize)))
                {
                    selectedBrushIndex = i;
                    eraserMode = false;
                }

                GUI.backgroundColor = originalColor;
            }
        }
    }

    private void DrawLevelGrid()
    {
        EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);

        if (level == null)
        {
            EditorGUILayout.HelpBox("Create or assign a level asset before editing the grid.", MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int y = level.height - 1; y >= 0; y--)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(y.ToString(), GUILayout.Width(24), GUILayout.Height(GridButtonSize));

                for (int x = 0; x < level.width; x++)
                {
                    DrawGridCell(x, y);
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(24);
            for (int x = 0; x < level.width; x++)
            {
                GUILayout.Label(x.ToString(), GUILayout.Width(GridButtonSize));
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawGridCell(int x, int y)
    {
        Rect rect = GUILayoutUtility.GetRect(GridButtonSize, GridButtonSize, GUILayout.Width(GridButtonSize), GUILayout.Height(GridButtonSize));
        PipeLevelPiece piece = level.GetPiece(x, y);
        GUIContent content = BuildCellContent(piece, x, y);

        Color originalColor = GUI.backgroundColor;
        if (piece != null)
        {
            if (piece.isStartingPipe)
            {
                GUI.backgroundColor = new Color(0.55f, 1f, 0.55f);
            }
            else if (piece.isEndingPipe)
            {
                GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
            }
        }

        GUI.Box(rect, content, GUI.skin.button);
        GUI.backgroundColor = originalColor;

        if (piece != null)
        {
            string colorPrefix = PipeLevelUtility.NormalizeFlowColor(piece.pipeColor) == PipeFlowColor.Red ? "R" : "B";
            string roleLabel = piece.isStartingPipe ? colorPrefix + "S" : piece.isEndingPipe ? colorPrefix + "E" : string.Empty;
            if (piece.isRotationLocked)
            {
                DrawLockOverlay(rect);
            }

            if (!string.IsNullOrEmpty(roleLabel))
            {
                GUI.Label(new Rect(rect.x + 3, rect.y + 2, 28, 16), roleLabel, EditorStyles.boldLabel);
            }
        }

        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.MouseDown && rect.Contains(currentEvent.mousePosition))
        {
            if (currentEvent.button == 1)
            {
                EraseCell(x, y);
                currentEvent.Use();
            }
            else if (currentEvent.button == 0)
            {
                if (eraserMode)
                {
                    EraseCell(x, y);
                }
                else
                {
                    PaintCell(x, y);
                }

                currentEvent.Use();
            }
        }
    }

    private GUIContent BuildBrushContent(PipeBrush brush, int index)
    {
        if (brush == null)
        {
            return new GUIContent("?");
        }

        Sprite previewSprite = GetBrushPreviewSprite(brush);
        Texture preview = previewSprite != null ? AssetPreview.GetAssetPreview(previewSprite) : null;
        if (preview == null && previewSprite != null)
        {
            preview = AssetPreview.GetMiniThumbnail(previewSprite);
        }

        string label = preview == null ? (index + 1).ToString() : string.Empty;
        string role = brush.isStartingPipe ? "Start" : brush.isEndingPipe ? "End" : "Normal";
        string locked = brush.isRotationLocked ? " / Locked" : string.Empty;
        return new GUIContent(label, preview, brush.displayName + " / " + role + " / " + PipeLevelUtility.FlowColorName(brush.pipeColor) + locked + " / " + PipeLevelUtility.OpeningsKey(brush.openings));
    }

    private GUIContent BuildCellContent(PipeLevelPiece piece, int x, int y)
    {
        if (piece == null)
        {
            return new GUIContent(".", "Empty (" + x + ", " + y + ")");
        }

        Sprite previewSprite = GetPiecePreviewSprite(piece);
        Texture preview = previewSprite != null ? AssetPreview.GetAssetPreview(previewSprite) : null;
        if (preview == null && previewSprite != null)
        {
            preview = AssetPreview.GetMiniThumbnail(previewSprite);
        }

        string label = preview == null ? "P" : string.Empty;
        string locked = piece.isRotationLocked ? " / Locked" : string.Empty;
        return new GUIContent(label, preview, piece.displayName + " (" + x + ", " + y + ") / " + PipeLevelUtility.FlowColorName(piece.pipeColor) + locked);
    }

    private void DrawLockOverlay(Rect rect)
    {
        Texture lockPreview = level != null && level.rotationLockSprite != null
            ? AssetPreview.GetAssetPreview(level.rotationLockSprite)
            : null;
        if (lockPreview == null && level != null && level.rotationLockSprite != null)
        {
            lockPreview = AssetPreview.GetMiniThumbnail(level.rotationLockSprite);
        }

        if (lockPreview != null)
        {
            GUI.DrawTexture(rect, lockPreview, ScaleMode.ScaleToFit, true);
        }
        else
        {
            GUI.Label(new Rect(rect.xMax - 15, rect.y + 2, 14, 16), "L", EditorStyles.boldLabel);
        }
    }

    private void PaintCell(int x, int y)
    {
        if (palette == null || palette.brushes.Count == 0 || selectedBrushIndex < 0 || selectedBrushIndex >= palette.brushes.Count)
        {
            ShowNotification(new GUIContent("Select a brush first."));
            return;
        }

        PipeBrush brush = palette.brushes[selectedBrushIndex];
        if (brush == null)
        {
            return;
        }

        Undo.RecordObject(level, "Paint Pipe Cell");

        PipeLevelPiece piece = new PipeLevelPiece();
        piece.CopyFromBrush(brush, x, y, rotationSteps);
        piece.redWaterSprite = ResolveRedWaterSprite(piece);

        if (paintRole == PaintRole.Normal)
        {
            piece.isStartingPipe = false;
            piece.isEndingPipe = false;
            piece.pipeColor = paintPipeColor;
        }
        else if (paintRole == PaintRole.Start)
        {
            piece.isStartingPipe = true;
            piece.isEndingPipe = false;
            piece.pipeColor = paintPipeColor;
        }
        else if (paintRole == PaintRole.End)
        {
            piece.isStartingPipe = false;
            piece.isEndingPipe = true;
            piece.pipeColor = paintPipeColor;
        }
        else
        {
            piece.pipeColor = PipeLevelUtility.NormalizeFlowColor(piece.pipeColor);
        }

        piece.isRotationLocked = paintRotationLocked;
        piece.redWaterSprite = ResolveRedWaterSprite(piece);

        level.SetPiece(piece);
        MarkLevelDirty();
    }

    private void EraseCell(int x, int y)
    {
        if (level == null)
        {
            return;
        }

        Undo.RecordObject(level, "Erase Pipe Cell");
        level.RemovePiece(x, y);
        MarkLevelDirty();
    }

    private bool EnsureLevelAsset()
    {
        if (level != null)
        {
            return true;
        }

        return CreateLevelAsset();
    }

    private bool EnsurePaletteAsset()
    {
        if (palette != null)
        {
            return true;
        }

        return CreatePaletteAsset();
    }

    private bool CreateLevelAsset()
    {
        EnsureFolder("Assets/Levels");
        string path = EditorUtility.SaveFilePanelInProject("Create Pipe Level", "PipeLevel", "asset", "Choose where to save the level.", "Assets/Levels");
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        PipeLevelData newLevel = CreateInstance<PipeLevelData>();
        AssignDefaultSprites(newLevel);
        AssetDatabase.CreateAsset(newLevel, path);
        AssetDatabase.SaveAssets();
        level = newLevel;
        Selection.activeObject = level;
        return true;
    }

    private bool CreatePaletteAsset()
    {
        EnsureFolder("Assets/Levels");
        string path = EditorUtility.SaveFilePanelInProject("Create Pipe Palette", "PipePalette", "asset", "Choose where to save the palette.", "Assets/Levels");
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        PipePalette newPalette = CreateInstance<PipePalette>();
        AssetDatabase.CreateAsset(newPalette, path);
        AssetDatabase.SaveAssets();
        palette = newPalette;
        Selection.activeObject = palette;
        return true;
    }

    private void CaptureSceneToLevel()
    {
        Blockin[] pipes = Object.FindObjectsByType<Blockin>(FindObjectsInactive.Include);
        if (pipes.Length == 0)
        {
            EditorUtility.DisplayDialog("Pipe Level Editor", "No Blockin pipes were found in this scene.", "OK");
            return;
        }

        Undo.RecordObject(level, "Capture Scene To Level");
        PipeComponentSetupUtility.ApplyToScenePipes();
        AssignDefaultSprites(level);

        level.pieces.Clear();

        int maxX = 0;
        int maxY = 0;
        float capturedCellSize = 2f;
        bool hasCapturedCellSize = false;
        for (int i = 0; i < pipes.Length; i++)
        {
            Blockin pipe = pipes[i];
            if (pipe == null || !pipe.gameObject.scene.IsValid())
            {
                continue;
            }

            if (!hasCapturedCellSize)
            {
                capturedCellSize = Mathf.Max(0.1f, pipe.cellSize);
                hasCapturedCellSize = true;
            }

            PipeLevelPiece piece = new PipeLevelPiece();
            piece.CopyFromBlock(pipe);
            piece.redWaterSprite = ResolveRedWaterSprite(piece);
            if (!piece.isRotationLocked && SceneHasLockOverlayAtPipe(pipe, capturedCellSize))
            {
                piece.isRotationLocked = true;
            }

            level.SetPiece(piece);
            maxX = Mathf.Max(maxX, piece.x);
            maxY = Mathf.Max(maxY, piece.y);
        }

        PipeManager manager = Object.FindAnyObjectByType<PipeManager>();
        level.width = manager != null ? manager.width : Mathf.Max(level.width, maxX + 1);
        level.height = manager != null ? manager.height : Mathf.Max(level.height, maxY + 1);
        level.cellSize = capturedCellSize;
        level.ClampPiecesToBounds();
        MarkLevelDirty();

        ShowNotification(new GUIContent("Captured " + level.pieces.Count + " pipes."));
    }

    private void BuildPaletteFromScene()
    {
        Blockin[] pipes = Object.FindObjectsByType<Blockin>(FindObjectsInactive.Include);
        if (pipes.Length == 0)
        {
            EditorUtility.DisplayDialog("Pipe Level Editor", "No Blockin pipes were found in this scene.", "OK");
            return;
        }

        Undo.RecordObject(palette, "Build Palette From Scene");
        PipeComponentSetupUtility.ApplyToScenePipes();
        palette.brushes.Clear();

        for (int i = 0; i < pipes.Length; i++)
        {
            Blockin pipe = pipes[i];
            if (pipe == null || !pipe.gameObject.scene.IsValid())
            {
                continue;
            }

            PipeBrush brush = new PipeBrush();
            brush.CopyFromBlock(pipe);
            brush.redWaterSprite = ResolveRedWaterSprite(brush);
            if (!brush.isRotationLocked && SceneHasLockOverlayAtPipe(pipe, pipe.cellSize))
            {
                brush.isRotationLocked = true;
            }

            if (!ContainsMatchingBrush(palette.brushes, brush))
            {
                palette.brushes.Add(brush);
            }
        }

        EditorUtility.SetDirty(palette);
        AssetDatabase.SaveAssets();
        selectedBrushIndex = Mathf.Clamp(selectedBrushIndex, 0, Mathf.Max(0, palette.brushes.Count - 1));
        ShowNotification(new GUIContent("Palette brushes: " + palette.brushes.Count));
    }

    private void BuildSceneFromLevel()
    {
        if (level == null)
        {
            return;
        }

        AssignDefaultSprites(level);
        MarkLevelDirty();

        string message = replaceExistingPipes
            ? "This will replace all Blockin pipes in the current scene."
            : "This will replace pipes under LevelPipes only.";

        if (!EditorUtility.DisplayDialog("Build Scene From Level", message, "Build", "Cancel"))
        {
            return;
        }

        PipeLevelSceneBuilder.BuildLevel(level, null, replaceExistingPipes, null, null);
        PipeComponentSetupUtility.ApplyToScenePipes();
        PipeManager manager = Object.FindAnyObjectByType<PipeManager>();
        if (manager != null)
        {
            Undo.RecordObject(manager, "Assign Pipe Level");
            manager.levelToLoad = level;
            manager.loadLevelOnStart = false;
            manager.width = level.width;
            manager.height = level.height;
            EditorUtility.SetDirty(manager);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private void ClearScenePipes()
    {
        if (!EditorUtility.DisplayDialog("Clear Scene Pipes", "This will delete Blockin pipe objects from the current scene.", "Clear", "Cancel"))
        {
            return;
        }

        Transform root = null;
        GameObject rootObject = GameObject.Find(PipeLevelSceneBuilder.DefaultRootName);
        if (rootObject != null)
        {
            root = rootObject.transform;
        }

        PipeLevelSceneBuilder.ClearScenePipes(root, replaceExistingPipes);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private bool ContainsMatchingBrush(List<PipeBrush> brushes, PipeBrush candidate)
    {
        for (int i = 0; i < brushes.Count; i++)
        {
            PipeBrush brush = brushes[i];
            if (brush == null)
            {
                continue;
            }

            if (brush.emptySprite == candidate.emptySprite &&
                brush.waterSprite == candidate.waterSprite &&
                brush.material == candidate.material &&
                brush.isStartingPipe == candidate.isStartingPipe &&
                brush.isEndingPipe == candidate.isEndingPipe &&
                brush.isRotationLocked == candidate.isRotationLocked &&
                PipeLevelUtility.NormalizeFlowColor(brush.pipeColor) == PipeLevelUtility.NormalizeFlowColor(candidate.pipeColor) &&
                brush.sortingLayerName == candidate.sortingLayerName &&
                brush.sortingOrder == candidate.sortingOrder &&
                Mathf.Approximately(brush.rotationZ, candidate.rotationZ) &&
                brush.scale == candidate.scale &&
                brush.redWaterSprite == candidate.redWaterSprite &&
                PipeLevelUtility.OpeningsKey(brush.openings) == PipeLevelUtility.OpeningsKey(candidate.openings))
            {
                return true;
            }
        }

        return false;
    }

    private int AddRedVariantsToPalette()
    {
        if (palette == null)
        {
            return 0;
        }

        Undo.RecordObject(palette, "Add Red Pipe Brushes");
        int addedCount = 0;
        int originalCount = palette.brushes.Count;

        for (int i = 0; i < originalCount; i++)
        {
            PipeBrush source = palette.brushes[i];
            if (source == null)
            {
                continue;
            }

            if (PipeLevelUtility.NormalizeFlowColor(source.pipeColor) == PipeFlowColor.Red)
            {
                continue;
            }

            PipeBrush redBrush = new PipeBrush();
            redBrush.CopyFromPiece(CreatePieceFromBrush(source));
            redBrush.pipeColor = PipeFlowColor.Red;
            redBrush.redWaterSprite = ResolveRedWaterSprite(redBrush);
            if (redBrush.redWaterSprite == null)
            {
                continue;
            }

            float redScale = GetRedSpriteScale(redBrush.redWaterSprite);
            redBrush.scale = new Vector3(redScale, redScale, source.scale.z);
            redBrush.displayName = "Red " + source.displayName;
            if (source.isStartingPipe)
            {
                redBrush.emptySprite = redBrush.redWaterSprite;
                redBrush.waterSprite = redBrush.redWaterSprite;
            }

            if (!ContainsMatchingBrush(palette.brushes, redBrush))
            {
                palette.brushes.Add(redBrush);
                addedCount++;
            }
        }

        EditorUtility.SetDirty(palette);
        AssetDatabase.SaveAssets();
        Repaint();
        return addedCount;
    }

    private PipeLevelPiece CreatePieceFromBrush(PipeBrush brush)
    {
        PipeLevelPiece piece = new PipeLevelPiece();
        piece.CopyFromBrush(brush, 0, 0, 0);
        return piece;
    }

    private Sprite GetBrushPreviewSprite(PipeBrush brush)
    {
        if (brush == null)
        {
            return null;
        }

        if (PipeLevelUtility.NormalizeFlowColor(brush.pipeColor) == PipeFlowColor.Red)
        {
            Sprite redSprite = ResolveRedWaterSprite(brush);
            if (redSprite != null)
            {
                return redSprite;
            }
        }

        return brush.emptySprite != null ? brush.emptySprite : brush.waterSprite;
    }

    private Sprite GetPiecePreviewSprite(PipeLevelPiece piece)
    {
        if (piece == null)
        {
            return null;
        }

        if (PipeLevelUtility.NormalizeFlowColor(piece.pipeColor) == PipeFlowColor.Red)
        {
            Sprite redSprite = ResolveRedWaterSprite(piece);
            if (redSprite != null)
            {
                return redSprite;
            }
        }

        return piece.emptySprite != null ? piece.emptySprite : piece.waterSprite;
    }

    private Sprite ResolveRedWaterSprite(PipeBrush brush)
    {
        if (brush == null)
        {
            return null;
        }

        return ResolveRedWaterSprite(brush.redWaterSprite, brush.waterSprite, brush.emptySprite);
    }

    private Sprite ResolveRedWaterSprite(PipeLevelPiece piece)
    {
        if (piece == null)
        {
            return null;
        }

        return ResolveRedWaterSprite(piece.redWaterSprite, piece.waterSprite, piece.emptySprite);
    }

    private Sprite ResolveRedWaterSprite(Sprite existingRedSprite, Sprite waterSprite, Sprite emptySprite)
    {
        if (existingRedSprite != null)
        {
            return existingRedSprite;
        }

        Sprite source = waterSprite != null ? waterSprite : emptySprite;
        if (source == null)
        {
            return null;
        }

        if (source.name.StartsWith("red"))
        {
            return source;
        }

        string redName = GetRedSpriteName(source.name);
        return string.IsNullOrEmpty(redName) ? null : LoadSprite("Assets/Grey/" + redName + ".png", redName + "_0");
    }

    private string GetRedSpriteName(string sourceName)
    {
        if (string.IsNullOrEmpty(sourceName))
        {
            return string.Empty;
        }

        if (sourceName.StartsWith("red"))
        {
            int underscoreIndex = sourceName.IndexOf('_');
            return underscoreIndex > 0 ? sourceName.Substring(0, underscoreIndex) : sourceName;
        }

        if (sourceName.StartsWith("pipeGrey_") && sourceName.Length >= "pipeGrey_00".Length)
        {
            string number = sourceName.Substring("pipeGrey_".Length, 2);
            if (number == "29" || number == "30" || number == "31" || number == "32" || number == "42")
            {
                return "red" + number;
            }
        }

        return string.Empty;
    }

    private float GetRedSpriteScale(Sprite sprite)
    {
        if (sprite == null)
        {
            return 1f;
        }

        switch (GetRedSpriteName(sprite.name))
        {
            case "red29":
            case "red30":
                return 0.7f;
            case "red31":
            case "red32":
            case "red33":
                return 0.9f;
            case "red42":
                return 0.6f;
            default:
                return 1f;
        }
    }

    private bool SceneHasLockOverlayAtPipe(Blockin pipe, float cellSize)
    {
        if (pipe == null)
        {
            return false;
        }

        SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include);
        float maxDistance = Mathf.Max(0.1f, cellSize) * 0.75f;
        Vector3 pipePosition = pipe.transform.position;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.transform.IsChildOf(pipe.transform))
            {
                continue;
            }

            string objectName = renderer.gameObject.name;
            string spriteName = renderer.sprite != null ? renderer.sprite.name : string.Empty;
            if (!objectName.Contains("chain") && !spriteName.Contains("chain"))
            {
                continue;
            }

            if (Vector3.Distance(renderer.bounds.center, pipePosition) <= maxDistance)
            {
                return true;
            }
        }

        return false;
    }

    private void MarkLevelDirty()
    {
        if (level == null)
        {
            return;
        }

        EditorUtility.SetDirty(level);
        AssetDatabase.SaveAssets();
        Repaint();
    }

    private void EnsureLevelDefaultSprites()
    {
        if (level == null)
        {
            return;
        }

        if (level.rotationLockSprite == null ||
            level.blueEndpointTileSprite == null ||
            level.redEndpointTileSprite == null)
        {
            Undo.RecordObject(level, "Assign Pipe Level Default Sprites");
            AssignDefaultSprites(level);
            MarkLevelDirty();
        }
    }

    private void AssignDefaultSprites(PipeLevelData targetLevel)
    {
        if (targetLevel == null)
        {
            return;
        }

        if (targetLevel.rotationLockSprite == null)
        {
            targetLevel.rotationLockSprite = LoadDefaultRotationLockSprite();
        }

        if (targetLevel.blueEndpointTileSprite == null)
        {
            targetLevel.blueEndpointTileSprite = LoadDefaultBlueEndpointTileSprite();
        }

        if (targetLevel.redEndpointTileSprite == null)
        {
            targetLevel.redEndpointTileSprite = LoadDefaultRedEndpointTileSprite();
        }
    }

    private void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private Sprite LoadDefaultRotationLockSprite()
    {
        return LoadSprite(DefaultRotationLockSpritePath, DefaultRotationLockSpriteName);
    }

    private Sprite LoadDefaultBlueEndpointTileSprite()
    {
        return LoadSprite(DefaultBlueEndpointTileSpritePath, DefaultBlueEndpointTileSpriteName);
    }

    private Sprite LoadDefaultRedEndpointTileSprite()
    {
        return LoadSprite(DefaultRedEndpointTileSpritePath, DefaultRedEndpointTileSpriteName);
    }

    private Sprite LoadSprite(string path, string spriteName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            Sprite sprite = assets[i] as Sprite;
            if (sprite != null && sprite.name == spriteName)
            {
                return sprite;
            }
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
