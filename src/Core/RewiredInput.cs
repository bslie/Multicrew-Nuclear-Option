using Rewired;
using UnityEngine;

namespace SimpleWSO.Core
{
    /// <summary>
    /// Input abstraction over Rewired. Nuclear Option drives input through Rewired, and the
    /// legacy UnityEngine.Input class throws / does nothing in this build, so all polling must
    /// go through ReInput. This mirrors how other working NO mods read keys.
    /// </summary>
    public static class RewiredInput
    {
        public static bool Ready => ReInput.isReady && ReInput.controllers != null;

        private static Keyboard Kb => ReInput.controllers != null ? ReInput.controllers.Keyboard : null;
        private static Mouse Ms => ReInput.controllers != null ? ReInput.controllers.Mouse : null;

        public static bool GetKeyDown(KeyCode key)
        {
            if (!Ready) return false;
            if (TryMouseButton(key, out int btn)) { var m = Ms; return m != null && m.GetButtonDown(btn); }
            var kb = Kb; return kb != null && kb.GetKeyDown(key);
        }

        /// <summary>
        /// Poll a named Rewired action the same way vanilla PilotPlayerState does (timed press up).
        /// Used for "Next Weapon" / "Previous Weapon" while in gunner mode.
        /// </summary>
        public static bool GetActionTimedPressUp(string actionName)
        {
            if (!Ready || string.IsNullOrEmpty(actionName)) return false;
            var player = GameManager.playerInput;
            if (player == null) return false;
            return player.GetButtonTimedPressUp(actionName, 0, PlayerSettings.clickDelay);
        }

        public static bool GetAction(string actionName)
        {
            if (!Ready || string.IsNullOrEmpty(actionName)) return false;
            var player = GameManager.playerInput;
            return player != null && player.GetButton(actionName);
        }

        private static bool TryMouseButton(KeyCode key, out int button)
        {
            switch (key)
            {
                case KeyCode.Mouse0: button = 0; return true;
                case KeyCode.Mouse1: button = 1; return true;
                case KeyCode.Mouse2: button = 2; return true;
                case KeyCode.Mouse3: button = 3; return true;
                case KeyCode.Mouse4: button = 4; return true;
                default: button = -1; return false;
            }
        }
    }
}
