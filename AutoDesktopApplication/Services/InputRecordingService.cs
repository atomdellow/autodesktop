using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json;
using WindowsInput;
using WindowsInput.Native; // For VirtualKeyCode from InputSimulatorCore, aliased if needed
using InputSimVK = WindowsInput.Native.VirtualKeyCode; // Alias for clarity
using AutoDesktopApplication.Models;
using AutoDesktopApplication.Services.WindowsInput;

namespace AutoDesktopApplication.Services
{
    /// <summary>
    /// Service responsible for recording user inputs for automation
    /// </summary>
    public class InputRecordingService : IDisposable
    {
        // Win32 API imports for mouse position and state
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        // Using our custom key codes to avoid conflicts
        private const int VK_LBUTTON = (int)CustomVirtualKeyCode.LBUTTON;  // Left mouse button
        private const int VK_RBUTTON = (int)CustomVirtualKeyCode.RBUTTON;  // Right mouse button
        private const int VK_MBUTTON = (int)CustomVirtualKeyCode.MBUTTON;  // Middle mouse button
        private const int VK_ESCAPE = (int)CustomVirtualKeyCode.ESCAPE;    // Escape key

        // Mouse position structure
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private readonly KeyboardHookService _keyboardHookService;
        private readonly InputLogService _logService;

        // Lists to store recorded actions
        private List<MouseAction> _mouseActions = new List<MouseAction>();
        private List<KeyboardAction> _keyboardActions = new List<KeyboardAction>();

        // Timer to sample mouse position and state - explicitly use System.Threading.Timer to avoid ambiguity
        private System.Threading.Timer _mouseTimer = null!;

        // Tracking state
        private bool _isRecording;
        private Stopwatch _stopwatch;
        private int _lastMouseX;
        private int _lastMouseY;
        private bool _lastLeftDown;
        private bool _lastRightDown;
        private bool _lastMiddleDown;

        // Enum to track the current type of action being recorded for grouping
        private enum CurrentActionType { None, Mouse, Keyboard }
        private CurrentActionType _currentRecordingActionType = CurrentActionType.None;

        // List to store groups of actions. Each group will become a TaskBot.
        private class ActionGroup
        {
            public CurrentActionType Type { get; set; }
            public List<MouseAction> MouseActions { get; set; } = new List<MouseAction>();
            public List<KeyboardAction> KeyboardActions { get; set; } = new List<KeyboardAction>();
            public long StartTimeMs { get; set; }
            public long EndTimeMs { get; set; }
        }
        private List<ActionGroup> _recordedActionGroups = new List<ActionGroup>();

        /// <summary>
        /// Constructor that initializes keyboard hook service
        /// </summary>
        public InputRecordingService(KeyboardHookService keyboardHookService, InputLogService logService)
        {
            _keyboardHookService = keyboardHookService;
            _logService = logService;
            _stopwatch = new Stopwatch();
            _mouseActions = new List<MouseAction>();
            _keyboardActions = new List<KeyboardAction>();
        }

        /// <summary>
        /// Start recording user inputs
        /// </summary>
        public void StartRecording()
        {
            if (_isRecording)
                return;

            _isRecording = true;
            _mouseActions.Clear(); // Clear actions from previous recordings
            _keyboardActions.Clear(); // Clear actions from previous recordings
            _recordedActionGroups.Clear(); // Clear action groups from previous recordings
            _currentRecordingActionType = CurrentActionType.None; // Reset current action type

            // Start tracking elapsed time
            _stopwatch.Reset();
            _stopwatch.Start();

            // Hook keyboard events - match updated delegate signatures
            _keyboardHookService.KeyDown += OnKeyDown;
            _keyboardHookService.KeyUp += OnKeyUp;
            _keyboardHookService.StartHook();

            // Get initial mouse state
            GetCursorPos(out POINT initialPos);
            _lastMouseX = initialPos.X;
            _lastMouseY = initialPos.Y;
            _lastLeftDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
            _lastRightDown = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
            _lastMiddleDown = (GetAsyncKeyState(VK_MBUTTON) & 0x8000) != 0;

            // Start mouse polling timer (30ms interval = ~33 fps)
            _mouseTimer = new System.Threading.Timer(PollMouseState, null, 0, 30);

            Debug.WriteLine("Recording started");
        }

        /// <summary>
        /// Stop recording user inputs
        /// </summary>
        public RecordedInputData StopRecording()
        {
            if (!_isRecording)
                return new RecordedInputData();

            // Stop mouse polling
            _mouseTimer?.Dispose();
            _mouseTimer = null!;

            // Unhook keyboard events
            _keyboardHookService.KeyDown -= OnKeyDown;
            _keyboardHookService.KeyUp -= OnKeyUp;
            _keyboardHookService.StopHook();

            // Stop tracking time
            _stopwatch.Stop();
            _isRecording = false;

            // Create and return the recorded data
            var recordedData = new RecordedInputData
            {
                MouseData = new MouseMovementData { Actions = _mouseActions },
                KeyboardData = new KeyboardInputData { Actions = _keyboardActions },
                TotalDurationMs = (int)_stopwatch.ElapsedMilliseconds
            };

            Debug.WriteLine($"Recording stopped. {_mouseActions.Count} mouse actions and {_keyboardActions.Count} keyboard actions recorded.");

            return recordedData;
        }

        /// <summary>
        /// Stop recording and save workflow
        /// </summary>
        public Workflow StopRecordingAndSaveWorkflow(string workflowName, string description, int projectId)
        {
            _logService.LogInfo($"--- StopRecordingAndSaveWorkflow START --- Name: {workflowName}, ProjectID: {projectId}");
            Workflow workflow = new Workflow
            {
                Name = workflowName,
                Description = description,
                ProjectId = projectId,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow,
                TaskBots = new List<TaskBot>()
            };

            try
            {
                if (_isRecording)
                {
                    System.Threading.Thread.Sleep(100); // Brief pause to capture final inputs, slightly increased

                    _isRecording = false;
                    _keyboardHookService.StopHook();
                    _mouseTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                    _mouseTimer?.Dispose();
                    _mouseTimer = null!;
                    _stopwatch.Stop();
                    _logService.LogInfo("Recording hooks and timers stopped.");
                }
                else
                {
                    _logService.LogInfo("StopRecordingAndSaveWorkflow: Recording was already stopped.");
                }

                _logService.LogInfo("Finalizing current action list into a group (called from StopRecordingAndSaveWorkflow).");
                FinalizeCurrentActionListAsGroup();

                int taskBotSequenceOrder = 1; // Initialize sequence order starting from 1

                _logService.LogInfo($"Starting TaskBot creation. Examining {_recordedActionGroups.Count} recorded action groups.");

                for (int i = 0; i < _recordedActionGroups.Count; i++)
                {
                    var group = _recordedActionGroups[i];
                    TaskBot? newTaskBot = null; // Initialize here

                    try
                    {
                        _logService.LogInfo($"Processing group index {i}. Type: {group.Type}. MouseActions: {group.MouseActions?.Count ?? 0}. KeyboardActions: {group.KeyboardActions?.Count ?? 0}. StartTime: {group.StartTimeMs}. EndTime: {group.EndTimeMs}");

                        long delayBefore = (i == 0) ? group.StartTimeMs : group.StartTimeMs - _recordedActionGroups[i - 1].EndTimeMs;
                        long estimatedDuration = group.EndTimeMs - group.StartTimeMs;

                        if (group.Type == CurrentActionType.Mouse && group.MouseActions != null && group.MouseActions.Any())
                        {
                            var mouseMovementData = new MouseMovementData { Actions = group.MouseActions };
                            string serializedMouseData = JsonConvert.SerializeObject(mouseMovementData);
                            newTaskBot = new TaskBot
                            {
                                Name = "Mouse Movement",
                                Description = $"Mouse actions from {TimeSpan.FromMilliseconds(group.StartTimeMs):g} to {TimeSpan.FromMilliseconds(group.EndTimeMs):g}",
                                Type = TaskType.MouseMovement,
                                InputData = serializedMouseData,
                                WorkflowId = workflow.Id,
                                Workflow = workflow,
                                SequenceOrder = taskBotSequenceOrder,
                                DelayBefore = delayBefore,
                                EstimatedDuration = estimatedDuration,
                                CreatedDate = DateTime.UtcNow,
                                ModifiedDate = DateTime.UtcNow
                            };
                            _logService.LogInfo($"Mouse TaskBot created for group index {i}. Actions: {group.MouseActions.Count}. DelayBefore: {newTaskBot.DelayBefore}, Duration: {newTaskBot.EstimatedDuration}");
                        }
                        else if (group.Type == CurrentActionType.Keyboard && group.KeyboardActions != null && group.KeyboardActions.Any())
                        {
                            var keyboardInputData = new KeyboardInputData { Actions = group.KeyboardActions };
                            string serializedKeyboardData = JsonConvert.SerializeObject(keyboardInputData);
                            newTaskBot = new TaskBot
                            {
                                Name = "Keyboard Inputs",
                                Description = $"Keyboard actions from {TimeSpan.FromMilliseconds(group.StartTimeMs):g} to {TimeSpan.FromMilliseconds(group.EndTimeMs):g}",
                                Type = TaskType.KeyboardInput,
                                InputData = serializedKeyboardData,
                                WorkflowId = workflow.Id,
                                Workflow = workflow,
                                SequenceOrder = taskBotSequenceOrder,
                                DelayBefore = delayBefore,
                                EstimatedDuration = estimatedDuration,
                                CreatedDate = DateTime.UtcNow,
                                ModifiedDate = DateTime.UtcNow
                            };
                            _logService.LogInfo($"Keyboard TaskBot created for group index {i}. Actions: {group.KeyboardActions.Count}. DelayBefore: {newTaskBot.DelayBefore}, Duration: {newTaskBot.EstimatedDuration}");
                            // Log details of keyboard actions for diagnostics
                            foreach (var ka in group.KeyboardActions)
                            {
                                string charToLog = ka.IsDirectTextInput ? ka.Key : "N/A";
                                _logService.LogInfo($"  - Key: {ka.Key}, ActionType: {ka.ActionType}, Shift: {ka.Shift}, Ctrl: {ka.Ctrl}, Alt: {ka.Alt}, Win: {ka.Win}, IsKeyDown: {(ka.ActionType == KeyboardActionType.KeyDown)}, Time: {ka.RelativeTimeMs}, DirectInput: {ka.IsDirectTextInput}, Char: '{charToLog}'");
                            }
                        }
                        else
                        {
                            _logService.LogInfo($"Group index {i} (Type: {group.Type}) has no actions or is of an unknown type. Skipping TaskBot creation.");
                        }

                        if (newTaskBot != null)
                        {
                            workflow.TaskBots.Add(newTaskBot);
                            _logService.LogInfo($"TaskBot '{newTaskBot.Name}' (Type: {newTaskBot.Type}) added to workflow with SequenceOrder {newTaskBot.SequenceOrder}. Total TaskBots now: {workflow.TaskBots.Count}.");
                            taskBotSequenceOrder++; // Increment for the next TaskBot
                        }
                    }
                    catch (JsonSerializationException jsonEx)
                    {
                        _logService.LogInfo($"JSON SERIALIZATION ERROR during TaskBot creation for group index {i} (Type: {group.Type}): {jsonEx.Message} - Path: {jsonEx.Path} - LineNumber: {jsonEx.LineNumber} - LinePosition: {jsonEx.LinePosition} - StackTrace: {jsonEx.StackTrace}");
                        // Optionally, create an error TaskBot or skip
                    }
                    catch (Exception ex)
                    {
                        _logService.LogInfo($"CRITICAL ERROR during TaskBot creation for group index {i} (Type: {group.Type}): {ex.Message} - StackTrace: {ex.StackTrace}");
                        // Optionally, create an error TaskBot or skip
                    }
                }

                _mouseActions.Clear();
                _keyboardActions.Clear();
                _recordedActionGroups.Clear();
                _currentRecordingActionType = CurrentActionType.None;
                _stopwatch.Reset();

                _logService.LogInfo($"--- StopRecordingAndSaveWorkflow END --- Workflow '{workflowName}' prepared with {workflow.TaskBots.Count} TaskBots.");
                return workflow;
            }
            catch (Exception ex)
            {
                _logService.LogInfo($"--- StopRecordingAndSaveWorkflow CRITICAL FAILURE --- Unhandled exception: {ex.Message} - StackTrace: {ex.StackTrace}");
                workflow.Description = $"Error during creation: {ex.Message}. Original Description: {description}";
                workflow.TaskBots.Clear(); // Clear any partially added taskbots
                _logService.LogInfo($"Returning the workflow object (possibly with 0 TaskBots) despite the critical failure. Original Name: {workflow.Name}");
                return workflow;
            }
        }

        private void FinalizeCurrentActionListAsGroup()
        {
            if (_currentRecordingActionType == CurrentActionType.Mouse && _mouseActions.Any())
            {
                _logService.LogInfo($"Finalizing Mouse group. Action Count: {_mouseActions.Count}, FirstActionTime: {_mouseActions.First().RelativeTimeMs}, LastActionTime: {_mouseActions.Last().RelativeTimeMs}");
                _recordedActionGroups.Add(new ActionGroup
                {
                    Type = CurrentActionType.Mouse,
                    MouseActions = new List<MouseAction>(_mouseActions),
                    StartTimeMs = _mouseActions.First().RelativeTimeMs,
                    EndTimeMs = _mouseActions.Last().RelativeTimeMs
                });
                _logService.LogInfo($"Added Mouse group. Total groups now: {_recordedActionGroups.Count}");
                _mouseActions.Clear();
            }
            else if (_currentRecordingActionType == CurrentActionType.Keyboard && _keyboardActions.Any())
            {
                _logService.LogInfo($"Finalizing Keyboard group. Action Count: {_keyboardActions.Count}, FirstActionTime: {_keyboardActions.First().RelativeTimeMs}, LastActionTime: {_keyboardActions.Last().RelativeTimeMs}");
                _recordedActionGroups.Add(new ActionGroup
                {
                    Type = CurrentActionType.Keyboard,
                    KeyboardActions = new List<KeyboardAction>(_keyboardActions),
                    StartTimeMs = _keyboardActions.First().RelativeTimeMs,
                    EndTimeMs = _keyboardActions.Last().RelativeTimeMs
                });
                _logService.LogInfo($"Added Keyboard group. Total groups now: {_recordedActionGroups.Count}");
                _keyboardActions.Clear();
            }
            else
            {
                _logService.LogInfo($"FinalizeCurrentActionListAsGroup: No current actions to finalize or lists are empty. CurrentType: {_currentRecordingActionType}, MouseActions: {_mouseActions.Count}, KeyboardActions: {_keyboardActions.Count}");
            }
            _currentRecordingActionType = CurrentActionType.None; // Ready for a new type or end of recording
        }

        private void EnsureCorrectActionType(CurrentActionType newActionType)
        {
            if (_currentRecordingActionType == CurrentActionType.None)
            {
                _currentRecordingActionType = newActionType;
            }
            else if (_currentRecordingActionType != newActionType)
            {
                _logService.LogInfo($"Action type changing from {_currentRecordingActionType} to {newActionType}. Finalizing old group.");
                FinalizeCurrentActionListAsGroup();
                _currentRecordingActionType = newActionType;
            }
        }

        /// <summary>
        /// Poll the mouse state periodically
        /// </summary>
        private void PollMouseState(object? state)
        {
            if (!_isRecording)
                return;

            try
            {
                GetCursorPos(out POINT currentPos);
                bool currentLeftDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
                bool currentRightDown = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
                bool currentMiddleDown = (GetAsyncKeyState(VK_MBUTTON) & 0x8000) != 0;
                long elapsedMsLong = _stopwatch.ElapsedMilliseconds; // Use long for consistency
                int elapsedMs = (int)elapsedMsLong; // Cast to int for RelativeTimeMs

                // Log raw mouse position at a lower frequency or if changed significantly
                if (Math.Abs(currentPos.X - _lastMouseX) > 1 || Math.Abs(currentPos.Y - _lastMouseY) > 1) // Log more frequently for debugging
                {
                     // _logService.LogInfo($"Record: Raw Mouse Poll ({currentPos.X},{currentPos.Y}) Time: {elapsedMs}");
                }

                if (Math.Abs(currentPos.X - _lastMouseX) > 5 || Math.Abs(currentPos.Y - _lastMouseY) > 5)
                {
                    EnsureCorrectActionType(CurrentActionType.Mouse);
                    var action = new MouseAction
                    {
                        X = currentPos.X,
                        Y = currentPos.Y,
                        ActionType = MouseActionType.Move,
                        RelativeTimeMs = elapsedMs
                    };
                    _mouseActions.Add(action);
                    _logService.LogInfo($"Record: MouseMove ({action.X},{action.Y}) Time: {action.RelativeTimeMs}");
                    _lastMouseX = currentPos.X;
                    _lastMouseY = currentPos.Y;
                }

                if (currentLeftDown != _lastLeftDown)
                {
                    EnsureCorrectActionType(CurrentActionType.Mouse);
                    var action = new MouseAction
                    {
                        X = currentPos.X,
                        Y = currentPos.Y,
                        ActionType = currentLeftDown ? MouseActionType.Down : MouseActionType.Up,
                        Button = AutoDesktopApplication.Models.MouseButton.Left,
                        RelativeTimeMs = elapsedMs
                    };
                    _mouseActions.Add(action);
                    _logService.LogInfo($"Record: MouseButton Left {(currentLeftDown ? "Down" : "Up")} ({action.X},{action.Y}) Time: {action.RelativeTimeMs}");
                    _lastLeftDown = currentLeftDown;
                }

                if (currentRightDown != _lastRightDown)
                {
                    EnsureCorrectActionType(CurrentActionType.Mouse);
                     var action = new MouseAction
                    {
                        X = currentPos.X,
                        Y = currentPos.Y,
                        ActionType = currentRightDown ? MouseActionType.Down : MouseActionType.Up,
                        Button = AutoDesktopApplication.Models.MouseButton.Right,
                        RelativeTimeMs = elapsedMs
                    };
                    _mouseActions.Add(action);
                    _logService.LogInfo($"Record: MouseButton Right {(currentRightDown ? "Down" : "Up")} ({action.X},{action.Y}) Time: {action.RelativeTimeMs}");
                    _lastRightDown = currentRightDown;
                }

                if (currentMiddleDown != _lastMiddleDown)
                {
                    EnsureCorrectActionType(CurrentActionType.Mouse);
                    var action = new MouseAction
                    {
                        X = currentPos.X,
                        Y = currentPos.Y,
                        ActionType = currentMiddleDown ? MouseActionType.Down : MouseActionType.Up,
                        Button = AutoDesktopApplication.Models.MouseButton.Middle,
                        RelativeTimeMs = elapsedMs
                    };
                    _mouseActions.Add(action);
                    _logService.LogInfo($"Record: MouseButton Middle {(currentMiddleDown ? "Down" : "Up")} ({action.X},{action.Y}) Time: {action.RelativeTimeMs}");
                    _lastMiddleDown = currentMiddleDown;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in mouse polling: {ex}");
            }
        }

        /// <summary>
        /// Handle key down events - updated to match new delegate signature
        /// </summary>
        private void OnKeyDown(object sender, int vkCode)
        {
            if (!_isRecording)
                return;

            try
            {
                if (Enum.IsDefined(typeof(InputSimVK), vkCode))
                {
                    var keyCodeEnum = (InputSimVK)vkCode;
                    int elapsedMs = (int)_stopwatch.ElapsedMilliseconds;
                    bool shift = (GetAsyncKeyState((int)CustomVirtualKeyCode.SHIFT) & 0x8000) != 0;
                    bool ctrl = (GetAsyncKeyState((int)CustomVirtualKeyCode.CONTROL) & 0x8000) != 0;
                    bool alt = (GetAsyncKeyState((int)CustomVirtualKeyCode.MENU) & 0x8000) != 0;

                    if (IsPrintableKey(keyCodeEnum) && !ctrl && !alt)
                    {
                        _logService.LogKeyEvent($"KeyDown (Printable, no Ctrl/Alt): {keyCodeEnum}. Handled by IsDirectTextInput in OnKeyUp.");
                    }
                    else
                    {
                        EnsureCorrectActionType(CurrentActionType.Keyboard);
                        _keyboardActions.Add(new KeyboardAction
                        {
                            Key = keyCodeEnum.ToString(),
                            ActionType = KeyboardActionType.KeyDown,
                            RelativeTimeMs = elapsedMs,
                            Shift = shift,
                            Ctrl = ctrl,
                            Alt = alt,
                            IsDirectTextInput = false
                        });
                        _logService.LogKeyEvent($"KeyDown (Special/Modifier or Ctrl/Alt + Key): {keyCodeEnum}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in key down handler: {ex}");
            }
        }

        /// <summary>
        /// Handle key up events - updated to match new delegate signature
        /// </summary>
        private void OnKeyUp(object sender, int vkCode)
        {
            if (!_isRecording)
                return;

            try
            {
                if (Enum.IsDefined(typeof(InputSimVK), vkCode))
                {
                    var keyCodeEnum = (InputSimVK)vkCode;
                    int elapsedMs = (int)_stopwatch.ElapsedMilliseconds;
                    bool shift = (GetAsyncKeyState((int)CustomVirtualKeyCode.SHIFT) & 0x8000) != 0;
                    bool ctrl = (GetAsyncKeyState((int)CustomVirtualKeyCode.CONTROL) & 0x8000) != 0;
                    bool alt = (GetAsyncKeyState((int)CustomVirtualKeyCode.MENU) & 0x8000) != 0;

                    if (IsPrintableKey(keyCodeEnum) && !ctrl && !alt)
                    {
                        char? character = ConvertKeyCodeToChar(keyCodeEnum, shift);
                        if (character.HasValue)
                        {
                            EnsureCorrectActionType(CurrentActionType.Keyboard);
                            _keyboardActions.Add(new KeyboardAction
                            {
                                Key = character.Value.ToString(),
                                ActionType = KeyboardActionType.KeyPress,
                                RelativeTimeMs = elapsedMs,
                                Shift = shift, // Use the shift state that was active for ConvertKeyCodeToChar
                                Ctrl = false,  // Explicitly false as per condition
                                Alt = false,   // Explicitly false as per condition
                                IsDirectTextInput = true
                            });
                            _logService.LogKeyEvent($"KeyUp (Printable, no Ctrl/Alt): {keyCodeEnum} -> Char: '{character.Value}' (IsDirectTextInput)");
                        }
                        else // ConvertKeyCodeToChar FAILED for a printable key
                        {
                            _logService.LogInfo($"ConvertKeyCodeToChar failed for printable key {keyCodeEnum} (Shift: {shift}). Recording as raw KeyDown/KeyUp."); // Changed LogWarning to LogInfo
                            EnsureCorrectActionType(CurrentActionType.Keyboard);
                            // Add KeyDown as a fallback
                            _keyboardActions.Add(new KeyboardAction
                            {
                                Key = keyCodeEnum.ToString(),
                                ActionType = KeyboardActionType.KeyDown,
                                RelativeTimeMs = elapsedMs, // Using KeyUp's time for simplicity, or a slightly earlier estimate
                                Shift = shift,
                                Ctrl = false, // As per the outer condition
                                Alt = false,  // As per the outer condition
                                IsDirectTextInput = false
                            });
                            // Add KeyUp as a fallback
                            _keyboardActions.Add(new KeyboardAction
                            {
                                Key = keyCodeEnum.ToString(),
                                ActionType = KeyboardActionType.KeyUp,
                                RelativeTimeMs = elapsedMs,
                                Shift = shift,
                                Ctrl = false, // As per the outer condition
                                Alt = false,  // As per the outer condition
                                IsDirectTextInput = false
                            });
                        }
                    }
                    else // Non-printable, or printable with Ctrl/Alt
                    {
                        EnsureCorrectActionType(CurrentActionType.Keyboard);
                        _keyboardActions.Add(new KeyboardAction
                        {
                            Key = keyCodeEnum.ToString(),
                            ActionType = KeyboardActionType.KeyUp,
                            RelativeTimeMs = elapsedMs,
                            Shift = shift,
                            Ctrl = ctrl,
                            Alt = alt,
                            IsDirectTextInput = false
                        });
                        _logService.LogKeyEvent($"KeyUp (Special/Modifier or Ctrl/Alt + Key): {keyCodeEnum}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in key up handler: {ex}");
            }
        }

        /// <summary>
        /// Determines if a key represents a printable character
        /// </summary>
        private bool IsPrintableKey(InputSimVK key)
        {
            // Letters
            if (key >= InputSimVK.VK_A && key <= InputSimVK.VK_Z)
                return true;

            // Numbers on the main keyboard
            if (key >= InputSimVK.VK_0 && key <= InputSimVK.VK_9)
                return true;

            // Numpad numbers
            if (key >= InputSimVK.NUMPAD0 && key <= InputSimVK.NUMPAD9)
                return true;

            // Common symbols
            return key == InputSimVK.OEM_1 || // semicolon, colon
                   key == InputSimVK.OEM_2 || // slash, question mark
                   key == InputSimVK.OEM_3 || // backtick, tilde
                   key == InputSimVK.OEM_4 || // open bracket, brace
                   key == InputSimVK.OEM_5 || // backslash, pipe
                   key == InputSimVK.OEM_6 || // close bracket, brace
                   key == InputSimVK.OEM_7 || // quote
                   key == InputSimVK.OEM_PLUS || // equals, plus
                   key == InputSimVK.OEM_MINUS || // minus, underscore
                   key == InputSimVK.OEM_COMMA || // comma, less than
                   key == InputSimVK.OEM_PERIOD || // period, greater than
                   key == InputSimVK.SPACE;
        }

        /// <summary>
        /// Converts a virtual key code to its character representation
        /// </summary>
        private char? ConvertKeyCodeToChar(InputSimVK key, bool shift)
        {
            // Letters
            if (key >= InputSimVK.VK_A && key <= InputSimVK.VK_Z)
            {
                char baseChar = (char)('a' + (key - InputSimVK.VK_A));
                return shift ? char.ToUpper(baseChar) : baseChar;
            }

            // Numbers on the main keyboard
            if (key >= InputSimVK.VK_0 && key <= InputSimVK.VK_9)
            {
                if (!shift) return (char)('0' + (key - InputSimVK.VK_0));

                // Shifted number keys
                switch (key)
                {
                    case InputSimVK.VK_0: return ')';
                    case InputSimVK.VK_1: return '!';
                    case InputSimVK.VK_2: return '@';
                    case InputSimVK.VK_3: return '#';
                    case InputSimVK.VK_4: return '$';
                    case InputSimVK.VK_5: return '%';
                    case InputSimVK.VK_6: return '^';
                    case InputSimVK.VK_7: return '&';
                    case InputSimVK.VK_8: return '*';
                    case InputSimVK.VK_9: return '(';
                }
            }

            // Numpad numbers
            if (key >= InputSimVK.NUMPAD0 && key <= InputSimVK.NUMPAD9)
                return (char)('0' + (key - InputSimVK.NUMPAD0));

            // Special keys
            switch (key)
            {
                case InputSimVK.SPACE: return ' ';
                case InputSimVK.OEM_1: return shift ? ':' : ';';
                case InputSimVK.OEM_2: return shift ? '?' : '/';
                case InputSimVK.OEM_3: return shift ? '~' : '`';
                case InputSimVK.OEM_4: return shift ? '{' : '[';
                case InputSimVK.OEM_5: return shift ? '|' : '\\';
                case InputSimVK.OEM_6: return shift ? '}' : ']';
                case InputSimVK.OEM_7: return shift ? '"' : '\'';
                case InputSimVK.OEM_PLUS: return shift ? '+' : '=';
                case InputSimVK.OEM_MINUS: return shift ? '_' : '-';
                case InputSimVK.OEM_COMMA: return shift ? '<' : ',';
                case InputSimVK.OEM_PERIOD: return shift ? '>' : '.';
            }

            return null;
        }

        public void Dispose()
        {
            _mouseTimer?.Dispose();
            _keyboardHookService.StopHook();
        }
    }
}