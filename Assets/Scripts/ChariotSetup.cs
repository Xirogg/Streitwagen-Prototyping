using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Builds the entire chariot rig using Unity primitives.
/// Split-screen 2-player setup: Player 1 (left, WASD), Player 2 (right, Arrow Keys).
/// Use the context menu "Build Rig" to generate in Editor, or press Play.
/// </summary>
[ExecuteAlways]
public class ChariotSetup : MonoBehaviour
{
    [Header("Build Settings")]
    [SerializeField] private bool buildRigAtRuntime = true;

    [Header("Player Spacing")]
    [SerializeField] private float playerSeparation = 12f;

    [Header("Colors")]
    [SerializeField] private Color groundColor = new Color(0.3f, 0.55f, 0.2f);
    [SerializeField] private Color horseColor = new Color(0.55f, 0.35f, 0.2f);
    [SerializeField] private Color chariotColorP1 = new Color(0.6f, 0.5f, 0.25f);
    [SerializeField] private Color chariotColorP2 = new Color(0.25f, 0.4f, 0.6f);
    [SerializeField] private Color wheelColor = new Color(0.35f, 0.25f, 0.15f);
    [SerializeField] private Color yokeColor = new Color(0.45f, 0.3f, 0.15f);
    [SerializeField] private Color obstacleColor = new Color(1f, 0.5f, 0f);

    private bool isBuilt = false;

    // References for HUD
    private Rigidbody[] horseRbRefs = new Rigidbody[2];
    private Rigidbody[] chariotRbRefs = new Rigidbody[2];
    private ChariotPhysics[] chariotPhysicsRefs = new ChariotPhysics[2];

    private void Awake()
    {
        if (Application.isPlaying && buildRigAtRuntime && !isBuilt)
        {
            ClearRig();
            BuildRig();
        }
    }

    [ContextMenu("Build Rig")]
    public void BuildRigFromEditor()
    {
        ClearRig();
        BuildRig();
    }

    [ContextMenu("Clear Rig")]
    public void ClearRig()
    {
        string[] rootNames = {
            "Ground", "TestObstacles",
            "HorsePair_P1", "ChariotBody_P1",
            "HorsePair_P2", "ChariotBody_P2",
            // Legacy single-player names
            "HorsePair", "ChariotBody"
        };
        foreach (string name in rootNames)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null) SafeDestroy(obj);
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            SafeDestroy(transform.GetChild(i).gameObject);
        }

        // Clean up cameras
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            ChariotCamera camScript = mainCam.GetComponent<ChariotCamera>();
            if (camScript != null) SafeDestroy(camScript);
            // Reset viewport to full
            mainCam.rect = new Rect(0f, 0f, 1f, 1f);
        }

        // Remove player 2 camera if it exists
        GameObject p2CamObj = GameObject.Find("Camera_P2");
        if (p2CamObj != null) SafeDestroy(p2CamObj);

        isBuilt = false;
        horseRbRefs = new Rigidbody[2];
        chariotRbRefs = new Rigidbody[2];
        chariotPhysicsRefs = new ChariotPhysics[2];
    }

    private void SafeDestroy(Object obj)
    {
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }

    private void BuildRig()
    {
        // --- Physics Materials ---
        PhysicsMaterial groundMat = CreatePhysicsMaterial("Ground", 0.6f, 0.6f, PhysicsMaterialCombine.Average);
        PhysicsMaterial hoovesMat = CreatePhysicsMaterial("HorseHooves", 1.2f, 1.4f, PhysicsMaterialCombine.Maximum);
        PhysicsMaterial chariotMat = CreatePhysicsMaterial("ChariotSlide", 0.08f, 0.12f, PhysicsMaterialCombine.Minimum);

        // --- Ground ---
        CreateGround(groundMat);

        // --- Player 1 (left side, WASD) ---
        float p1X = -playerSeparation / 2f;
        GameObject horsePair1 = CreateHorsePair(hoovesMat, 0, p1X);
        GameObject chariot1 = CreateChariotBody(chariotMat, horsePair1.GetComponent<Rigidbody>(), 0, p1X);
        CreateDrawbarVisual(chariot1.transform, horsePair1.transform);

        // --- Player 2 (right side, Arrow Keys) ---
        float p2X = playerSeparation / 2f;
        GameObject horsePair2 = CreateHorsePair(hoovesMat, 1, p2X);
        GameObject chariot2 = CreateChariotBody(chariotMat, horsePair2.GetComponent<Rigidbody>(), 1, p2X);
        CreateDrawbarVisual(chariot2.transform, horsePair2.transform);

        // --- Split-Screen Cameras ---
        SetupSplitScreenCameras(chariot1.transform, chariot2.transform);

        // --- Test obstacles (shared) ---
        CreateTestObstacles();

        // Cache references for HUD
        horseRbRefs[0] = horsePair1.GetComponent<Rigidbody>();
        horseRbRefs[1] = horsePair2.GetComponent<Rigidbody>();
        chariotRbRefs[0] = chariot1.GetComponent<Rigidbody>();
        chariotRbRefs[1] = chariot2.GetComponent<Rigidbody>();
        chariotPhysicsRefs[0] = chariot1.GetComponent<ChariotPhysics>();
        chariotPhysicsRefs[1] = chariot2.GetComponent<ChariotPhysics>();

        isBuilt = true;
        Debug.Log("[ChariotSetup] Split-screen rig built! P1=WASD, P2=Arrow Keys");
    }

    // ==================== GROUND ====================

    private GameObject CreateGround(PhysicsMaterial mat)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(200f, 1f, 200f);
        ground.GetComponent<Collider>().material = mat;
        SetColor(ground, groundColor);
        return ground;
    }

    // ==================== HORSE PAIR ====================

    private GameObject CreateHorsePair(PhysicsMaterial mat, int playerIndex, float xOffset)
    {
        GameObject horsePair = new GameObject(playerIndex == 0 ? "HorsePair_P1" : "HorsePair_P2");
        horsePair.transform.position = new Vector3(xOffset, 1f, 6f);

        Rigidbody rb = horsePair.AddComponent<Rigidbody>();
        rb.mass = 400f;
        rb.linearDamping = 2f;
        rb.angularDamping = 8f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        CapsuleCollider col = horsePair.AddComponent<CapsuleCollider>();
        col.direction = 2;
        col.radius = 0.7f;
        col.height = 3.0f;
        col.center = Vector3.zero;
        col.material = mat;

        HorseController controller = horsePair.AddComponent<HorseController>();
        controller.SetPlayerIndex(playerIndex);

        // --- Visual children ---
        GameObject leftHorse = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        leftHorse.name = "LeftHorseVisual";
        leftHorse.transform.SetParent(horsePair.transform, false);
        leftHorse.transform.localPosition = new Vector3(-0.8f, 0.2f, 0.64f);
        leftHorse.transform.localScale = new Vector3(0.6f, 0.8f, 1.8f);
        RemoveCollider(leftHorse);
        SetColor(leftHorse, horseColor);

        GameObject rightHorse = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        rightHorse.name = "RightHorseVisual";
        rightHorse.transform.SetParent(horsePair.transform, false);
        rightHorse.transform.localPosition = new Vector3(0.8f, 0.2f, 0.64f);
        rightHorse.transform.localScale = new Vector3(0.6f, 0.8f, 1.8f);
        RemoveCollider(rightHorse);
        SetColor(rightHorse, horseColor);

        CreateHorseHead(leftHorse.transform, "LeftHead");
        CreateHorseHead(rightHorse.transform, "RightHead");

        GameObject yoke = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        yoke.name = "YokeBar";
        yoke.transform.SetParent(horsePair.transform, false);
        yoke.transform.localPosition = new Vector3(0f, 0.2f, 0.2f);
        yoke.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        yoke.transform.localScale = new Vector3(0.1f, 0.9f, 0.1f);
        RemoveCollider(yoke);
        SetColor(yoke, yokeColor);

        return horsePair;
    }

    private void CreateHorseHead(Transform parent, string name)
    {
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = name;
        head.transform.SetParent(parent, false);
        head.transform.localPosition = new Vector3(0f, 0.5f, 0.7f);
        head.transform.localScale = new Vector3(0.6f, 0.5f, 0.7f);
        RemoveCollider(head);
        SetColor(head, horseColor * 0.85f);
    }

    // ==================== CHARIOT BODY ====================

    private GameObject CreateChariotBody(PhysicsMaterial mat, Rigidbody horseRb, int playerIndex, float xOffset)
    {
        GameObject chariot = new GameObject(playerIndex == 0 ? "ChariotBody_P1" : "ChariotBody_P2");
        chariot.transform.position = new Vector3(xOffset, 0.75f, 0f);

        Rigidbody rb = chariot.AddComponent<Rigidbody>();
        rb.mass = 200f;
        rb.linearDamping = 0.3f;
        rb.angularDamping = 1.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        BoxCollider col = chariot.AddComponent<BoxCollider>();
        col.size = new Vector3(2.0f, 0.3f, 2.5f);
        col.center = new Vector3(0f, -0.2f, 0f);
        col.material = mat;

        ChariotPhysics physics = chariot.AddComponent<ChariotPhysics>();
        physics.SetupJoint(horseRb);

        Color chariotColor = playerIndex == 0 ? chariotColorP1 : chariotColorP2;

        // Platform
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "Platform";
        platform.transform.SetParent(chariot.transform, false);
        platform.transform.localPosition = Vector3.zero;
        platform.transform.localScale = new Vector3(1.8f, 0.15f, 2.2f);
        RemoveCollider(platform);
        SetColor(platform, chariotColor);

        CreateWheel(chariot.transform, "LeftWheel", new Vector3(-1.1f, -0.15f, -0.3f));
        CreateWheel(chariot.transform, "RightWheel", new Vector3(1.1f, -0.15f, -0.3f));
        CreateFrontRail(chariot.transform, chariotColor);

        return chariot;
    }

    private void CreateWheel(Transform parent, string name, Vector3 localPos)
    {
        GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wheel.name = name;
        wheel.transform.SetParent(parent, false);
        wheel.transform.localPosition = localPos;
        wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        wheel.transform.localScale = new Vector3(0.8f, 0.08f, 0.8f);
        RemoveCollider(wheel);
        SetColor(wheel, wheelColor);

        GameObject spoke1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spoke1.name = "Spoke1";
        spoke1.transform.SetParent(wheel.transform, false);
        spoke1.transform.localScale = new Vector3(0.05f, 1f, 0.8f);
        RemoveCollider(spoke1);
        SetColor(spoke1, wheelColor * 0.8f);

        GameObject spoke2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spoke2.name = "Spoke2";
        spoke2.transform.SetParent(wheel.transform, false);
        spoke2.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        spoke2.transform.localScale = new Vector3(0.05f, 1f, 0.8f);
        RemoveCollider(spoke2);
        SetColor(spoke2, wheelColor * 0.8f);
    }

    private void CreateFrontRail(Transform parent, Color chariotColor)
    {
        GameObject front = GameObject.CreatePrimitive(PrimitiveType.Cube);
        front.name = "FrontRail";
        front.transform.SetParent(parent, false);
        front.transform.localPosition = new Vector3(0f, 0.5f, 1.0f);
        front.transform.localScale = new Vector3(1.8f, 0.6f, 0.08f);
        RemoveCollider(front);
        SetColor(front, chariotColor * 0.9f);

        GameObject left = GameObject.CreatePrimitive(PrimitiveType.Cube);
        left.name = "LeftRail";
        left.transform.SetParent(parent, false);
        left.transform.localPosition = new Vector3(-0.9f, 0.5f, 0.3f);
        left.transform.localScale = new Vector3(0.08f, 0.6f, 1.5f);
        RemoveCollider(left);
        SetColor(left, chariotColor * 0.9f);

        GameObject right = GameObject.CreatePrimitive(PrimitiveType.Cube);
        right.name = "RightRail";
        right.transform.SetParent(parent, false);
        right.transform.localPosition = new Vector3(0.9f, 0.5f, 0.3f);
        right.transform.localScale = new Vector3(0.08f, 0.6f, 1.5f);
        RemoveCollider(right);
        SetColor(right, chariotColor * 0.9f);
    }

    // ==================== DRAWBAR ====================

    private void CreateDrawbarVisual(Transform chariotTransform, Transform horseTransform)
    {
        GameObject drawbar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        drawbar.name = "Drawbar";
        drawbar.transform.SetParent(chariotTransform, false);
        drawbar.transform.localPosition = new Vector3(0f, 0.1f, 3.5f);
        drawbar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        drawbar.transform.localScale = new Vector3(0.08f, 3.0f, 0.08f);
        RemoveCollider(drawbar);
        SetColor(drawbar, yokeColor);

        GameObject leftTrace = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        leftTrace.name = "LeftTrace";
        leftTrace.transform.SetParent(chariotTransform, false);
        leftTrace.transform.localPosition = new Vector3(-0.4f, 0.1f, 3.7f);
        leftTrace.transform.localRotation = Quaternion.Euler(85f, 5f, 0f);
        leftTrace.transform.localScale = new Vector3(0.04f, 3.0f, 0.04f);
        RemoveCollider(leftTrace);
        SetColor(leftTrace, yokeColor * 0.8f);

        GameObject rightTrace = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rightTrace.name = "RightTrace";
        rightTrace.transform.SetParent(chariotTransform, false);
        rightTrace.transform.localPosition = new Vector3(0.4f, 0.1f, 3.7f);
        rightTrace.transform.localRotation = Quaternion.Euler(85f, -5f, 0f);
        rightTrace.transform.localScale = new Vector3(0.04f, 3.0f, 0.04f);
        RemoveCollider(rightTrace);
        SetColor(rightTrace, yokeColor * 0.8f);
    }

    // ==================== SPLIT-SCREEN CAMERAS ====================

    private void SetupSplitScreenCameras(Transform chariot1, Transform chariot2)
    {
        // --- Player 1 Camera (left half) ---
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("[ChariotSetup] No Main Camera found!");
            return;
        }

        // Use main camera for Player 1
        mainCam.rect = new Rect(0f, 0f, 0.5f, 1f); // Left half
        ChariotCamera cam1Script = mainCam.gameObject.AddComponent<ChariotCamera>();
        cam1Script.SetTarget(chariot1);
        mainCam.transform.position = chariot1.position + new Vector3(0f, 5f, -8f);
        mainCam.transform.LookAt(chariot1);

        // --- Player 2 Camera (right half) ---
        GameObject cam2Obj = new GameObject("Camera_P2");
        Camera cam2 = cam2Obj.AddComponent<Camera>();
        cam2.rect = new Rect(0.5f, 0f, 0.5f, 1f); // Right half
        cam2.depth = 0;
        cam2.fieldOfView = mainCam.fieldOfView;
        cam2.nearClipPlane = mainCam.nearClipPlane;
        cam2.farClipPlane = mainCam.farClipPlane;
        cam2.clearFlags = mainCam.clearFlags;
        cam2.backgroundColor = mainCam.backgroundColor;

        // Copy URP renderer data if available
        var mainCamData = mainCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (mainCamData != null)
        {
            var cam2Data = cam2Obj.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cam2Data.renderType = mainCamData.renderType;
        }

        // Only one AudioListener allowed in scene
        AudioListener listener2 = cam2Obj.GetComponent<AudioListener>();
        if (listener2 != null) SafeDestroy(listener2);

        ChariotCamera cam2Script = cam2Obj.AddComponent<ChariotCamera>();
        cam2Script.SetTarget(chariot2);
        cam2.transform.position = chariot2.position + new Vector3(0f, 5f, -8f);
        cam2.transform.LookAt(chariot2);
    }

    // ==================== TEST OBSTACLES ====================

    private void CreateTestObstacles()
    {
        GameObject obstacles = new GameObject("TestObstacles");

        float[] xPositions = { -3f, 3f, -3f, 3f, -3f, 3f };
        float startZ = 20f;
        float spacing = 15f;

        for (int i = 0; i < xPositions.Length; i++)
        {
            GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cone.name = $"Cone_{i}";
            cone.transform.SetParent(obstacles.transform, false);
            cone.transform.position = new Vector3(xPositions[i], 0.75f, startZ + i * spacing);
            cone.transform.localScale = new Vector3(0.5f, 1.5f, 0.5f);
            SetColor(cone, obstacleColor);

            Rigidbody rb = cone.AddComponent<Rigidbody>();
            rb.mass = 5f;
        }

        int pillarCount = 24;
        float arenaRadius = 40f;
        for (int i = 0; i < pillarCount; i++)
        {
            float angle = (360f / pillarCount) * i * Mathf.Deg2Rad;
            float x = Mathf.Sin(angle) * arenaRadius;
            float z = Mathf.Cos(angle) * arenaRadius;

            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = $"ArenaPillar_{i}";
            pillar.transform.SetParent(obstacles.transform, false);
            pillar.transform.position = new Vector3(x, 1.5f, z);
            pillar.transform.localScale = new Vector3(1f, 3f, 1f);
            SetColor(pillar, new Color(0.5f, 0.5f, 0.5f));
        }
    }

    // ==================== UTILITY ====================

    private PhysicsMaterial CreatePhysicsMaterial(string name, float dynamicFriction, float staticFriction, PhysicsMaterialCombine frictionCombine)
    {
        PhysicsMaterial mat = new PhysicsMaterial(name);
        mat.dynamicFriction = dynamicFriction;
        mat.staticFriction = staticFriction;
        mat.bounciness = 0f;
        mat.frictionCombine = frictionCombine;
        mat.bounceCombine = PhysicsMaterialCombine.Minimum;
        return mat;
    }

    private void SetColor(GameObject obj, Color color)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            renderer.material = material;
        }
    }

    private void RemoveCollider(GameObject obj)
    {
        Collider col = obj.GetComponent<Collider>();
        if (col != null)
        {
            SafeDestroy(col);
        }
    }

    // ==================== DEBUG HUD ====================

    private void Start()
    {
        if (!Application.isPlaying) return;

        // Cache references if not already set by BuildRig
        if (horseRbRefs[0] == null)
        {
            GameObject hp1 = GameObject.Find("HorsePair_P1");
            GameObject hp2 = GameObject.Find("HorsePair_P2");
            GameObject cb1 = GameObject.Find("ChariotBody_P1");
            GameObject cb2 = GameObject.Find("ChariotBody_P2");

            if (hp1 != null) horseRbRefs[0] = hp1.GetComponent<Rigidbody>();
            if (hp2 != null) horseRbRefs[1] = hp2.GetComponent<Rigidbody>();
            if (cb1 != null)
            {
                chariotRbRefs[0] = cb1.GetComponent<Rigidbody>();
                chariotPhysicsRefs[0] = cb1.GetComponent<ChariotPhysics>();
            }
            if (cb2 != null)
            {
                chariotRbRefs[1] = cb2.GetComponent<Rigidbody>();
                chariotPhysicsRefs[1] = cb2.GetComponent<ChariotPhysics>();
            }
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 12;
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;

        GUIStyle headerStyle = new GUIStyle(style);
        headerStyle.fontSize = 14;
        headerStyle.normal.textColor = Color.yellow;

        float screenHalf = Screen.width / 2f;

        for (int p = 0; p < 2; p++)
        {
            float panelX = p == 0 ? 15f : screenHalf + 15f;
            float y = 15f;
            float lineHeight = 18f;
            float boxWidth = Mathf.Min(280f, screenHalf - 30f);
            float boxHeight = 200f;

            string playerLabel = p == 0 ? "P1 (WASD)" : "P2 (Pfeiltasten)";
            Color playerColor = p == 0 ? chariotColorP1 : chariotColorP2;

            GUI.Box(new Rect(panelX - 5f, 10f, boxWidth, boxHeight), "");

            headerStyle.normal.textColor = playerColor;
            GUI.Label(new Rect(panelX, y, boxWidth, 25), $"=== {playerLabel} ===", headerStyle);
            y += lineHeight + 5;

            style.normal.textColor = Color.white;
            if (horseRbRefs[p] != null)
            {
                float hSpeed = horseRbRefs[p].linearVelocity.magnitude;
                GUI.Label(new Rect(panelX, y, boxWidth, 25), $"Pferde Speed: {hSpeed:F2} m/s", style);
                y += lineHeight;
            }

            if (chariotRbRefs[p] != null)
            {
                float cSpeed = chariotRbRefs[p].linearVelocity.magnitude;
                GUI.Label(new Rect(panelX, y, boxWidth, 25), $"Wagen Speed: {cSpeed:F2} m/s", style);
                y += lineHeight;
                GUI.Label(new Rect(panelX, y, boxWidth, 25), $"Pos: {chariotRbRefs[p].position:F1}", style);
                y += lineHeight;
            }

            if (chariotPhysicsRefs[p] != null)
            {
                float drift = chariotPhysicsRefs[p].driftAngle;
                GUI.Label(new Rect(panelX, y, boxWidth, 25), $"Drift: {drift:F1} deg", style);
                y += lineHeight;

                string driftStatus = drift > 15f ? "DRIFTING!" :
                                     drift > 5f ? "leichter Drift" : "stabil";
                style.normal.textColor = drift > 15f ? Color.red :
                                         drift > 5f ? Color.yellow : Color.green;
                GUI.Label(new Rect(panelX, y, boxWidth, 25), $"Status: {driftStatus}", style);
            }
        }
    }
}
