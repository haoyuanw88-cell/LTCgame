using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PipeComponentSetupUtility
{
    private const string DefaultRotateSoundPath = "Assets/536788__egomassive__flop.ogg";
    private const string DefaultRotationLockSpritePath = "Assets/Grey/chain_shade3.png";
    private const string DefaultRotationLockSpriteName = "chain_shade3_0";

    [MenuItem("Tools/Pipe Level Editor/Apply Components And Sound To Scene Pipes")]
    public static void ApplyToScenePipesMenu()
    {
        int changedCount = ApplyToScenePipes();
        EditorUtility.DisplayDialog("Pipe Setup", "Updated " + changedCount + " pipe object(s).", "OK");
    }

    public static int ApplyToScenePipes()
    {
        AudioClip defaultRotateSound = AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultRotateSoundPath);
        Sprite defaultRotationLockSprite = LoadDefaultRotationLockSprite();
        Blockin[] pipes = Object.FindObjectsByType<Blockin>(FindObjectsInactive.Include);
        int changedCount = 0;

        for (int i = 0; i < pipes.Length; i++)
        {
            Blockin pipe = pipes[i];
            if (pipe == null || !pipe.gameObject.scene.IsValid())
            {
                continue;
            }

            if (ApplyToPipe(pipe, defaultRotateSound, defaultRotationLockSprite))
            {
                changedCount++;
            }
        }

        if (changedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        return changedCount;
    }

    public static bool ApplyToPipe(Blockin pipe, AudioClip defaultRotateSound)
    {
        return ApplyToPipe(pipe, defaultRotateSound, LoadDefaultRotationLockSprite());
    }

    public static bool ApplyToPipe(Blockin pipe, AudioClip defaultRotateSound, Sprite defaultRotationLockSprite)
    {
        if (pipe == null)
        {
            return false;
        }

        bool changed = false;
        GameObject pipeObject = pipe.gameObject;

        if (pipeObject.GetComponent<BoxCollider2D>() == null)
        {
            Undo.AddComponent<BoxCollider2D>(pipeObject);
            changed = true;
        }

        AudioSource audioSource = pipeObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = Undo.AddComponent<AudioSource>(pipeObject);
            changed = true;
        }

        if (audioSource.playOnAwake)
        {
            Undo.RecordObject(audioSource, "Configure Pipe Audio Source");
            audioSource.playOnAwake = false;
            changed = true;
        }

        if (!Mathf.Approximately(audioSource.spatialBlend, 0f))
        {
            Undo.RecordObject(audioSource, "Configure Pipe Audio Source");
            audioSource.spatialBlend = 0f;
            changed = true;
        }

        if (pipe.myAudioSource != audioSource)
        {
            Undo.RecordObject(pipe, "Assign Pipe Audio Source");
            pipe.myAudioSource = audioSource;
            changed = true;
        }

        if (pipe.rotateSound == null && defaultRotateSound != null)
        {
            Undo.RecordObject(pipe, "Assign Pipe Rotate Sound");
            pipe.rotateSound = defaultRotateSound;
            changed = true;
        }

        if (pipe.rotationLockSprite == null && defaultRotationLockSprite != null)
        {
            Undo.RecordObject(pipe, "Assign Pipe Lock Sprite");
            pipe.rotationLockSprite = defaultRotationLockSprite;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(pipeObject);
            EditorUtility.SetDirty(pipe);
            if (audioSource != null)
            {
                EditorUtility.SetDirty(audioSource);
            }
        }

        return changed;
    }

    private static Sprite LoadDefaultRotationLockSprite()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(DefaultRotationLockSpritePath);
        for (int i = 0; i < assets.Length; i++)
        {
            Sprite sprite = assets[i] as Sprite;
            if (sprite != null && sprite.name == DefaultRotationLockSpriteName)
            {
                return sprite;
            }
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(DefaultRotationLockSpritePath);
    }
}
