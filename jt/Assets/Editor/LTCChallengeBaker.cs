#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class LTCChallengeBaker
{
    public const string ScenePath="Assets/Scenes/ChallengeRush.unity";
    [MenuItem("LTC/Build Challenge Rush")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before baking.");
        var original=SceneManager.GetActiveScene();
        var catalog=UnityEngine.Object.FindFirstObjectByType<CognitiveGameCatalogController>();
        if(catalog==null)throw new InvalidOperationException("Open GameScene before baking.");
        var canvas=catalog.transform.Find("Cognitive Catalog Canvas");
        LTCChallengeEntry.Ensure(canvas);
        var font=canvas.Find("我的頁/個人資料內容/設定").GetComponentInChildren<TMPro.TMP_Text>(true).font;
        var skin=canvas.Find("我的頁/個人資料內容/設定").GetComponent<UnityEngine.UI.Image>().sprite;
        var background=canvas.Find("我的頁").GetComponent<UnityEngine.UI.Image>().sprite;
        var asset=AssetDatabase.LoadAssetAtPath<LTCChallengeAssets>("Assets/Resources/ChallengeRushAssets.asset");
        if(asset==null){asset=ScriptableObject.CreateInstance<LTCChallengeAssets>();AssetDatabase.CreateAsset(asset,"Assets/Resources/ChallengeRushAssets.asset");}
        asset.font=font;asset.panel=skin;asset.background=background;
        asset.enemies=Resources.Load<CardLevelCatalog>(CardLevelCatalog.ResourceName);
        var cards=EditorSceneManager.OpenScene("Assets/cards/Scenes/CardsGame.unity",OpenSceneMode.Additive);
        var game=cards.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameController>(true)).First();
        asset.player=game.playerAnimator.GetComponent<SpriteRenderer>().sprite;
        asset.cardBack=game.cardBackBlue;
        asset.cardFaces=game.cardFaces.Where(s=>s!=null).Distinct().ToArray();
        asset.playerAttack=Frames(game.playerAttackClip);
        asset.playerHit=Frames(game.playerHitClip);
        EditorSceneManager.CloseScene(cards,true);
        asset.products=Resources.Load<SupermarketLevelCatalog>("SupermarketLevelCatalog").levels
            .Where(l=>l!=null).SelectMany(l=>l.products).Where(p=>p!=null&&p.productImage!=null).Select(p=>p.productImage).Distinct().ToArray();
        var gophers=EditorSceneManager.OpenScene("Assets/new LTC/場景轉換/gopher.unity",OpenSceneMode.Additive);
        var mole=gophers.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Mole>(true)).First();
        asset.mole=mole.s2;asset.bomb=mole.bombSprite;
        EditorSceneManager.CloseScene(gophers,true);
        Directory.CreateDirectory("Assets/Audio/Challenge");
        const string music="Assets/Audio/Challenge/Sketchbook 2025-11-13.ogg";
        if(!File.Exists(music))File.Copy(@"C:\project material\music\Sketchbook 2025-11-13.ogg",music);
        AssetDatabase.ImportAsset(music,ImportAssetOptions.ForceSynchronousImport);
        var importer=AssetImporter.GetAtPath(music) as AudioImporter;
        if(importer!=null)
        {
            var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.Streaming;
            importer.defaultSampleSettings=settings;importer.SaveAndReimport();
        }
        asset.music=AssetDatabase.LoadAssetAtPath<AudioClip>(music);
        if(asset.music==null)throw new InvalidOperationException("Challenge music did not import.");
        if(asset.cardFaces.Length<3||asset.products.Length<6||asset.mole==null)
            throw new InvalidOperationException("Missing mini-game artwork.");
        EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(original);EditorSceneManager.SaveScene(original);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));
        camera.tag="MainCamera";camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;
        camera.GetComponent<Camera>().backgroundColor=new Color(.18f,.15f,.12f);
        camera.transform.position=new Vector3(0,0,-10);
        var light=new GameObject("Directional Light",typeof(Light));light.GetComponent<Light>().type=LightType.Directional;
        var events=new GameObject("EventSystem",typeof(EventSystem));
        var input=Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if(input!=null)events.AddComponent(input);else events.AddComponent<StandaloneInputModule>();
        var manager=new GameObject("Challenge Rush",typeof(LTCChallengeController));
        manager.GetComponent<LTCChallengeController>().assets=asset;
        EditorSceneManager.SaveScene(scene,ScenePath);
        var scenes=EditorBuildSettings.scenes.ToList();
        if(!scenes.Any(s=>s.path==ScenePath))scenes.Add(new EditorBuildSettingsScene(ScenePath,true));
        EditorBuildSettings.scenes=scenes.ToArray();
        EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(original);
        Debug.Log("Challenge Rush baked: "+asset.cardFaces.Length+" card faces, "+asset.products.Length+" products, music "+asset.music.length+"s.");
    }
    static Sprite[] Frames(AnimationClip clip)
    {
        if(clip==null)return Array.Empty<Sprite>();
        return AnimationUtility.GetObjectReferenceCurveBindings(clip)
            .Where(b=>b.propertyName=="m_Sprite")
            .SelectMany(b=>AnimationUtility.GetObjectReferenceCurve(clip,b))
            .Select(k=>k.value as Sprite).Where(s=>s!=null).ToArray();
    }
}
#endif
