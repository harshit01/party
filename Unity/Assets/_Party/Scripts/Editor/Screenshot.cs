using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Party.EditorTools
{
    /// <summary>
    /// Renders a scene to a PNG from the command line.
    ///
    /// Exists so presentation work can be VERIFIED rather than described. Compiling
    /// proves the code runs; it says nothing about whether the thing looks like a game.
    ///
    ///     Unity -batchmode -quit -executeMethod Party.EditorTools.Screenshot.Shoot
    ///
    /// Note: no -nographics. A null graphics device cannot render.
    /// </summary>
    public static class Screenshot
    {
        public static void Shoot()
        {
            string scene = "Assets/_Party/Scenes/RedLight.unity";
            string outPath = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nopost") < 0
                ? "Build/Shots/redlight.png" : "Build/Shots/redlight_nopost.png";

            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);

            // Players only exist at runtime, so stand a few in for the shot.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Party/Prefabs/PartyPlayer.prefab");
            Color[] palette =
            {
                new Color(0.93f,0.26f,0.21f), new Color(0.20f,0.60f,0.86f),
                new Color(0.18f,0.80f,0.44f), new Color(0.95f,0.77f,0.06f),
            };
            for (int i = 0; i < 4; i++)
            {
                GameObject p = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                p.transform.position = new Vector3(-4.5f + i * 3f, 1.1f, -6f + i * 2.5f);
                Transform body = p.transform.Find("Visual/Body");
                if (body != null)
                {
                    Renderer r = body.GetComponent<Renderer>();
                    Material m = new Material(r.sharedMaterial) { color = palette[i] };
                    m.SetFloat("_Smoothness", 0.45f);
                    r.sharedMaterial = m;
                }
            }

            // Dump what the renderers ACTUALLY hold. Materials on disk being correct and
            // the scene referencing them correctly does not prove the shader reads them.
            foreach (string n in new[] { "Lane", "Barrier L", "FinishGantry", "Crowd L" })
            {
                GameObject g = GameObject.Find(n);
                if (g == null) { Debug.Log($"[Shot] {n}: NOT FOUND"); continue; }
                Renderer r = g.GetComponent<Renderer>() ?? g.GetComponentInChildren<Renderer>();
                if (r == null) { Debug.Log($"[Shot] {n}: no renderer"); continue; }
                Material m = r.sharedMaterial;
                Debug.Log($"[Shot] {n}: mat={(m == null ? "NULL" : m.name)} " +
                          $"shader={(m == null || m.shader == null ? "NULL" : m.shader.name)} " +
                          $"base={(m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "n/a")}");
            }

            Camera cam = Camera.main;
            if (cam == null) { Debug.LogError("[Shot] no main camera"); EditorApplication.Exit(1); return; }

            // -nopost lets us isolate whether post-processing is responsible for a look,
            // instead of reasoning about it from the outside.
            bool post = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nopost") < 0;
            UniversalAdditionalCameraData cd = cam.GetComponent<UniversalAdditionalCameraData>();
            if (cd != null) cd.renderPostProcessing = post;
            if (!post)
            {
                foreach (var v in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(
                             FindObjectsSortMode.None)) v.enabled = false;
            }

            const int W = 1280, H = 720;
            RenderTexture rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;

            System.IO.Directory.CreateDirectory("Build/Shots");
            System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Debug.Log($"[Shot] wrote {outPath} ({W}x{H})");
        }
    }
}
