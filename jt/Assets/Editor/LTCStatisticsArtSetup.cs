#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using TMPro;

public static class LTCStatisticsArtSetup
{
    const string Art="Assets/UI/Statistics/Art/";
    public static void ImportArt()
    {
        foreach(string name in new[]{"background","icons","panels","navigation"})
        {
            string path=Art+name+".png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.isReadable=true;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            if(name=="background"){importer.isReadable=false;importer.SaveAndReimport();continue;}
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var pixels=tex.GetPixels32();
            int columns=name=="icons"?4:name=="navigation"?5:2,rows=name=="navigation"?1:2;
            var slices=new SpriteMetaData[columns*rows];
            for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
            {
                int x0=col*tex.width/columns,x1=(col+1)*tex.width/columns;
                int y0=(rows-row-1)*tex.height/rows,y1=(rows-row)*tex.height/rows;
                if(name=="panels"){y0=row==0?(int)(tex.height*.40f):0;y1=row==0?tex.height:(int)(tex.height*.40f);}
                int minX=x1,maxX=x0,minY=y1,maxY=y0;
                for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)if(pixels[y*tex.width+x].a>100){minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
                if(maxX<minX||maxY<minY)throw new Exception("Empty sprite cell: "+name+row+col);
                minX=Math.Max(x0,minX-2);minY=Math.Max(y0,minY-2);maxX=Math.Min(x1-1,maxX+2);maxY=Math.Min(y1-1,maxY+2);
                float border=name=="panels"?Mathf.Min(100,(maxY-minY)*.32f):0;
                slices[row*columns+col]=new SpriteMetaData{name=name+"_"+(row*columns+col).ToString("00"),rect=new Rect(minX,minY,maxX-minX+1,maxY-minY+1),pivot=new Vector2(.5f,.5f),alignment=9,border=new Vector4(border,border,border,border)};
            }
            importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritesheet=slices;importer.isReadable=false;importer.SaveAndReimport();
        }
        string fontPath="Assets/UI/Statistics/Fonts/Huninn SDF.asset";
        if(!AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath))
        {
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Statistics/Fonts/jf-openhuninn.ttf");
            var font=TMP_FontAsset.CreateFontAsset(source,64,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
            font.name="Huninn SDF";AssetDatabase.CreateAsset(font,fontPath);
            AssetDatabase.AddObjectToAsset(font.material,font);
            foreach(var texture in font.atlasTextures)AssetDatabase.AddObjectToAsset(texture,font);
        }
        AssetDatabase.SaveAssets();
    }
    public static void Bake()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop play mode before baking.");
        var controller=UnityEngine.Object.FindFirstObjectByType<CognitiveGameCatalogController>();
        var page=controller.transform.Find("Cognitive Catalog Canvas/統計頁");var view=page.GetComponent<LTCStatisticsPrototype>();
        view.background=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"background.png");
        var panels=AssetDatabase.LoadAllAssetsAtPath(Art+"panels.png").OfType<Sprite>().OrderBy(s=>s.name).ToArray();view.panelSkin=panels[0];view.tabSkin=panels[3];
        view.gameIcons=AssetDatabase.LoadAllAssetsAtPath(Art+"icons.png").OfType<Sprite>().OrderBy(s=>s.name).ToArray();
        view.navigationIcons=AssetDatabase.LoadAllAssetsAtPath(Art+"navigation.png").OfType<Sprite>().OrderBy(s=>s.name).ToArray();
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Statistics/Fonts/Huninn SDF.asset");view.Build(font);
        foreach(var t in view.GetComponentsInChildren<TMP_Text>(true))t.ForceMeshUpdate(true);
        EditorUtility.SetDirty(view);EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(page.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(page.gameObject.scene);
    }
}
#endif
