using System.IO;
using UnityEngine;

public class BoboLabel : MonoBehaviour
{
    [System.Serializable]
    public class LabeledObject
    {
        public GameObject targetObject;
        public int classId;
    }
    public int imageStartIdx = 0;
    public LabeledObject[] labeledObjects;
    public string DatasetFolder;
    public bool captureButton = false;
    public bool CapWithTime = false;
    public float delay = 0.5f;

    private Camera cam;
    private bool _screenshotSaved = false;
    private float _timeSinceLastCall = 0.0f;

    private string _labelFolder => Path.Combine(DatasetFolder, "labels");
    private string _imagesFolder => Path.Combine(DatasetFolder, "images");

    private void Start()
    {
        cam = GetComponent<Camera>();
        foreach (string folder in new [] {_labelFolder, _imagesFolder})
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }
    }

    private void Update()
    {
        _timeSinceLastCall += Time.deltaTime;

        if (CapWithTime && _timeSinceLastCall >= delay)
        {
            CaptureAndLabel();
            _timeSinceLastCall = 0.0f;

        }
        if (Input.GetKeyDown(KeyCode.Space) || captureButton)
        {
            CaptureAndLabel();
            captureButton = false;
        }
    }

    private void CaptureAndLabel()
    {
        _screenshotSaved = false;

        RenderTexture renderTexture = new RenderTexture(cam.pixelWidth, cam.pixelHeight, 24);
        cam.targetTexture = renderTexture;
        Texture2D screenshot = new Texture2D(cam.pixelWidth, cam.pixelHeight, TextureFormat.RGB24, false);
        cam.Render();
        RenderTexture.active = renderTexture;
        screenshot.ReadPixels(new Rect(0, 0, cam.pixelWidth, cam.pixelHeight), 0, 0);
        screenshot.Apply();
        cam.targetTexture = null;
        RenderTexture.active = null;
        Destroy(renderTexture);
        string labelPath = Path.Combine(_labelFolder, $"{imageStartIdx}.txt");

        if (File.Exists(labelPath))
        {
            File.Delete(labelPath);
        }

        for (int i = 0; i < labeledObjects.Length; i++)
        {
            GameObject targetObject = labeledObjects[i].targetObject;
            int classId = labeledObjects[i].classId;

            if (!IsObjectVisible(targetObject))
            {
                Debug.Log("Object is outside camera view. Skipping labeling.");
                continue;
            }
            else
            {
                if (!_screenshotSaved)
                {
                    string imagePath = Path.Combine(_imagesFolder, $"{imageStartIdx}.png");
                    SaveScreenshot(screenshot, Path.Combine(_imagesFolder, $"{imageStartIdx}.png"));
                    _screenshotSaved = true;
                    imageStartIdx++;
                }
            }
            string labelData = GetLabelData(targetObject, classId);
            SaveLabel(labelData, labelPath);
        }
    }

    private bool IsObjectVisible(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null || !obj.activeSelf)
        {
            Debug.LogWarning("Renderer component not found on the object or Gameobject is not enabled");
            return false;
        }

        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cam);
        return GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds);
    }
    private void SaveScreenshot(Texture2D screenshot, string path)
    {
        byte[] bytes = screenshot.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        Debug.Log("Saved image at: " + path);
    }
    private void SaveLabel(string labelData, string labelPath)
    {
        if (File.Exists(labelPath))
        {
            File.AppendAllText(labelPath, labelData + "\n");
        }
        else
        {
            File.WriteAllText(labelPath, labelData + "\n");
        }
        Debug.Log("Saved labeling data at: " + labelPath);       
    }
    private string GetLabelData(GameObject obj, int classId)
    {
        Rect rect = GetScreenRect(obj);
        float centerX = rect.x + (rect.width / 2);
        float centerY = rect.y + (rect.height / 2);
        float normalizedX = centerX / cam.pixelWidth;
        float normalizedY = 1 - (centerY / cam.pixelHeight);
        float normalizedWidth = rect.width / cam.pixelWidth;
        float normalizedHeight = rect.height / cam.pixelHeight;
        string labelData = $"{classId} {normalizedX.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)} " +
                           $"{normalizedY.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)} " +
                           $"{normalizedWidth.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)} " +
                           $"{normalizedHeight.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}";

        return labelData;
    }
    private Rect GetScreenRect(GameObject obj)
    {
        MeshFilter[] meshFilters = obj.GetComponentsInChildren<MeshFilter>();

        if (meshFilters.Length == 0)
        {
            Debug.LogError("MeshFilter or sharedMesh missing on the object or its children.");
            return Rect.zero;
        }

        float left = float.MaxValue;
        float right = float.MinValue;
        float top = float.MaxValue;
        float bottom = float.MinValue;

        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null)
            {
                Debug.LogError("SharedMesh missing on the object.");
                continue;
            }

            Mesh mesh = meshFilter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Vector3[] transformedVertices = new Vector3[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                transformedVertices[i] = meshFilter.transform.TransformPoint(vertices[i]);
            }

            Vector2[] screenCorners = new Vector2[transformedVertices.Length];
            for (int i = 0; i < transformedVertices.Length; i++)
            {
                screenCorners[i] = cam.WorldToScreenPoint(transformedVertices[i]);
            }

            for (int i = 0; i < screenCorners.Length; i++)
            {
                if (screenCorners[i].x < left)
                    left = screenCorners[i].x;
                if (screenCorners[i].x > right)
                    right = screenCorners[i].x;
                if (screenCorners[i].y < top)
                    top = screenCorners[i].y;
                if (screenCorners[i].y > bottom)
                    bottom = screenCorners[i].y;
            }
        }
        return new Rect(left, top, right - left, bottom - top);
    }
    
}

