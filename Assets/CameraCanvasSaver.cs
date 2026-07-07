using UnityEngine;
using System.IO;

public class CameraCanvasSaver : MonoBehaviour
{
    public Renderer targetRenderer;
    public Camera mainCamera;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            Debug.Log("S pressed!");
            SavePaint();
        }
    }

    void SavePaint()
    {
        if (targetRenderer == null)
        {
            Debug.LogError("No Renderer assigned!");
            return;
        }

        if (mainCamera == null)
        {
            Debug.LogError("No Camera assigned!");
            return;
        }

        string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        string path = Path.Combine(desktopPath, "Paint_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");

        Texture mainTex = targetRenderer.material.mainTexture;

        if (mainTex is RenderTexture renderTex)
        {
            Debug.Log("Found RenderTexture: " + renderTex.width + "x" + renderTex.height);

            RenderTexture.active = renderTex;
            Texture2D tex = new Texture2D(renderTex.width, renderTex.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            Destroy(tex);

            Debug.Log("SUCCESS! Saved to Desktop: " + Path.GetFileName(path));
        }
        else if (mainTex is Texture2D tex2D)
        {
            Debug.Log("Found Texture2D: " + tex2D.width + "x" + tex2D.height);

            RenderTexture temp = RenderTexture.GetTemporary(tex2D.width, tex2D.height, 24);
            Graphics.Blit(tex2D, temp);

            RenderTexture.active = temp;
            Texture2D tex = new Texture2D(tex2D.width, tex2D.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, tex2D.width, tex2D.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(temp);

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            Destroy(tex);

            Debug.Log("SUCCESS! Saved to Desktop: " + Path.GetFileName(path));
        }
        else
        {
            Debug.LogError("No texture found! Texture is: " + (mainTex == null ? "null" : mainTex.GetType().Name));
        }
    }
}