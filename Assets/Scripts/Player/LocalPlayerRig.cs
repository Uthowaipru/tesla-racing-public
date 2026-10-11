using UnityEngine;

namespace VehicleSystem
{
    /// <summary>
    /// Everything that belongs to the person holding THIS device: their controls, camera, touch buttons,
    /// pause menu, hotkey list and reset counter. It is the single place that connects those parts to a car,
    /// so no other script has to search the scene for "the" vehicle, camera or input.
    ///
    ///   Attach(car)  : the car is driven by this player's input, followed by this player's camera,
    ///                  and its recovery feeds this player's reset counter. Call it when the car spawns.
    ///   Detach()     : releases the car (it holds still).
    ///
    /// Remote / AI cars never get a rig, so they can no longer be steered by your keyboard or grab your camera.
    ///
    /// SINGLE-PLAYER / EXISTING SCENE: add this component to an empty GameObject. With "Find Scene Objects" on
    /// (default) it picks up the LocalPlayerInput, VehicleCameraRig, MobileControls, PauseMenu, ControlsOverlay
    /// and ResetCounterUI that are already in the scene (once, in Awake), creates the ones that are missing,
    /// and attaches the first VehicleController it finds.
    ///
    /// MULTIPLAYER: make this a prefab with its parts assigned, switch off "Find Scene Objects" and
    /// "Auto Attach Scene Vehicle", and let your spawner call Attach() for the local player's car only.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class LocalPlayerRig : MonoBehaviour
    {
        [Header("Parts (found or created if empty)")]
        public LocalPlayerInput input;
        public VehicleCameraRig cameraRig;
        [Tooltip("Optional. Without it there are no on-screen touch buttons.")]
        public MobileControls mobile;
        public PauseMenu pauseMenu;
        public ControlsOverlay controlsOverlay;
        public ResetCounterUI resetCounter;
        public Speedometer speedometer;

        [Header("Setup")]
        [Tooltip("ON (single player / existing scenes): empty parts above are searched for in the scene once, at startup. " +
                 "OFF (prefab used for multiplayer): only the parts assigned here are used.")]
        public bool findSceneObjects = true;
        [Tooltip("Create the part if it is still missing after the search.")]
        public bool createControlsOverlay = true;
        public bool createPauseMenu = true;
        public bool createResetCounter = true;
        public bool createSpeedometer = true;

        [Header("Vehicle")]
        [Tooltip("Attached on Start. Leave empty to attach nothing (a spawner calls Attach later).")]
        public VehicleController startVehicle;
        [Tooltip("If Start Vehicle is empty, attach the first VehicleController in the scene. Single player only.")]
        public bool autoAttachSceneVehicle = true;

        /// <summary>The car this player is driving (null before Attach).</summary>
        public VehicleController Vehicle { get; private set; }

        VehicleDriver driver;
        VehicleRecovery recovery;

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            if (input == null) input = GetComponent<LocalPlayerInput>();

            if (findSceneObjects)
            {
                if (input == null) input = FindFirstObjectByType<LocalPlayerInput>();
                if (cameraRig == null) cameraRig = FindFirstObjectByType<VehicleCameraRig>();
                if (mobile == null) mobile = FindFirstObjectByType<MobileControls>();
                if (pauseMenu == null) pauseMenu = FindFirstObjectByType<PauseMenu>();
                if (controlsOverlay == null) controlsOverlay = FindFirstObjectByType<ControlsOverlay>();
                if (resetCounter == null) resetCounter = FindFirstObjectByType<ResetCounterUI>();
                if (speedometer == null) speedometer = FindFirstObjectByType<Speedometer>();
            }

            if (input == null) input = gameObject.AddComponent<LocalPlayerInput>();
            if (cameraRig == null) cameraRig = new GameObject("Vehicle Camera Rig").AddComponent<VehicleCameraRig>();

            if (createControlsOverlay && controlsOverlay == null)
                controlsOverlay = new GameObject("Controls Overlay").AddComponent<ControlsOverlay>();
            if (createPauseMenu && pauseMenu == null)
                pauseMenu = new GameObject("Pause Menu").AddComponent<PauseMenu>();
            if (createResetCounter && resetCounter == null)
                resetCounter = new GameObject("Reset Counter").AddComponent<ResetCounterUI>();
            if (createSpeedometer && speedometer == null)
                speedometer = new GameObject("Speedometer").AddComponent<Speedometer>();

            // hand the parts to each other, so none of them has to search
            if (input.mobile == null) input.mobile = mobile;
            if (controlsOverlay != null) controlsOverlay.Bind(input, cameraRig);
            if (pauseMenu != null) pauseMenu.Bind(mobile, input);
        }

        void Start()
        {
            if (Vehicle != null) return; // someone already called Attach()

            if (startVehicle == null && autoAttachSceneVehicle)
                startVehicle = FindFirstObjectByType<VehicleController>();

            if (startVehicle != null) Attach(startVehicle);
        }

        void OnDestroy() => Detach();

        // ------------------------------------------------------------------ attach / detach

        /// <summary>Make this player drive, follow and monitor the given car.</summary>
        public void Attach(VehicleController car)
        {
            if (car == null) { Detach(); return; }
            if (car == Vehicle) return;

            Detach();
            Vehicle = car;

            if (!car.TryGetComponent(out driver)) driver = car.gameObject.AddComponent<VehicleDriver>();
            driver.SetSource(input);

            car.TryGetComponent(out recovery);
            if (recovery != null) recovery.cameraRig = cameraRig;

            cameraRig.SetTarget(car);
            if (resetCounter != null) resetCounter.SetRecovery(recovery);
            if (speedometer != null) speedometer.SetVehicle(car);

            input.ResetPressed += OnResetPressed;
        }

        /// <summary>Stop driving the current car. It holds still until another source drives it.</summary>
        public void Detach()
        {
            if (Vehicle == null) return;

            if (input != null) input.ResetPressed -= OnResetPressed;
            if (driver != null && ReferenceEquals(driver.Source, input)) driver.SetSource(null);
            if (recovery != null && recovery.cameraRig == cameraRig) recovery.cameraRig = null;
            if (resetCounter != null) resetCounter.SetRecovery(null);
            if (speedometer != null) speedometer.SetVehicle(null);

            Vehicle = null;
            driver = null;
            recovery = null;
        }

        void OnResetPressed()
        {
            if (recovery != null) recovery.RecoverManual();
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            if (cameraRig != null && input != null) cameraRig.SetLookBack(input.LookBack);
        }
    }
}
