using UnityEngine;
using UnityEngine.InputSystem;

namespace RallyAyiti.Kart
{
    /// <summary>
    /// Clavier, manette et boutons tactiles. Les boutons à l'écran (On-Screen Button)
    /// simulent la manette : voir le README pour les chemins de contrôle.
    /// Désactivé sur les karts IA ; RaceSetup l'active sur le pilote choisi au menu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerKartInput : MonoBehaviour, IKartInput
    {
        [Tooltip("Accélération automatique, recommandée sur mobile.")]
        [SerializeField] private bool autoAccelerate = true;

        private InputAction _steer, _throttle, _brake, _drift, _item, _horn, _power;

        private void Awake() => EnsureActions();

        private void EnsureActions()
        {
            if (_steer != null) return;

            _steer = new InputAction("Steer", InputActionType.Value, expectedControlType: "Axis");
            _steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            _steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/q").With("Positive", "<Keyboard>/d");
            _steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            _steer.AddCompositeBinding("1DAxis").With("Negative", "<Gamepad>/dpad/left").With("Positive", "<Gamepad>/dpad/right");
            _steer.AddBinding("<Gamepad>/leftStick/x");

            _throttle = new InputAction("Throttle", InputActionType.Button, "<Keyboard>/w");
            _throttle.AddBinding("<Keyboard>/z");
            _throttle.AddBinding("<Keyboard>/upArrow");
            _throttle.AddBinding("<Gamepad>/rightTrigger");

            _brake = new InputAction("Brake", InputActionType.Button, "<Keyboard>/s");
            _brake.AddBinding("<Keyboard>/downArrow");
            _brake.AddBinding("<Gamepad>/leftTrigger");
            _brake.AddBinding("<Gamepad>/buttonWest");

            _drift = new InputAction("Drift", InputActionType.Button, "<Keyboard>/space");
            _drift.AddBinding("<Gamepad>/rightShoulder");
            _drift.AddBinding("<Gamepad>/buttonSouth");

            _item = new InputAction("Item", InputActionType.Button, "<Keyboard>/e");
            _item.AddBinding("<Keyboard>/leftShift");
            _item.AddBinding("<Gamepad>/buttonEast");

            _horn = new InputAction("Horn", InputActionType.Button, "<Keyboard>/h");
            _horn.AddBinding("<Gamepad>/buttonNorth");

            _power = new InputAction("Power", InputActionType.Button, "<Keyboard>/f");
            _power.AddBinding("<Keyboard>/r");
            _power.AddBinding("<Gamepad>/leftShoulder");
        }

        private void OnEnable()
        {
            EnsureActions();
            _steer.Enable(); _throttle.Enable(); _brake.Enable(); _drift.Enable(); _item.Enable(); _horn.Enable(); _power.Enable();
        }

        private void OnDisable()
        {
            if (_steer == null) return;
            _steer.Disable(); _throttle.Disable(); _brake.Disable(); _drift.Disable(); _item.Disable(); _horn.Disable(); _power.Disable();
        }

        private void OnDestroy()
        {
            if (_steer == null) return;
            _steer.Dispose(); _throttle.Dispose(); _brake.Dispose(); _drift.Dispose(); _item.Dispose(); _horn.Dispose(); _power.Dispose();
        }

        [Tooltip("Dérapage automatique : maintenir le braquage à fond fait partir le kart en glissade (comme Beach Buggy).")]
        [SerializeField] private bool autoDrift = true;
        [SerializeField] private float autoDriftHold = 0.35f;

        private float _fullSteerTime;
        private int _autoDriftDir;

        public KartInput ReadInput()
        {
            float steer = Mathf.Clamp(_steer.ReadValue<float>(), -1f, 1f);
            return new KartInput
            {
                Steer = steer,
                Throttle = autoAccelerate || _throttle.IsPressed() ? 1f : 0f,
                Brake = _brake.IsPressed(),
                Drift = _drift.IsPressed() || AutoDrift(steer),
                UseItem = _item.WasPressedThisFrame(),
                Horn = _horn.WasPressedThisFrame(),
                Power = _power.WasPressedThisFrame(),
            };
        }

        /// <summary>Glissade tant que le braquage reste du même côté ; elle s'arrête quand on relâche ou qu'on change de côté.</summary>
        private bool AutoDrift(float steer)
        {
            if (!autoDrift) return false;
            int side = steer > 0.85f ? 1 : steer < -0.85f ? -1 : 0;
            if (_autoDriftDir != 0)
            {
                if (Mathf.Abs(steer) < 0.35f || steer * _autoDriftDir < 0f) _autoDriftDir = 0;
                return _autoDriftDir != 0;
            }
            _fullSteerTime = side != 0 ? _fullSteerTime + Time.deltaTime : 0f;
            if (_fullSteerTime >= autoDriftHold)
            {
                _autoDriftDir = side;
                _fullSteerTime = 0f;
            }
            return _autoDriftDir != 0;
        }
    }
}
