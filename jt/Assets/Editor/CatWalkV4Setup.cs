#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Uses the same true 4x4, 314 px grid as Rabbit V4.
/// The cat source is preserved; only transparent padding was added to make 1256 px.
/// </summary>
[InitializeOnLoad]
public static class CatWalkV4Setup
{
    private const string SheetPath = "Assets/new LTC/寵物系統/Art/Pets/cat_walk_sheet_v4_1256.png";
    private const string Folder = "Assets/new LTC/寵物系統/Art/Pets/CatV4Animation";
    private const string IdlePath = Folder + "/Cat_Idle_V4.anim";
    private const string WalkPath = Folder + "/Cat_WalkLoop_V4.anim";
    private const string ControllerPath = Folder + "/Cat_Walk_V4.controller";

    static CatWalkV4Setup()
    {
        EditorApplication.delayCall += Configure;
    }

    [MenuItem("LTC/寵物/重新建立貓咪 V4 動畫（4x4）")]
    private static void Configure()
    {
        // Import/scene edits are editor-only work. Never attempt them while Play Mode is active.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
        if (importer == null)
            return;

        bool needsImport = importer.textureType != TextureImporterType.Sprite
                           || importer.spriteImportMode != SpriteImportMode.Multiple
                           || importer.npotScale != TextureImporterNPOTScale.None
                           || importer.maxTextureSize < 1256
                           || importer.spritesheet == null
                           || importer.spritesheet.Length != 16
                           || importer.spritesheet.Any(s => s.rect.width != 314f || s.rect.height != 314f);

        if (needsImport)
        {
            var slices = new SpriteMetaData[16];
            for (int visualRow = 0; visualRow < 4; visualRow++)
            {
                int sourceRow = 3 - visualRow;
                for (int column = 0; column < 4; column++)
                {
                    int index = visualRow * 4 + column;
                    slices[index] = new SpriteMetaData
                    {
                        name = "cat_walk_v4_" + index.ToString("00"),
                        rect = new Rect(column * 314, sourceRow * 314, 314, 314),
                        alignment = (int)SpriteAlignment.Custom,
                        pivot = new Vector2(0.5f, 0f)
                    };
                }
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritesheet = slices;
            importer.SaveAndReimport();
        }

        var frames = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
        if (frames.Length != 16)
        {
            Debug.LogError("貓咪 V4 切片失敗：預期 16 格，實際 " + frames.Length + " 格。");
            return;
        }
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/new LTC/寵物系統/Art/Pets", "CatV4Animation");

        var idle = CreateClip(IdlePath, new[] { frames[0] }, true);
        var walk = CreateClip(WalkPath, frames, true);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var idleState = machine.AddState("待機", new Vector3(220f, 80f, 0f));
            var walkState = machine.AddState("行走（16 格）", new Vector3(500f, 80f, 0f));
            idleState.motion = idle;
            walkState.motion = walk;
            machine.defaultState = idleState;
            var toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false; toWalk.duration = 0.05f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "Walking");
            var toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false; toIdle.duration = 0.05f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Walking");
        }

        var cat = GameObject.Find("貓咪（待機動畫）");
        if (cat != null)
        {
            // The early prototype has an old renderer/animator on the root and a new
            // visual child. Keeping both enabled causes two cats to be drawn together.
            var oldRenderer = cat.GetComponent<SpriteRenderer>();
            var oldAnimator = cat.GetComponent<Animator>();
            if (oldRenderer != null) oldRenderer.enabled = false;
            if (oldAnimator != null) oldAnimator.enabled = false;

            var artwork = cat.transform.Find("Visual/Artwork");
            var renderer = artwork == null ? null : artwork.GetComponent<SpriteRenderer>();
            var animator = artwork == null ? null : artwork.GetComponent<Animator>();
            if (renderer != null && animator != null)
            {
                renderer.sprite = frames[0];
                renderer.sortingOrder = 2;
                animator.runtimeAnimatorController = controller;

                // This sheet's natural walk faces left. PetWander flips it only when
                // moving right, so it no longer appears to walk backward.
                var wander = cat.GetComponent<PetWander>();
                if (wander != null)
                {
                    var serializedWander = new SerializedObject(wander);
                    serializedWander.FindProperty("sourceFacesRight").boolValue = false;
                    serializedWander.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(renderer);
                EditorUtility.SetDirty(animator);
                EditorSceneManager.MarkSceneDirty(cat.scene);
                EditorSceneManager.SaveScene(cat.scene);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("貓咪 V4 已完成：1256×1256 圖片、4×4 等分、每格 314×314。", AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath));
    }

    private static AnimationClip CreateClip(string path, Sprite[] frames, bool loop)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.frameRate = 10f;
        var binding = new EditorCurveBinding { type = typeof(SpriteRenderer), path = string.Empty, propertyName = "m_Sprite" };
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }
}
#endif
