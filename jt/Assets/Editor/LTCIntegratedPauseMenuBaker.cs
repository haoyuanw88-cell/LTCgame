using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class LTCIntegratedPauseMenuBaker {
    [MenuItem("Tools/LTC/Bake Integrated Game Pause Menus")]
    public static void Bake() {
        var sourceScene=EditorSceneManager.OpenScene("Assets/new LTC/場景轉換/mb.unity",OpenSceneMode.Additive);
        try {
            var source=sourceScene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<CognitiveGamePauseMenu>(true)).First();
            foreach(var path in new[]{"Assets/FreeNet/Scenes/PipeGame.unity","Assets/cards/Scenes/CardsGame.unity","Assets/supermarket/Scenes/SupermarketGame.unity","Assets/text puzzle/Scenes/TextPuzzleGame.unity","Assets/new LTC/場景轉換/gopher.unity"}) {
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try {
                    foreach(var old in scene.GetRootGameObjects().Where(x=>x.name=="LTC Integrated Game Pause Canvas").ToArray()) Object.DestroyImmediate(old);
                    var root=new GameObject("LTC Integrated Game Pause Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
                    SceneManager.MoveGameObjectToScene(root,scene);
                    var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32001;
                    var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1366,768);scaler.matchWidthOrHeight=.5f;
                    var button=Object.Instantiate(source.pauseButton.gameObject,root.transform,false);
                    var panel=Object.Instantiate(source.pausePanel,root.transform,false);
                    var menu=root.AddComponent<CognitiveGamePauseMenu>();
                    menu.pauseButton=button.GetComponent<Button>();menu.pausePanel=panel;
                    menu.resumeButton=Clone(source.resumeButton,source.pausePanel.transform,panel.transform);
                    menu.homeButton=Clone(source.homeButton,source.pausePanel.transform,panel.transform);
                    menu.messageText=Clone(source.messageText,source.pausePanel.transform,panel.transform);
                    menu.messageText.fontSize=26;menu.messageText.enableAutoSizing=true;menu.messageText.fontSizeMin=22;menu.messageText.fontSizeMax=26;
                    menu.resumeKeepsProgress=true;menu.gameHomeScene="GameScene";panel.SetActive(false);
                    if(!scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<EventSystem>(true)).Any()) {
                        var events=new GameObject("EventSystem",typeof(EventSystem),typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                        SceneManager.MoveGameObjectToScene(events,scene);
                    }
                    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                } finally {EditorSceneManager.CloseScene(scene,true);}
            }
        } finally {EditorSceneManager.CloseScene(sourceScene,true);}
    }
    static T Clone<T>(T component,Transform source,Transform destination) where T:Component {
        return destination.Find(AnimationUtility.CalculateTransformPath(component.transform,source)).GetComponent<T>();
    }
}