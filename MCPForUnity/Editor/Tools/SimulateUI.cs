using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Runtime;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Tool for simulating UI interactions in Unity Game view during Play Mode.
    /// Uses the runtime UIInputSimulator queue pattern so that ExecuteEvents calls
    /// happen inside the game Update loop where they actually work.
    /// </summary>
    [McpForUnityTool("simulate_ui", Group = "ui")]
    public static class SimulateUI
    {
        private const string AllActions =
            "list_buttons, click, list_inputs, set_input, list_toggles, set_toggle, " +
            "list_sliders, set_slider, list_dropdowns, set_dropdown, " +
            "mouse_click, mouse_down, mouse_up, mouse_move, mouse_drag, mouse_scroll, " +
            "key_down, key_up, key_press, text_input";

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("Parameters cannot be null.");

            if (!EditorApplication.isPlaying)
                return new ErrorResponse("simulate_ui requires Play Mode. Enter Play Mode first.");

            string action = ParamCoercion.CoerceString(@params["action"], null)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
                return new ErrorResponse("'action' parameter is required. Valid actions: " + AllActions);

            try
            {
                return action switch
                {
                    // Read-only list actions (work fine from editor code)
                    "list_buttons" => ListButtons(),
                    "list_inputs" => ListInputs(),
                    "list_toggles" => ListToggles(),
                    "list_sliders" => ListSliders(),
                    "list_dropdowns" => ListDropdowns(),
                    // UI interaction actions (enqueued via UIInputSimulator)
                    "click" => EnqueueClick(@params),
                    "set_input" => EnqueueSetInput(@params),
                    "set_toggle" => EnqueueSetToggle(@params),
                    "set_slider" => EnqueueSetSlider(@params),
                    "set_dropdown" => EnqueueSetDropdown(@params),
                    // Mouse actions
                    "mouse_click" => EnqueueMouseClick(@params),
                    "mouse_down" => EnqueueMouseDown(@params),
                    "mouse_up" => EnqueueMouseUp(@params),
                    "mouse_move" => EnqueueMouseMove(@params),
                    "mouse_drag" => EnqueueMouseDrag(@params),
                    "mouse_scroll" => EnqueueMouseScroll(@params),
                    // Keyboard actions
                    "key_down" => EnqueueKeyDown(@params),
                    "key_up" => EnqueueKeyUp(@params),
                    "key_press" => EnqueueKeyPress(@params),
                    "text_input" => EnqueueTextInput(@params),
                    _ => new ErrorResponse("Unknown action: '" + action + "'. Valid actions: " + AllActions)
                };
            }
            catch (Exception e)
            {
                McpLog.Error("[SimulateUI] Action '" + action + "' failed: " + e);
                return new ErrorResponse("Internal error processing action '" + action + "': " + e.Message);
            }
        }

        #region List Actions (read-only, direct execution)

        private static object ListButtons()
        {
            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            var results = new List<object>();

            foreach (var btn in buttons)
            {
                if (btn == null || !btn.gameObject.activeInHierarchy) continue;

                string label = "";
                var textComp = btn.GetComponentInChildren<Text>();
                if (textComp != null)
                {
                    label = textComp.text;
                }
                else
                {
                    var tmpComp = btn.GetComponentInChildren<Component>()?.gameObject
                        .GetComponentsInChildren<Component>()
                        .FirstOrDefault(c => c != null && c.GetType().Name.Contains("TextMeshPro"));
                    if (tmpComp != null)
                    {
                        var textProp = tmpComp.GetType().GetProperty("text");
                        if (textProp != null)
                            label = textProp.GetValue(tmpComp) as string ?? "";
                    }
                }

                results.Add(new
                {
                    name = btn.gameObject.name,
                    path = GetGameObjectPath(btn.gameObject),
                    text = label,
                    interactable = btn.interactable,
                    instanceID = btn.gameObject.GetInstanceID()
                });
            }

            return new
            {
                success = true,
                message = "Found " + results.Count + " active button(s).",
                data = new { buttons = results }
            };
        }

        private static object ListInputs()
        {
            var inputs = UnityEngine.Object.FindObjectsByType<InputField>(FindObjectsSortMode.None);
            var results = new List<object>();

            foreach (var input in inputs)
            {
                if (input == null || !input.gameObject.activeInHierarchy) continue;

                results.Add(new
                {
                    name = input.gameObject.name,
                    path = GetGameObjectPath(input.gameObject),
                    text = input.text ?? "",
                    placeholder = GetPlaceholderText(input),
                    interactable = input.interactable,
                    instanceID = input.gameObject.GetInstanceID()
                });
            }

            return new
            {
                success = true,
                message = "Found " + results.Count + " active input field(s).",
                data = new { inputs = results }
            };
        }

        private static object ListToggles()
        {
            var toggles = UnityEngine.Object.FindObjectsByType<Toggle>(FindObjectsSortMode.None);
            var results = new List<object>();

            foreach (var toggle in toggles)
            {
                if (toggle == null || !toggle.gameObject.activeInHierarchy) continue;

                string label = "";
                var labelComp = toggle.GetComponentInChildren<Text>();
                if (labelComp != null && labelComp.gameObject != toggle.graphic?.gameObject)
                    label = labelComp.text;

                results.Add(new
                {
                    name = toggle.gameObject.name,
                    path = GetGameObjectPath(toggle.gameObject),
                    text = label,
                    isOn = toggle.isOn,
                    interactable = toggle.interactable,
                    instanceID = toggle.gameObject.GetInstanceID()
                });
            }

            return new
            {
                success = true,
                message = "Found " + results.Count + " active toggle(s).",
                data = new { toggles = results }
            };
        }

        private static object ListSliders()
        {
            var sliders = UnityEngine.Object.FindObjectsByType<Slider>(FindObjectsSortMode.None);
            var results = new List<object>();

            foreach (var slider in sliders)
            {
                if (slider == null || !slider.gameObject.activeInHierarchy) continue;

                results.Add(new
                {
                    name = slider.gameObject.name,
                    path = GetGameObjectPath(slider.gameObject),
                    value = slider.value,
                    minValue = slider.minValue,
                    maxValue = slider.maxValue,
                    wholeNumbers = slider.wholeNumbers,
                    interactable = slider.interactable,
                    instanceID = slider.gameObject.GetInstanceID()
                });
            }

            return new
            {
                success = true,
                message = "Found " + results.Count + " active slider(s).",
                data = new { sliders = results }
            };
        }

        private static object ListDropdowns()
        {
            var dropdowns = UnityEngine.Object.FindObjectsByType<Dropdown>(FindObjectsSortMode.None);
            var results = new List<object>();

            foreach (var dropdown in dropdowns)
            {
                if (dropdown == null || !dropdown.gameObject.activeInHierarchy) continue;

                var options = dropdown.options.Select((opt, i) => new
                {
                    index = i,
                    text = opt.text
                }).ToList();

                results.Add(new
                {
                    name = dropdown.gameObject.name,
                    path = GetGameObjectPath(dropdown.gameObject),
                    selectedIndex = dropdown.value,
                    selectedText = dropdown.value >= 0 && dropdown.value < dropdown.options.Count
                        ? dropdown.options[dropdown.value].text : "",
                    options,
                    interactable = dropdown.interactable,
                    instanceID = dropdown.gameObject.GetInstanceID()
                });
            }

            return new
            {
                success = true,
                message = "Found " + results.Count + " active dropdown(s).",
                data = new { dropdowns = results }
            };
        }

        #endregion

        #region Enqueued UI Interaction Actions

        private static object EnqueueClick(JObject @params)
        {
            string targetText = ParamCoercion.CoerceString(@params["text"], null);
            string targetName = ParamCoercion.CoerceString(@params["name"], null);
            float? x = ParamCoercion.CoerceFloatNullable(@params["x"]);
            float? y = ParamCoercion.CoerceFloatNullable(@params["y"]);

            if (x.HasValue && y.HasValue)
            {
                UIInputSimulator.EnqueueClickAtPosition(x.Value, y.Value);
                return new
                {
                    success = true,
                    message = "Click at position (" + x.Value + ", " + y.Value + ") enqueued. Will execute next frame."
                };
            }

            if (string.IsNullOrEmpty(targetText) && string.IsNullOrEmpty(targetName))
                return new ErrorResponse("Either 'text' (fuzzy match), 'name' (exact match), or 'x'+'y' (position) is required.");

            if (!string.IsNullOrEmpty(targetName))
            {
                UIInputSimulator.EnqueueClickByName(targetName);
                return new
                {
                    success = true,
                    message = "Click on button '" + targetName + "' enqueued. Will execute next frame."
                };
            }

            UIInputSimulator.EnqueueClickByText(targetText);
            return new
            {
                success = true,
                message = "Click on button with text '" + targetText + "' enqueued. Will execute next frame."
            };
        }

        private static object EnqueueSetInput(JObject @params)
        {
            string targetName = ParamCoercion.CoerceString(@params["name"], null);
            string newText = ParamCoercion.CoerceString(@params["text"], null);

            if (string.IsNullOrEmpty(targetName))
                return new ErrorResponse("'name' parameter is required (exact GameObject name of the InputField).");
            if (newText == null)
                return new ErrorResponse("'text' parameter is required (the text to set).");

            UIInputSimulator.EnqueueSetInput(targetName, newText);
            return new
            {
                success = true,
                message = "Set InputField '" + targetName + "' to '" + newText + "' enqueued. Will execute next frame."
            };
        }

        private static object EnqueueSetToggle(JObject @params)
        {
            string targetName = ParamCoercion.CoerceString(@params["name"], null);
            bool? newValue = ParamCoercion.CoerceBoolNullable(@params["value"]);

            if (string.IsNullOrEmpty(targetName))
                return new ErrorResponse("'name' parameter is required (exact GameObject name of the Toggle).");
            if (!newValue.HasValue)
                return new ErrorResponse("'value' parameter is required (true or false).");

            UIInputSimulator.EnqueueSetToggle(targetName, newValue.Value);
            return new
            {
                success = true,
                message = "Set Toggle '" + targetName + "' to " + newValue.Value + " enqueued. Will execute next frame."
            };
        }

        private static object EnqueueSetSlider(JObject @params)
        {
            string targetName = ParamCoercion.CoerceString(@params["name"], null);
            float? newValue = ParamCoercion.CoerceFloatNullable(@params["value"]);

            if (string.IsNullOrEmpty(targetName))
                return new ErrorResponse("'name' parameter is required (exact GameObject name of the Slider).");
            if (!newValue.HasValue)
                return new ErrorResponse("'value' parameter is required (float value within min/max range).");

            UIInputSimulator.EnqueueSetSlider(targetName, newValue.Value);
            return new
            {
                success = true,
                message = "Set Slider '" + targetName + "' to " + newValue.Value + " enqueued. Will execute next frame."
            };
        }

        private static object EnqueueSetDropdown(JObject @params)
        {
            string targetName = ParamCoercion.CoerceString(@params["name"], null);
            int? newIndex = ParamCoercion.CoerceIntNullable(@params["index"]);

            if (string.IsNullOrEmpty(targetName))
                return new ErrorResponse("'name' parameter is required (exact GameObject name of the Dropdown).");
            if (!newIndex.HasValue)
                return new ErrorResponse("'index' parameter is required (0-based option index).");

            UIInputSimulator.EnqueueSetDropdown(targetName, newIndex.Value);
            return new
            {
                success = true,
                message = "Set Dropdown '" + targetName + "' to index " + newIndex.Value + " enqueued. Will execute next frame."
            };
        }

        #endregion

        #region Mouse Actions

        private static object EnqueueMouseClick(JObject @params)
        {
            float? x = ParamCoercion.CoerceFloatNullable(@params["x"]);
            float? y = ParamCoercion.CoerceFloatNullable(@params["y"]);
            if (!x.HasValue || !y.HasValue)
                return new ErrorResponse("'x' and 'y' screen coordinates are required.");

            int button = ParamCoercion.CoerceInt(@params["button"], 0);
            int clickCount = ParamCoercion.CoerceInt(@params["click_count"], 1);

            UIInputSimulator.EnqueueMouseClick(x.Value, y.Value, button, clickCount);
            return new
            {
                success = true,
                message = "Mouse click at (" + x.Value + ", " + y.Value + ") enqueued (button=" + button + ", clicks=" + clickCount + ")."
            };
        }

        private static object EnqueueMouseDown(JObject @params)
        {
            float? x = ParamCoercion.CoerceFloatNullable(@params["x"]);
            float? y = ParamCoercion.CoerceFloatNullable(@params["y"]);
            if (!x.HasValue || !y.HasValue)
                return new ErrorResponse("'x' and 'y' screen coordinates are required.");

            int button = ParamCoercion.CoerceInt(@params["button"], 0);

            UIInputSimulator.EnqueueMouseDown(x.Value, y.Value, button);
            return new
            {
                success = true,
                message = "Mouse down at (" + x.Value + ", " + y.Value + ") enqueued (button=" + button + ")."
            };
        }

        private static object EnqueueMouseUp(JObject @params)
        {
            float? x = ParamCoercion.CoerceFloatNullable(@params["x"]);
            float? y = ParamCoercion.CoerceFloatNullable(@params["y"]);
            if (!x.HasValue || !y.HasValue)
                return new ErrorResponse("'x' and 'y' screen coordinates are required.");

            int button = ParamCoercion.CoerceInt(@params["button"], 0);

            UIInputSimulator.EnqueueMouseUp(x.Value, y.Value, button);
            return new
            {
                success = true,
                message = "Mouse up at (" + x.Value + ", " + y.Value + ") enqueued (button=" + button + ")."
            };
        }

        private static object EnqueueMouseMove(JObject @params)
        {
            float? x = ParamCoercion.CoerceFloatNullable(@params["x"]);
            float? y = ParamCoercion.CoerceFloatNullable(@params["y"]);
            if (!x.HasValue || !y.HasValue)
                return new ErrorResponse("'x' and 'y' screen coordinates are required.");

            UIInputSimulator.EnqueueMouseMove(x.Value, y.Value);
            return new
            {
                success = true,
                message = "Mouse move to (" + x.Value + ", " + y.Value + ") enqueued."
            };
        }

        private static object EnqueueMouseDrag(JObject @params)
        {
            float? fromX = ParamCoercion.CoerceFloatNullable(@params["from_x"]);
            float? fromY = ParamCoercion.CoerceFloatNullable(@params["from_y"]);
            float? toX = ParamCoercion.CoerceFloatNullable(@params["to_x"]);
            float? toY = ParamCoercion.CoerceFloatNullable(@params["to_y"]);

            if (!fromX.HasValue || !fromY.HasValue || !toX.HasValue || !toY.HasValue)
                return new ErrorResponse("'from_x', 'from_y', 'to_x', 'to_y' are all required.");

            UIInputSimulator.EnqueueMouseDrag(fromX.Value, fromY.Value, toX.Value, toY.Value);
            return new
            {
                success = true,
                message = "Mouse drag from (" + fromX.Value + ", " + fromY.Value + ") to (" + toX.Value + ", " + toY.Value + ") enqueued."
            };
        }

        private static object EnqueueMouseScroll(JObject @params)
        {
            float? x = ParamCoercion.CoerceFloatNullable(@params["x"]);
            float? y = ParamCoercion.CoerceFloatNullable(@params["y"]);
            float? scrollDelta = ParamCoercion.CoerceFloatNullable(@params["scroll_delta"]);

            if (!x.HasValue || !y.HasValue)
                return new ErrorResponse("'x' and 'y' screen coordinates are required.");
            if (!scrollDelta.HasValue)
                return new ErrorResponse("'scroll_delta' is required (positive=up, negative=down).");

            UIInputSimulator.EnqueueMouseScroll(x.Value, y.Value, scrollDelta.Value);
            return new
            {
                success = true,
                message = "Mouse scroll at (" + x.Value + ", " + y.Value + ") delta=" + scrollDelta.Value + " enqueued."
            };
        }

        #endregion

        #region Keyboard Actions

        private static object EnqueueKeyDown(JObject @params)
        {
            string keyStr = ParamCoercion.CoerceString(@params["key"], null);
            if (string.IsNullOrEmpty(keyStr))
                return new ErrorResponse("'key' parameter is required (Unity KeyCode name, e.g. 'Return', 'Space', 'A').");

            if (!TryParseKeyCode(keyStr, out KeyCode key))
                return new ErrorResponse("Invalid KeyCode: '" + keyStr + "'. Use Unity KeyCode names.");

            UIInputSimulator.EnqueueKeyDown(key);
            return new { success = true, message = "Key down '" + key + "' enqueued." };
        }

        private static object EnqueueKeyUp(JObject @params)
        {
            string keyStr = ParamCoercion.CoerceString(@params["key"], null);
            if (string.IsNullOrEmpty(keyStr))
                return new ErrorResponse("'key' parameter is required (Unity KeyCode name).");

            if (!TryParseKeyCode(keyStr, out KeyCode key))
                return new ErrorResponse("Invalid KeyCode: '" + keyStr + "'.");

            UIInputSimulator.EnqueueKeyUp(key);
            return new { success = true, message = "Key up '" + key + "' enqueued." };
        }

        private static object EnqueueKeyPress(JObject @params)
        {
            string keyStr = ParamCoercion.CoerceString(@params["key"], null);
            if (string.IsNullOrEmpty(keyStr))
                return new ErrorResponse("'key' parameter is required (Unity KeyCode name).");

            if (!TryParseKeyCode(keyStr, out KeyCode key))
                return new ErrorResponse("Invalid KeyCode: '" + keyStr + "'.");

            UIInputSimulator.EnqueueKeyPress(key);
            return new { success = true, message = "Key press '" + key + "' enqueued (down + up)." };
        }

        private static object EnqueueTextInput(JObject @params)
        {
            string text = ParamCoercion.CoerceString(@params["text"], null);
            if (string.IsNullOrEmpty(text))
                return new ErrorResponse("'text' parameter is required (text to type into focused input).");

            UIInputSimulator.EnqueueTextInput(text);
            return new { success = true, message = "Text input '" + text + "' enqueued." };
        }

        #endregion

        #region Helpers

        private static bool TryParseKeyCode(string keyStr, out KeyCode key)
        {
            if (Enum.TryParse<KeyCode>(keyStr, true, out key))
                return true;
            key = KeyCode.None;
            return false;
        }

        private static string GetGameObjectPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private static string GetPlaceholderText(InputField input)
        {
            if (input.placeholder == null) return "";
            var textComp = input.placeholder as Text;
            if (textComp != null) return textComp.text;
            return "";
        }

        #endregion
    }
}
