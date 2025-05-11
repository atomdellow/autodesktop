using System;
using System.Collections.Generic;

namespace AutoDesktopApplication.Models
{
    /// <summary>
    /// Base class for all input data types
    /// </summary>
    public abstract class InputData
    {
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Container for recorded input data including both mouse and keyboard events
    /// </summary>
    public class RecordedInputData
    {
        public MouseMovementData MouseData { get; set; } = new MouseMovementData();
        public KeyboardInputData KeyboardData { get; set; } = new KeyboardInputData();
        public int TotalDurationMs { get; set; }
        
        /// <summary>
        /// Deconstructor to support tuple deconstruction syntax
        /// </summary>
        public void Deconstruct(out MouseMovementData mouseData, out KeyboardInputData keyboardData)
        {
            mouseData = MouseData;
            keyboardData = KeyboardData;
        }
    }

    /// <summary>
    /// Represents recorded mouse movement data
    /// </summary>
    public class MouseMovementData : InputData
    {
        public List<MouseAction> Actions { get; set; } = new List<MouseAction>();
    }

    /// <summary>
    /// Represents a single mouse action
    /// </summary>
    public class MouseAction
    {
        public double X { get; set; } // Changed from int to double
        public double Y { get; set; } // Changed from int to double
        public MouseActionType ActionType { get; set; }
        public MouseButton Button { get; set; }
        public int ScrollAmount { get; set; } // Added for wheel actions
        public int RelativeTimeMs { get; set; } // Time since start of recording in milliseconds
    }

    /// <summary>
    /// Represents mouse action types
    /// </summary>
    public enum MouseActionType
    {
        Move,
        Click,
        DoubleClick,
        RightClick,
        Down,
        Up,
        Wheel
    }

    /// <summary>
    /// Represents mouse buttons
    /// </summary>
    public enum MouseButton
    {
        Left,
        Right,
        Middle,
        None
    }

    /// <summary>
    /// Represents recorded keyboard input data
    /// </summary>
    public class KeyboardInputData : InputData
    {
        public List<KeyboardAction> Actions { get; set; } = new List<KeyboardAction>();
    }

    /// <summary>
    /// Represents a keyboard action
    /// </summary>
    public class KeyboardAction
    {
        public required string Key { get; set; }
        public KeyboardActionType ActionType { get; set; }
        public bool Shift { get; set; }
        public bool Ctrl { get; set; }
        public bool Alt { get; set; }
        public bool Win { get; set; }
        public int RelativeTimeMs { get; set; } // Time since start of recording in milliseconds
        public bool IsDirectTextInput { get; set; } // Flag to indicate this is direct text to be typed, not a keycode
    }

    /// <summary>
    /// Represents keyboard action types
    /// </summary>
    public enum KeyboardActionType
    {
        KeyDown,
        KeyUp,
        KeyPress
    }

    /// <summary>
    /// Represents AI decision data
    /// </summary>
    public class AiDecisionData : InputData
    {
        public required string Prompt { get; set; } = string.Empty;
        public required string Response { get; set; } = string.Empty;
        public required string Model { get; set; } = string.Empty;
        public required Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Represents a delay between actions
    /// </summary>
    public class DelayData : InputData
    {
        public int DurationMs { get; set; }
    }

    /// <summary>
    /// Represents a unified input action for playback
    /// </summary>
    public class InputAction
    {
        public InputActionType Type { get; set; }
        public int DelayBeforeAction { get; set; }
        public double X { get; set; } // Changed from int to double
        public double Y { get; set; } // Changed from int to double
        public MouseButton Button { get; set; }
        public int KeyCode { get; set; }
        public int ScrollAmount { get; set; }
        public string? Text { get; set; }
    }

    /// <summary>
    /// Represents types of unified input actions for playback
    /// </summary>
    public enum InputActionType
    {
        KeyDown,
        KeyUp,
        KeyPress,
        MouseMove,
        MouseDown,
        MouseUp,
        MouseClick,
        MouseWheel,
        TypeText,
        Wait
    }
}