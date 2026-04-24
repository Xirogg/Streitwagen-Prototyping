using UnityEngine;

/// <summary>
/// Radial speed-line "motion blur" overlay.
/// Draws procedural streaks around the edges of the screen that grow in
/// count, length and opacity with the chariot's speed.
/// Attach to the same GameObject as the Camera.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraBlur : MonoBehaviour
{
    [Header("Speed Source")]
    [Tooltip("Rigidbody to read speed from. If empty, tries ChariotCamera.target.")]
    [SerializeField] private Rigidbody speedSource;
    [SerializeField] private ChariotCamera chariotCamera;

    [Header("Activation")]
    [Tooltip("Speed (m/s) at which lines start appearing.")]
    [SerializeField] private float activationSpeed = 8f;
    [Tooltip("Speed (m/s) at which the effect reaches full intensity.")]
    [SerializeField] private float maxSpeed = 28f;
    [Tooltip("How fast the intensity reacts to speed changes.")]
    [SerializeField] private float intensityLerpSpeed = 8f;

    [Header("Lines")]
    [Tooltip("Maximum number of radial lines at full intensity.")]
    [SerializeField] private int maxLineCount = 64;
    [Tooltip("Inner radius (fraction of shorter screen side) where lines start. Lines inside this are invisible.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float innerRadius = 0.45f;
    [Tooltip("Outer radius (fraction of shorter screen side) - usually > 0.5 so lines extend past corners.")]
    [Range(0.4f, 1.5f)]
    [SerializeField] private float outerRadius = 0.9f;
    [Tooltip("Line length scale at full intensity (fraction of shorter side).")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float lineLength = 0.18f;
    [Tooltip("Base thickness of a line (normalized). Approximated by drawing multiple parallel lines.")]
    [Range(0f, 0.01f)]
    [SerializeField] private float lineThickness = 0.002f;
    [Tooltip("Color of the speed lines.")]
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.55f);
    [Tooltip("Randomness (0..1) in line angular jitter & length.")]
    [Range(0f, 1f)]
    [SerializeField] private float jitter = 0.6f;
    [Tooltip("Angular scroll speed of the line pattern (rad/s) at max intensity.")]
    [SerializeField] private float rotationSpeed = 0.35f;

    private Camera cam;
    private Material lineMat;
    private float intensity; // 0..1 smoothed
    private float patternAngle;

    private struct LineSeed
    {
        public float angleOffset;  // radians
        public float lengthScale;  // 0.5..1.5
        public float radiusScale;  // 0.9..1.1
    }
    private LineSeed[] seeds;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (chariotCamera == null) chariotCamera = GetComponent<ChariotCamera>();
        CreateMaterial();
        GenerateSeeds();
    }

    private void OnValidate()
    {
        if (seeds == null || seeds.Length != Mathf.Max(1, maxLineCount))
        {
            GenerateSeeds();
        }
    }

    private void CreateMaterial()
    {
        // Built-in "Hidden/Internal-Colored" shader supports GL drawing with vertex colors and blending.
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        lineMat = new Material(shader);
        lineMat.hideFlags = HideFlags.HideAndDontSave;
        lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMat.SetInt("_ZWrite", 0);
        lineMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
    }

    private void GenerateSeeds()
    {
        int n = Mathf.Max(1, maxLineCount);
        seeds = new LineSeed[n];
        // Use a fixed seed for deterministic pattern
        var rng = new System.Random(1337);
        for (int i = 0; i < n; i++)
        {
            float baseAngle = (i / (float)n) * Mathf.PI * 2f;
            float jitterRad = ((float)rng.NextDouble() - 0.5f) * (Mathf.PI * 2f / n);
            seeds[i] = new LineSeed
            {
                angleOffset = baseAngle + jitterRad,
                lengthScale = 1f + ((float)rng.NextDouble() - 0.5f) * 1f,
                radiusScale = 1f + ((float)rng.NextDouble() - 0.5f) * 0.2f,
            };
        }
    }

    private void Update()
    {
        float speed = 0f;
        if (speedSource != null)
        {
            Vector3 v = speedSource.linearVelocity; v.y = 0f;
            speed = v.magnitude;
        }
        else if (chariotCamera != null)
        {
            speed = chariotCamera.CurrentSpeed;
        }

        float t = Mathf.InverseLerp(activationSpeed, maxSpeed, speed);
        intensity = Mathf.Lerp(intensity, t, intensityLerpSpeed * Time.deltaTime);
        patternAngle += rotationSpeed * intensity * Time.deltaTime;
    }

    private void OnPostRender()
    {
        if (lineMat == null || seeds == null) return;
        if (intensity <= 0.001f) return;

        int lineCount = Mathf.Clamp(Mathf.RoundToInt(maxLineCount * intensity), 0, seeds.Length);
        if (lineCount <= 0) return;

        float aspect = cam.aspect;
        // Normalize so shorter side = 1 (we'll compress X by aspect when aspect>1)
        // Work in an ortho space [-aspect..aspect] x [-1..1]
        GL.PushMatrix();
        lineMat.SetPass(0);
        GL.LoadOrtho();

        Color c = lineColor;
        float effectiveAlpha = c.a * Mathf.SmoothStep(0f, 1f, intensity);

        GL.Begin(GL.QUADS);
        for (int i = 0; i < lineCount; i++)
        {
            LineSeed s = seeds[i];
            float angle = s.angleOffset + patternAngle;
            float jitterAmt = jitter * ((Mathf.Sin(angle * 12.9f + i * 1.7f) * 0.5f));
            float len = lineLength * Mathf.Lerp(1f, s.lengthScale, jitter) * (0.6f + 0.4f * intensity);
            float r0 = innerRadius * s.radiusScale;
            float r1 = outerRadius * s.radiusScale + len * jitterAmt;

            // Point in [-1..1] normalized space, then map to ortho [0..1]
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 perp = new Vector2(-dir.y, dir.x) * lineThickness * (1f + intensity);

            Vector2 p0 = dir * r0;
            Vector2 p1 = dir * r1;

            // Correct for aspect so "radius" is measured on the shorter axis
            // We're in ortho [0,1]x[0,1]; center is (0.5,0.5); scale radial coords by 0.5.
            Vector2 a0 = ToScreen(p0 - perp, aspect);
            Vector2 a1 = ToScreen(p0 + perp, aspect);
            Vector2 a2 = ToScreen(p1 + perp, aspect);
            Vector2 a3 = ToScreen(p1 - perp, aspect);

            // Fade: transparent at inner end, opaque at outer end
            Color cInner = new Color(c.r, c.g, c.b, 0f);
            Color cOuter = new Color(c.r, c.g, c.b, effectiveAlpha);

            GL.Color(cInner); GL.Vertex3(a0.x, a0.y, 0f);
            GL.Color(cInner); GL.Vertex3(a1.x, a1.y, 0f);
            GL.Color(cOuter); GL.Vertex3(a2.x, a2.y, 0f);
            GL.Color(cOuter); GL.Vertex3(a3.x, a3.y, 0f);
        }
        GL.End();
        GL.PopMatrix();
    }

    private static Vector2 ToScreen(Vector2 p, float aspect)
    {
        // p is in "radius units" where 1 == half the shorter screen side.
        // Map so center = (0.5, 0.5), radius 1 fills half of the shorter axis.
        float x = 0.5f + p.x * 0.5f / aspect;
        float y = 0.5f + p.y * 0.5f;
        return new Vector2(x, y);
    }

    private void OnDestroy()
    {
        if (lineMat != null)
        {
            if (Application.isPlaying) Destroy(lineMat);
            else DestroyImmediate(lineMat);
        }
    }
}
