using UnityEngine;
using UnityEngine.InputSystem; 
/// <summary>
/// Builds the entire chariot rig at runtime using Unity primitives.
/// Attach this to an empty GameObject in the scene and press Play.
/// Creates: Ground, Horse Pair, Chariot Body, Drawbar visuals, Camera setup, Debug HUD.
/// </summary>
public class ChariotSetup : MonoBehaviour
{
    [Header("Build Settings")]
    [SerializeField] private bool buildRigAtRuntime = true;

    [Header("Colors")]
    [SerializeField] private Color groundColor = new Color(0.3f, 0.55f, 0.2f);
    [SerializeField] private Color horseColor = new Color(0.55f, 0.35f, 0.2f);
    [SerializeField] private Color chariotColor = new Color(0.6f, 0.5f, 0.25f);
    [SerializeField] private Color wheelColor = new Color(0.35f, 0.25f, 0.15f);
    [SerializeField] private Color yokeColor = new Color(0.45f, 0.3f, 0.15f);
    [SerializeField] private Color obstacleColor = new Color(1f, 0.5f, 0f);

    private void Awake()
    {
        if (buildRigAtRuntime)
        {
            BuildRig();
        }
    }

    private void BuildRig()
    {
        // --- Physics Materials ---
        PhysicsMaterial groundMat = CreatePhysicsMaterial("Ground", 0.6f, 0.6f, PhysicsMaterialCombine.Average);
        PhysicsMaterial hoovesMat = CreatePhysicsMaterial("HorseHooves", 1.2f, 1.4f, PhysicsMaterialCombine.Maximum);
        PhysicsMaterial chariotMat = CreatePhysicsMaterial("ChariotSlide", 0.08f, 0.12f, PhysicsMaterialCombine.Minimum);

        // --- Ground ---
        GameObject ground = CreateGround(groundMat);

        // --- Horse Pair ---
        GameObject horsePair = CreateHorsePair(hoovesMat);

        // --- Chariot Body ---
        GameObject chariotBody = CreateChariotBody(chariotMat, horsePair.GetComponent<Rigidbody>());

        // --- Drawbar visual (parented to chariot, purely decorative) ---
        CreateDrawbarVisual(chariotBody.transform, horsePair.transform);

        // --- Camera ---
        SetupCamera(chariotBody.transform);

        // --- Test obstacles ---
        CreateTestObstacles();

        Debug.Log("[ChariotSetup] Rig built successfully. Use WASD to drive!");
    }

    // ==================== GROUND ====================

    private GameObject CreateGround(PhysicsMaterial mat)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(200f, 1f, 200f);

        // Physics material
        ground.GetComponent<Collider>().material = mat;

        // Visual
        SetColor(ground, groundColor);

        return ground;
    }

    // ==================== HORSE PAIR ====================

    private GameObject CreateHorsePair(PhysicsMaterial mat)
    {
        // Main physics body (invisible - the capsule collider is the physics shape)
        GameObject horsePair = new GameObject("HorsePair");
        horsePair.transform.position = new Vector3(0f, 1f, 3f);

        // Rigidbody
        Rigidbody rb = horsePair.AddComponent<Rigidbody>();
        rb.mass = 400f;
        rb.linearDamping = 2f;
        rb.angularDamping = 8f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Collider (capsule oriented along Z axis for the horse pair)
        CapsuleCollider col = horsePair.AddComponent<CapsuleCollider>();
        col.direction = 2; // Z-axis
        col.radius = 0.7f;
        col.height = 3.0f;
        col.center = new Vector3(0f, 0f, 0f);
        col.material = mat;

        // Horse Controller script
        horsePair.AddComponent<HorseController>();

        // --- Visual children ---
        // Left horse
        GameObject leftHorse = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        leftHorse.name = "LeftHorseVisual";
        leftHorse.transform.SetParent(horsePair.transform, false);
        leftHorse.transform.localPosition = new Vector3(-0.8f, 0.2f, 0.64f);
        leftHorse.transform.localScale = new Vector3(0.6f, 0.8f, 1.8f);
        RemoveCollider(leftHorse);
        SetColor(leftHorse, horseColor);

        // Right horse
        GameObject rightHorse = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        rightHorse.name = "RightHorseVisual";
        rightHorse.transform.SetParent(horsePair.transform, false);
        rightHorse.transform.localPosition = new Vector3(0.8f, 0.2f, 0.64f);
        rightHorse.transform.localScale = new Vector3(0.6f, 0.8f, 1.8f);
        RemoveCollider(rightHorse);
        SetColor(rightHorse, horseColor);

        // Horse heads (small spheres at front)
        CreateHorseHead(leftHorse.transform, "LeftHead");
        CreateHorseHead(rightHorse.transform, "RightHead");

        // Yoke bar connecting the two horses
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
        SetColor(head, horseColor * 0.85f); // Slightly darker
    }

    // ==================== CHARIOT BODY ====================

    private GameObject CreateChariotBody(PhysicsMaterial mat, Rigidbody horseRb)
    {
        GameObject chariot = new GameObject("ChariotBody");
        chariot.transform.position = new Vector3(0f, 0.75f, 0f);

        // Rigidbody
        Rigidbody rb = chariot.AddComponent<Rigidbody>();
        rb.mass = 200f;
        rb.linearDamping = 0.3f;
        rb.angularDamping = 1.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Box collider for the chariot body
        BoxCollider col = chariot.AddComponent<BoxCollider>();
        col.size = new Vector3(2.0f, 0.3f, 2.5f);
        col.center = new Vector3(0f, -0.2f, 0f);
        col.material = mat;

        // Chariot Physics script + joint setup
        ChariotPhysics physics = chariot.AddComponent<ChariotPhysics>();
        physics.SetupJoint(horseRb);

        // --- Visual children ---
        // Platform (floor of the chariot)
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "Platform";
        platform.transform.SetParent(chariot.transform, false);
        platform.transform.localPosition = new Vector3(0f, 0f, 0f);
        platform.transform.localScale = new Vector3(1.8f, 0.15f, 2.2f);
        RemoveCollider(platform);
        SetColor(platform, chariotColor);

        // Left wheel
        CreateWheel(chariot.transform, "LeftWheel", new Vector3(-1.1f, -0.15f, -0.3f));

        // Right wheel
        CreateWheel(chariot.transform, "RightWheel", new Vector3(1.1f, -0.15f, -0.3f));

        // Front rail (U-shaped guard rail)
        CreateFrontRail(chariot.transform);

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

        // Wheel spokes (cross pattern)
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

    private void CreateFrontRail(Transform parent)
    {
        // Front bar
        GameObject front = GameObject.CreatePrimitive(PrimitiveType.Cube);
        front.name = "FrontRail";
        front.transform.SetParent(parent, false);
        front.transform.localPosition = new Vector3(0f, 0.5f, 1.0f);
        front.transform.localScale = new Vector3(1.8f, 0.6f, 0.08f);
        RemoveCollider(front);
        SetColor(front, chariotColor * 0.9f);

        // Left side rail
        GameObject left = GameObject.CreatePrimitive(PrimitiveType.Cube);
        left.name = "LeftRail";
        left.transform.SetParent(parent, false);
        left.transform.localPosition = new Vector3(-0.9f, 0.5f, 0.3f);
        left.transform.localScale = new Vector3(0.08f, 0.6f, 1.5f);
        RemoveCollider(left);
        SetColor(left, chariotColor * 0.9f);

        // Right side rail
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
        // Main drawbar (Deichsel) - connects chariot front to horse rear
        GameObject drawbar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        drawbar.name = "Drawbar";
        drawbar.transform.SetParent(chariotTransform, false);
        drawbar.transform.localPosition = new Vector3(0f, 0.1f, 2.0f);
        drawbar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        drawbar.transform.localScale = new Vector3(0.08f, 1.5f, 0.08f);
        RemoveCollider(drawbar);
        SetColor(drawbar, yokeColor);

        // Left trace (connects to left horse)
        GameObject leftTrace = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        leftTrace.name = "LeftTrace";
        leftTrace.transform.SetParent(chariotTransform, false);
        leftTrace.transform.localPosition = new Vector3(-0.4f, 0.1f, 2.2f);
        leftTrace.transform.localRotation = Quaternion.Euler(85f, 10f, 0f);
        leftTrace.transform.localScale = new Vector3(0.04f, 1.5f, 0.04f);
        RemoveCollider(leftTrace);
        SetColor(leftTrace, yokeColor * 0.8f);

        // Right trace
        GameObject rightTrace = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rightTrace.name = "RightTrace";
        rightTrace.transform.SetParent(chariotTransform, false);
        rightTrace.transform.localPosition = new Vector3(0.4f, 0.1f, 2.2f);
        rightTrace.transform.localRotation = Quaternion.Euler(85f, -10f, 0f);
        rightTrace.transform.localScale = new Vector3(0.04f, 1.5f, 0.04f);
        RemoveCollider(rightTrace);
        SetColor(rightTrace, yokeColor * 0.8f);
    }

    // ==================== CAMERA ====================

    private void SetupCamera(Transform chariotTransform)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("[ChariotSetup] No Main Camera found!");
            return;
        }

        ChariotCamera camScript = mainCam.gameObject.AddComponent<ChariotCamera>();
        camScript.SetTarget(chariotTransform);

        // Position camera behind chariot initially
        mainCam.transform.position = chariotTransform.position + new Vector3(0f, 5f, -8f);
        mainCam.transform.LookAt(chariotTransform);
    }

    // ==================== TEST OBSTACLES ====================

    private void CreateTestObstacles()
    {
        GameObject obstacles = new GameObject("TestObstacles");

        // Create cones/pillars in a slalom pattern
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

            // Add rigidbody so they can be knocked over
            Rigidbody rb = cone.AddComponent<Rigidbody>();
            rb.mass = 5f;
        }

        // Create a circular arena boundary (ring of pillars)
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
            // Create a new material instance with URP Lit shader
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
            Destroy(col);
        }
    }

    // ==================== DEBUG HUD ====================

    private ChariotPhysics chariotPhysicsRef;
    private Rigidbody horseRbRef;
    private Rigidbody chariotRbRef;

    private void Start()
    {
        // Cache references for the HUD
        GameObject horsePair = GameObject.Find("HorsePair");
        GameObject chariotBody = GameObject.Find("ChariotBody");

        if (horsePair != null) horseRbRef = horsePair.GetComponent<Rigidbody>();
        if (chariotBody != null)
        {
            chariotRbRef = chariotBody.GetComponent<Rigidbody>();
            chariotPhysicsRef = chariotBody.GetComponent<ChariotPhysics>();
        }
    }

    private void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;

        GUIStyle headerStyle = new GUIStyle(style);
        headerStyle.fontSize = 16;
        headerStyle.normal.textColor = Color.yellow;

        float x = 15;
        float y = 15;
        float lineHeight = 20;
        float boxWidth = 350;
        float boxHeight = 320;

        GUI.Box(new Rect(10, 10, boxWidth, boxHeight), "");

        GUI.Label(new Rect(x, y, boxWidth, 25), "=== STREITWAGEN DEBUG ===", headerStyle);
        y += lineHeight + 5;

        // Input
        
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            string keys = "";
            if (kb.wKey.isPressed) keys += "W ";
            if (kb.aKey.isPressed) keys += "A ";
            if (kb.sKey.isPressed) keys += "S ";
            if (kb.dKey.isPressed) keys += "D ";
            if (keys == "") keys = "(none)";
            GUI.Label(new Rect(x, y, boxWidth, 25), $"Input: {keys}", style);
            y += lineHeight;
        }

        // Horse data
        GUI.Label(new Rect(x, y, boxWidth, 25), "--- Pferde ---", headerStyle);
        y += lineHeight;
        if (horseRbRef != null)
        {
            GUI.Label(new Rect(x, y, boxWidth, 25), $"Pos: {horseRbRef.position:F2}", style);
            y += lineHeight;
            GUI.Label(new Rect(x, y, boxWidth, 25), $"Vel: {horseRbRef.linearVelocity:F2}", style);
            y += lineHeight;
            float hSpeed = horseRbRef.linearVelocity.magnitude;
            GUI.Label(new Rect(x, y, boxWidth, 25), $"Speed: {hSpeed:F2} m/s", style);
            y += lineHeight;
            GUI.Label(new Rect(x, y, boxWidth, 25), $"Sleeping: {horseRbRef.IsSleeping()} | Kinematic: {horseRbRef.isKinematic}", style);
            y += lineHeight;
            GUI.Label(new Rect(x, y, boxWidth, 25), $"Mass: {horseRbRef.mass} | Drag: {horseRbRef.linearDamping}", style);
            y += lineHeight;
        }
        else
        {
            GUI.Label(new Rect(x, y, boxWidth, 25), "Horse Rigidbody: NOT FOUND!", style);
            y += lineHeight;
        }

        // Chariot data
        GUI.Label(new Rect(x, y, boxWidth, 25), "--- Wagen ---", headerStyle);
        y += lineHeight;
        if (chariotRbRef != null)
        {
            GUI.Label(new Rect(x, y, boxWidth, 25), $"Pos: {chariotRbRef.position:F2}", style);
            y += lineHeight;
            GUI.Label(new Rect(x, y, boxWidth, 25), $"Vel: {chariotRbRef.linearVelocity:F2}", style);
            y += lineHeight;
            float cSpeed = chariotRbRef.linearVelocity.magnitude;
            GUI.Label(new Rect(x, y, boxWidth, 25), $"Speed: {cSpeed:F2} m/s", style);
            y += lineHeight;
            GUI.Label(new Rect(x, y, boxWidth, 25), $"Sleeping: {chariotRbRef.IsSleeping()} | Kinematic: {chariotRbRef.isKinematic}", style);
            y += lineHeight;
        }

        if (chariotPhysicsRef != null)
        {
            GUI.Label(new Rect(x, y, boxWidth, 25), $"Drift Winkel: {chariotPhysicsRef.driftAngle:F1} deg", style);
            y += lineHeight;
            string driftStatus = chariotPhysicsRef.driftAngle > 15f ? "DRIFTING!" :
                                 chariotPhysicsRef.driftAngle > 5f ? "leichter Drift" : "stabil";
            style.normal.textColor = chariotPhysicsRef.driftAngle > 15f ? Color.red :
                                     chariotPhysicsRef.driftAngle > 5f ? Color.yellow : Color.green;
            GUI.Label(new Rect(x, y, boxWidth, 25), $"Status: {driftStatus}", style);
        }
    }
}
