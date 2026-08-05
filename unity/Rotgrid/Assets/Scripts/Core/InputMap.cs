using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// Thin wrapper over Unity's legacy Input so the rest of the game asks for
    /// actions rather than keys, and so gamepad sticks feed the same paths.
    /// </summary>
    public static class InputMap
    {
        public static bool Enabled = true;
        public static bool Capturing;               // set while rebinding a key
        public static bool Locked;                  // cursor locked to the view

        public static bool Down(Bind b)
        {
            if (!Enabled || Capturing) return false;
            if (Input.GetKey(GameSettings.Key(b))) return true;
            return PadDown(b);
        }

        public static bool Pressed(Bind b)
        {
            if (!Enabled || Capturing) return false;
            if (Input.GetKeyDown(GameSettings.Key(b))) return true;
            return PadPressed(b);
        }

        // Standard gamepad layout. Triggers land on axes rather than buttons on
        // most platforms, so they are read separately.
        static bool PadDown(Bind b)
        {
            switch (b)
            {
                case Bind.Fire: return TriggerAxis("Fire1", KeyCode.JoystickButton7) > 0.4f;
                case Bind.Ads: return TriggerAxis("Fire2", KeyCode.JoystickButton6) > 0.4f;
                case Bind.Jump: return Input.GetKey(KeyCode.JoystickButton0);
                case Bind.Melee: return Input.GetKey(KeyCode.JoystickButton1);
                case Bind.Reload: return Input.GetKey(KeyCode.JoystickButton2);
                case Bind.Interact: return Input.GetKey(KeyCode.JoystickButton2);
                case Bind.Swap: return Input.GetKey(KeyCode.JoystickButton3);
                case Bind.Sprint: return Input.GetKey(KeyCode.JoystickButton9);
                case Bind.Crouch: return Input.GetKey(KeyCode.JoystickButton8);
                default: return false;
            }
        }

        static bool PadPressed(Bind b)
        {
            switch (b)
            {
                case Bind.Jump: return Input.GetKeyDown(KeyCode.JoystickButton0);
                case Bind.Melee: return Input.GetKeyDown(KeyCode.JoystickButton1);
                case Bind.Reload: return Input.GetKeyDown(KeyCode.JoystickButton2);
                case Bind.Interact: return Input.GetKeyDown(KeyCode.JoystickButton2);
                case Bind.Swap: return Input.GetKeyDown(KeyCode.JoystickButton3);
                case Bind.Crouch: return Input.GetKeyDown(KeyCode.JoystickButton8);
                default: return false;
            }
        }

        static float TriggerAxis(string axis, KeyCode fallback)
        {
            float v = 0f;
            try { v = Input.GetAxisRaw(axis); }
            catch (System.Exception) { v = 0f; }
            if (Mathf.Abs(v) > 0.01f) return Mathf.Abs(v);
            return Input.GetKey(fallback) ? 1f : 0f;
        }

        /// <summary>Movement input as (strafe, forward) in the range [-1, 1].</summary>
        public static Vector2 MoveAxis()
        {
            if (!Enabled) return Vector2.zero;
            float x = 0f, y = 0f;
            if (Down(Bind.Forward)) y += 1f;
            if (Down(Bind.Back)) y -= 1f;
            if (Down(Bind.Right)) x += 1f;
            if (Down(Bind.Left)) x -= 1f;

            float px = Deadzone(SafeAxis("Horizontal"));
            float py = Deadzone(SafeAxis("Vertical"));
            x += px; y += py;

            var v = new Vector2(x, y);
            if (v.sqrMagnitude > 1f) v.Normalize();
            return v;
        }

        /// <summary>Look delta in radians for this frame.</summary>
        public static Vector2 LookDelta(float dt)
        {
            if (!Enabled) return Vector2.zero;
            float inv = GameSettings.InvertY ? -1f : 1f;
            float yaw = 0f, pitch = 0f;
            if (Locked)
            {
                yaw = SafeAxis("Mouse X") * GameSettings.MouseSensitivity * 0.12f;
                pitch = SafeAxis("Mouse Y") * GameSettings.MouseSensitivity * 0.12f * inv;
            }
            // Right stick, with a quadratic response for fine aim.
            float rx = Deadzone(SafeAxis("RightStickX"));
            float ry = Deadzone(SafeAxis("RightStickY"));
            yaw += Mathf.Sign(rx) * rx * rx * GameSettings.ControllerSensitivity * dt;
            pitch += -Mathf.Sign(ry) * ry * ry * GameSettings.ControllerSensitivity * dt * inv;
            return new Vector2(yaw, pitch);
        }

        static float Deadzone(float v)
        {
            const float dz = 0.18f;
            if (Mathf.Abs(v) < dz) return 0f;
            return (v - Mathf.Sign(v) * dz) / (1f - dz);
        }

        /// <summary>Axes that may not exist in the project's input settings.</summary>
        static float SafeAxis(string name)
        {
            try { return Input.GetAxisRaw(name); }
            catch (System.Exception) { return 0f; }
        }

        public static void SetCursorLocked(bool locked)
        {
            Locked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
