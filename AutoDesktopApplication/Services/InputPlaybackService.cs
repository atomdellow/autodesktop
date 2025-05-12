using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoDesktopApplication.Models;
using WindowsInput;
using WindowsInput.Native;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

#if WINDOWS
using System.Windows;
#endif

namespace AutoDesktopApplication.Services
{
    public class InputPlaybackService
    {
        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);

        const int SM_CXVIRTUALSCREEN = 78;
        const int SM_CYVIRTUALSCREEN = 79;
        const int SM_XVIRTUALSCREEN = 76; // The x-coordinate of the upper-left corner of the virtual screen.
        const int SM_YVIRTUALSCREEN = 77; // The y-coordinate of the upper-left corner of the virtual screen.

        private readonly InputSimulator _simulator;
        private bool _isPlaying = false; // This guards PlayInputSequenceAsync
        private bool _isWorkflowExecutionInProgress = false; // This guards ExecuteWorkflow
        private readonly InputLogService _logService;

        // public event EventHandler<ContinueIterationEventArgs>? ContinueIterationRequested;

        /*
        public class ContinueIterationEventArgs : EventArgs
        {
            private bool _continueIteration = false;

            public void Continue() => _continueIteration = true;
            public void Stop() => _continueIteration = false;
            public bool ShouldContinue => _continueIteration;
        }
        */

        public InputPlaybackService(InputLogService logService)
        {
            _simulator = new InputSimulator();
            _logService = logService;
        }

        public async Task PlayInputSequenceAsync(List<InputAction> actions, CancellationToken cancellationToken = default)
        {
            if (_isPlaying)
            {
                return;
            }

            _isPlaying = true;
            try
            {
                foreach (var action in actions)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await Task.Delay(action.DelayBeforeAction, cancellationToken);
                    System.Diagnostics.Debug.WriteLine($"Playback: Executing Action: {action.Type}, DelayBefore: {action.DelayBeforeAction}ms, X:{action.X}, Y:{action.Y}, KeyCode:{action.KeyCode}, Text:'{action.Text}'");

                    switch (action.Type)
                    {
                        case InputActionType.KeyDown:
                            _logService.LogInfo($"Playback: KeyDown {(VirtualKeyCode)action.KeyCode}");
                            System.Diagnostics.Debug.WriteLine($"Playback: KeyDown {(VirtualKeyCode)action.KeyCode}");
                            _simulator.Keyboard.KeyDown((VirtualKeyCode)action.KeyCode);
                            break;
                        case InputActionType.KeyUp:
                            _logService.LogInfo($"Playback: KeyUp {(VirtualKeyCode)action.KeyCode}");
                            System.Diagnostics.Debug.WriteLine($"Playback: KeyUp {(VirtualKeyCode)action.KeyCode}");
                            _simulator.Keyboard.KeyUp((VirtualKeyCode)action.KeyCode);
                            break;
                        case InputActionType.KeyPress:
                            _logService.LogInfo($"Playback: KeyPress {(VirtualKeyCode)action.KeyCode}");
                            System.Diagnostics.Debug.WriteLine($"Playback: KeyPress {(VirtualKeyCode)action.KeyCode}");
                            _simulator.Keyboard.KeyPress((VirtualKeyCode)action.KeyCode);
                            break;
                        case InputActionType.MouseMove:
                            int virtualScreenXOffset = GetSystemMetrics(SM_XVIRTUALSCREEN);
                            int virtualScreenYOffset = GetSystemMetrics(SM_YVIRTUALSCREEN);
                            int virtualScreenWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
                            int virtualScreenHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);

                            if (virtualScreenWidth > 0 && virtualScreenHeight > 0)
                            {
                                double currentX = (double)action.X - virtualScreenXOffset;
                                double currentY = (double)action.Y - virtualScreenYOffset;

                                double normalizedX = (currentX / virtualScreenWidth) * 65535.0;
                                double normalizedY = (currentY / virtualScreenHeight) * 65535.0;

                                normalizedX = Math.Max(0, Math.Min(normalizedX, 65535.0));
                                normalizedY = Math.Max(0, Math.Min(normalizedY, 65535.0));

                                _simulator.Mouse.MoveMouseToPositionOnVirtualDesktop(normalizedX, normalizedY);
                                _logService.LogInfo($"Playback: MoveMouseToVD (VDesktop L:{virtualScreenXOffset},T:{virtualScreenYOffset} W:{virtualScreenWidth},H:{virtualScreenHeight}) Recorded ({action.X},{action.Y}) -> RelativeToVDOrigin ({currentX},{currentY}) -> Norm ({normalizedX:F2},{normalizedY:F2})");
                                System.Diagnostics.Debug.WriteLine($"Playback: MoveMouseToVD (VDesktop L:{virtualScreenXOffset},T:{virtualScreenYOffset} W:{virtualScreenWidth},H:{virtualScreenHeight}) Recorded ({action.X},{action.Y}) -> RelativeToVDOrigin ({currentX},{currentY}) -> Norm ({normalizedX:F2},{normalizedY:F2})");
                            }
                            else
                            {
                                _simulator.Mouse.MoveMouseTo(action.X, action.Y);
                                _logService.LogError($"Playback: Fallback MoveMouseTo (Virtual screen metrics error). Recorded ({action.X},{action.Y})");
                                System.Diagnostics.Debug.WriteLine($"Playback: Fallback MoveMouseTo (Virtual screen metrics error). Recorded ({action.X},{action.Y})");
                            }
                            break;
                        case InputActionType.MouseDown:
                            _logService.LogInfo($"Playback: MouseDown {action.Button} at ({action.X},{action.Y})");
                            System.Diagnostics.Debug.WriteLine($"Playback: MouseDown {action.Button} at ({action.X},{action.Y})");
                            if (action.Button == Models.MouseButton.Left)
                                _simulator.Mouse.LeftButtonDown();
                            else if (action.Button == Models.MouseButton.Right)
                                _simulator.Mouse.RightButtonDown();
                            break;
                        case InputActionType.MouseUp:
                            _logService.LogInfo($"Playback: MouseUp {action.Button} at ({action.X},{action.Y})");
                            System.Diagnostics.Debug.WriteLine($"Playback: MouseUp {action.Button} at ({action.X},{action.Y})");
                            if (action.Button == Models.MouseButton.Left)
                                _simulator.Mouse.LeftButtonUp();
                            else if (action.Button == Models.MouseButton.Right)
                                _simulator.Mouse.RightButtonUp();
                            break;
                        case InputActionType.MouseWheel:
                            _logService.LogInfo($"Playback: MouseWheel ScrollAmount: {action.ScrollAmount}");
                            System.Diagnostics.Debug.WriteLine($"Playback: MouseWheel ScrollAmount: {action.ScrollAmount} at ({action.X},{action.Y})");
                            _simulator.Mouse.VerticalScroll(action.ScrollAmount);
                            break;
                        case InputActionType.TypeText:
                            _logService.LogInfo($"Playback: TypeText '{action.Text}'");
                            System.Diagnostics.Debug.WriteLine($"Playback: TypeText '{action.Text}'");
                            if (!string.IsNullOrEmpty(action.Text))
                            {
                                await TypeTextIntoFocusedControlAsync(action.Text, cancellationToken);
                            }
                            break;
                    }
                }
            }
            finally
            {
                _isPlaying = false;
            }
        }

        private async Task TypeTextIntoFocusedControlAsync(string text, CancellationToken cancellationToken)
        {
#if WINDOWS
            _logService.LogInfo($"Playback: Attempting TextEntry with text: \'{text}\'");
            try
            {
                _simulator.Keyboard.TextEntry(text);
                _logService.LogInfo($"Playback: TextEntry successful for text: \'{text}\'");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Playback: TextEntry FIRST attempt FAILED for text: \'{text}\'. Error: {ex.ToString()}");
                try
                {
                    _logService.LogInfo($"Playback: Retrying TextEntry with text: \'{text}\'");
                    _simulator.Keyboard.TextEntry(text);
                    _logService.LogInfo($"Playback: TextEntry RETRY successful for text: \'{text}\'");
                    await Task.CompletedTask;
                }
                catch (Exception ex2)
                {
                    _logService.LogError($"Playback: TextEntry RETRY FAILED for text: \'{text}\'. Error: {ex2.ToString()}");
                    await Task.CompletedTask;
                }
            }
#else
            System.Diagnostics.Debug.WriteLine($"Playback (non-Windows): Attempting TextEntry with text: \'{text}\'");
            _simulator.Keyboard.TextEntry(text);
            System.Diagnostics.Debug.WriteLine($"Playback (non-Windows): TextEntry call completed for text: \'{text}\'");
            await Task.CompletedTask;
#endif
        }

        public async Task ExecuteWorkflow(Workflow workflow, CancellationToken cancellationToken = default, IProgress<(int current, int total, string message)>? progress = null)
        {
            if (_isWorkflowExecutionInProgress)
            {
                _logService.LogInfo("ExecuteWorkflow called while another workflow execution is already in progress. Request ignored.");
                return;
            }

            if (workflow?.TaskBots == null || !workflow.TaskBots.Any())
            {
                progress?.Report((0, 0, "Workflow has no tasks."));
                _logService.LogInfo("Workflow has no tasks.");
                return;
            }

            var sortedTasks = workflow.TaskBots.OrderBy(t => t.SequenceOrder).ToList();
            int totalTasks = sortedTasks.Count;
            _logService.LogInfo($"Starting workflow execution: {workflow.Name} with {totalTasks} tasks.");
            progress?.Report((0, totalTasks, "Starting workflow execution..."));

            _isWorkflowExecutionInProgress = true;
            try
            {
                long elapsedWorkflowTimeMs = 0;

                for (int i = 0; i < sortedTasks.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logService.LogInfo("Workflow execution cancelled.");
                        progress?.Report((i, totalTasks, "Workflow execution cancelled."));
                        return;
                    }

                    var taskBot = sortedTasks[i];
                    _logService.LogInfo($"Executing TaskBot {taskBot.SequenceOrder}: {taskBot.Name}, Type: {taskBot.Type}");
                    progress?.Report((i + 1, totalTasks, $"Executing task: {taskBot.Name} (Type: {taskBot.Type})"));

                    List<InputAction> actionsForThisTaskBot = new List<InputAction>();
                    long currentActionIntrinsicDurationMs = 0;

                    try
                    {
                        switch (taskBot.Type)
                        {
                            case TaskType.MouseMovement:
                                MouseMovementData? mouseMovementData = JsonConvert.DeserializeObject<MouseMovementData>(taskBot.InputData);
                                if (mouseMovementData != null && mouseMovementData.Actions != null && mouseMovementData.Actions.Any())
                                {
                                    actionsForThisTaskBot.AddRange(ConvertMouseActionsToInputActions(mouseMovementData.Actions, out long intrinsicDuration));
                                    currentActionIntrinsicDurationMs = intrinsicDuration;
                                    _logService.LogInfo($"TaskBot {taskBot.Name} (MouseMovement) - Intrinsic Duration: {currentActionIntrinsicDurationMs}ms, Actions: {mouseMovementData.Actions.Count}");
                                }
                                else
                                {
                                    _logService.LogError($"Failed to deserialize MouseMovementData or its Actions for TaskBot: {taskBot.Name}");
                                }
                                break;

                            case TaskType.KeyboardInput:
                                KeyboardInputData? keyboardInputData = JsonConvert.DeserializeObject<KeyboardInputData>(taskBot.InputData);
                                if (keyboardInputData != null && keyboardInputData.Actions != null && keyboardInputData.Actions.Any())
                                {
                                    actionsForThisTaskBot.AddRange(ConvertKeyboardActionsToInputActions(keyboardInputData.Actions, out long intrinsicDuration));
                                    currentActionIntrinsicDurationMs = intrinsicDuration;
                                    _logService.LogInfo($"TaskBot {taskBot.Name} (KeyboardInput) - Intrinsic Duration: {currentActionIntrinsicDurationMs}ms, Actions: {keyboardInputData.Actions.Count}");
                                }
                                else
                                {
                                    _logService.LogError($"Failed to deserialize KeyboardInputData or its Actions for TaskBot: {taskBot.Name}");
                                }
                                break;

                            case TaskType.Delay:
                                var delayData = JsonConvert.DeserializeObject<DelayData>(taskBot.InputData);
                                if (delayData != null && delayData.DurationMs > 0)
                                {
                                    _logService.LogInfo($"TaskBot {taskBot.Name}: Adding explicit delay of {delayData.DurationMs}ms.");
                                    actionsForThisTaskBot.Add(new InputAction { Type = InputActionType.Wait, DelayBeforeAction = delayData.DurationMs });
                                    currentActionIntrinsicDurationMs = delayData.DurationMs;
                                }
                                else
                                {
                                    _logService.LogInfo($"TaskBot {taskBot.Name}: Delay task with no or zero duration.");
                                }
                                break;

                            default:
                                _logService.LogInfo($"Skipping task '{taskBot.Name}' of unhandled type: {taskBot.Type}");
                                progress?.Report((i + 1, totalTasks, $"Skipping task '{taskBot.Name}' of unhandled type: {taskBot.Type}"));
                                elapsedWorkflowTimeMs += Math.Max(0, taskBot.DelayBefore) + currentActionIntrinsicDurationMs;
                                continue;
                        }

                        if (cancellationToken.IsCancellationRequested) break;

                        if (actionsForThisTaskBot.Any())
                        {
                            actionsForThisTaskBot[0].DelayBeforeAction += (int)Math.Max(0, taskBot.DelayBefore);
                            _logService.LogInfo($"TaskBot {taskBot.Name}: Total delay before first action: {actionsForThisTaskBot[0].DelayBeforeAction}ms. Intrinsic duration: {currentActionIntrinsicDurationMs}ms.");

                            await PlayInputSequenceAsync(actionsForThisTaskBot, cancellationToken);
                            elapsedWorkflowTimeMs += Math.Max(0, taskBot.DelayBefore) + currentActionIntrinsicDurationMs;
                        }
                        else if (taskBot.Type == TaskType.Delay)
                        {
                            await Task.Delay((int)Math.Max(0, taskBot.DelayBefore), cancellationToken);
                            elapsedWorkflowTimeMs += Math.Max(0, taskBot.DelayBefore) + currentActionIntrinsicDurationMs;
                        }
                        else
                        {
                            _logService.LogInfo($"TaskBot {taskBot.Name}: No actions generated. Advancing time. Applying delay of {Math.Max(0, taskBot.DelayBefore)}ms.");
                            await Task.Delay((int)Math.Max(0, taskBot.DelayBefore), cancellationToken);
                            elapsedWorkflowTimeMs += Math.Max(0, taskBot.DelayBefore) + currentActionIntrinsicDurationMs;
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logService.LogError($"JSON Error in Task '{taskBot.Name}': {jsonEx.Message}. InputData: {taskBot.InputData}");
                        progress?.Report((i + 1, totalTasks, $"Error deserializing task data for {taskBot.Name}: {jsonEx.Message}"));
                        elapsedWorkflowTimeMs += Math.Max(0, taskBot.DelayBefore) + currentActionIntrinsicDurationMs;
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError($"Execution Error in Task '{taskBot.Name}': {ex.Message}");
                        progress?.Report((i + 1, totalTasks, $"Error executing task {taskBot.Name}: {ex.Message}"));
                        elapsedWorkflowTimeMs += Math.Max(0, taskBot.DelayBefore) + currentActionIntrinsicDurationMs;
                    }

                    _logService.LogInfo($"TaskBot {taskBot.Name}: End. ElapsedWorkflowTimeMs updated to: {elapsedWorkflowTimeMs}ms.");

                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logService.LogInfo("Workflow execution cancelled post-task.");
                        progress?.Report((i + 1, totalTasks, "Workflow execution cancelled."));
                        return;
                    }
                }
                _logService.LogInfo($"Workflow execution completed: {workflow.Name}. Final elapsed time: {elapsedWorkflowTimeMs}ms.");
                progress?.Report((totalTasks, totalTasks, "Workflow execution completed."));
            }
            catch (Exception ex)
            {
                _logService.LogError($"Unhandled exception during workflow execution: {workflow.Name}. Error: {ex.ToString()}");
                progress?.Report((0, totalTasks, $"Workflow execution failed: {ex.Message}"));
                // Optionally rethrow or handle more gracefully depending on desired application behavior
            }
            finally
            {
                _isWorkflowExecutionInProgress = false;
                _logService.LogInfo($"ExecuteWorkflow finished. _isWorkflowExecutionInProgress set to false.");
            }
        }

        private List<InputAction> ConvertMouseActionsToInputActions(List<MouseAction> mouseActions, out long totalIntrinsicDurationMs)
        {
            var inputActions = new List<InputAction>();
            totalIntrinsicDurationMs = 0;
            if (mouseActions == null || !mouseActions.Any()) return inputActions;

            var sortedMouseActions = mouseActions.OrderBy(ma => ma.RelativeTimeMs).ToList();

            for (int i = 0; i < sortedMouseActions.Count; i++)
            {
                var mouseAction = sortedMouseActions[i];
                int delayBeforeThisAction;

                if (i == 0)
                {
                    delayBeforeThisAction = 0;
                }
                else
                {
                    delayBeforeThisAction = (int)(mouseAction.RelativeTimeMs - sortedMouseActions[i - 1].RelativeTimeMs);
                }

                if (delayBeforeThisAction < 0)
                {
                    _logService.LogInfo($"Clamping negative inner-task delay: {delayBeforeThisAction}ms to 0 for MouseAction at Time {mouseAction.RelativeTimeMs}. Previous action time: {(i > 0 ? sortedMouseActions[i - 1].RelativeTimeMs : -1)}");
                    delayBeforeThisAction = 0;
                }

                var inputAction = new InputAction
                {
                    Type = mouseAction.ActionType switch
                    {
                        MouseActionType.Move => InputActionType.MouseMove,
                        MouseActionType.Down => InputActionType.MouseDown,
                        MouseActionType.Up => InputActionType.MouseUp,
                        MouseActionType.Wheel => InputActionType.MouseWheel,
                        _ => throw new ArgumentOutOfRangeException(nameof(mouseAction.ActionType), $"Unsupported mouse action type: {mouseAction.ActionType}")
                    },
                    X = mouseAction.X,
                    Y = mouseAction.Y,
                    Button = mouseAction.Button,
                    ScrollAmount = mouseAction.ScrollAmount,
                    KeyCode = 0,
                    Text = null,
                    DelayBeforeAction = delayBeforeThisAction
                };
                inputActions.Add(inputAction);
            }

            if (sortedMouseActions.Any())
            {
                totalIntrinsicDurationMs = sortedMouseActions.Last().RelativeTimeMs - sortedMouseActions.First().RelativeTimeMs;
            }
            if (totalIntrinsicDurationMs < 0) totalIntrinsicDurationMs = 0;

            return inputActions;
        }

        private List<InputAction> ConvertKeyboardActionsToInputActions(List<KeyboardAction> keyboardActions, out long totalIntrinsicDurationMs)
        {
            var inputActions = new List<InputAction>();
            totalIntrinsicDurationMs = 0;
            if (keyboardActions == null || !keyboardActions.Any()) return inputActions;

            var sortedKeyboardActions = keyboardActions.OrderBy(ka => ka.RelativeTimeMs).ToList();

            for (int i = 0; i < sortedKeyboardActions.Count; i++)
            {
                var kbAction = sortedKeyboardActions[i];
                int delayBeforeThisAction;

                if (i == 0)
                {
                    delayBeforeThisAction = 0;
                }
                else
                {
                    delayBeforeThisAction = (int)(kbAction.RelativeTimeMs - sortedKeyboardActions[i - 1].RelativeTimeMs);
                }

                if (delayBeforeThisAction < 0)
                {
                    _logService.LogInfo($"Clamping negative inner-task delay: {delayBeforeThisAction}ms to 0 for KeyboardAction '{kbAction.Key}' at Time {kbAction.RelativeTimeMs}. Previous action time: {(i > 0 ? sortedKeyboardActions[i - 1].RelativeTimeMs : -1)}");
                    delayBeforeThisAction = 0;
                }

                InputActionType type;
                VirtualKeyCode keyCode = VirtualKeyCode.NONAME;
                string? textToType = null;

                if (kbAction.IsDirectTextInput && kbAction.ActionType == KeyboardActionType.KeyPress)
                {
                    type = InputActionType.TypeText;
                    textToType = kbAction.Key;
                }
                else
                {
                    type = kbAction.ActionType switch
                    {
                        KeyboardActionType.KeyDown => InputActionType.KeyDown,
                        KeyboardActionType.KeyUp => InputActionType.KeyUp,
                        KeyboardActionType.KeyPress => InputActionType.KeyPress,
                        _ => throw new ArgumentOutOfRangeException(nameof(kbAction.ActionType), $"Unsupported keyboard action type: {kbAction.ActionType}")
                    };

                    if (!string.IsNullOrEmpty(kbAction.Key))
                    {
                        if (Enum.TryParse<VirtualKeyCode>(kbAction.Key, true, out var parsedCode))
                        {
                            keyCode = parsedCode;
                        }
                        else
                        {
                            _logService.LogError($"Could not parse key: '{kbAction.Key}' to VirtualKeyCode during conversion. Action time: {kbAction.RelativeTimeMs}ms.");
                            if (kbAction.Key.Equals("ControlKey", StringComparison.OrdinalIgnoreCase)) keyCode = VirtualKeyCode.CONTROL;
                            else if (kbAction.Key.Equals("ShiftKey", StringComparison.OrdinalIgnoreCase)) keyCode = VirtualKeyCode.SHIFT;
                        }
                    }
                }

                var inputAction = new InputAction
                {
                    Type = type,
                    X = 0,
                    Y = 0,
                    KeyCode = (int)keyCode,
                    Text = textToType,
                    DelayBeforeAction = delayBeforeThisAction
                };
                inputActions.Add(inputAction);
            }

            if (sortedKeyboardActions.Any())
            {
                totalIntrinsicDurationMs = sortedKeyboardActions.Last().RelativeTimeMs - sortedKeyboardActions.First().RelativeTimeMs;
            }
            if (totalIntrinsicDurationMs < 0) totalIntrinsicDurationMs = 0;

            return inputActions;
        }
    }
}