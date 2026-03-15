using System;
using System.Collections.Generic;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Tool for simulating UI interactions in Unity Game view during Play Mode.
    /// Supports listing and interacting with Button, InputField, Toggle, Slider, and Dropdown elements.
    /// </summary>
    [McpForUnityTool("simulate_ui", Group = "ui")]
    public static class SimulateUI
    {
        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
            {
                return new ErrorResponse("Parameters cannot be null.");
            }

            if (!EditorApplication.isPlaying)
            {
                return new ErrorResponse("simulate_ui requires Play Mode. Enter Play Mode first.");
            }

            string action = ParamCoercion.CoerceString(@params["action"], null)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
            {
                return new ErrorResponse(
                    "'action' parameter is required. Valid actions: list_buttons, click, " +
                    "list_inputs, set_input, list_toggles, set_toggle, list_sliders, set_slider, " +
                    "list_dropdowns, set_dropdown");
            }

            try
            {
                switch (action)
                {
                    case "list_buttons": return ListButtons();
                    case "click": return ClickButton(@params);
                    case "list_inputs": return ListInputs();
                    case "set_input": return SetInput(@params);
                    case "list_toggles": return ListToggles();
                    case "set_toggle": return SetToggle(@params);
                    case "list_sliders": return ListSliders();
                    case "set_slider": return SetSlider(@params);
                    case "list_dropdowns": return ListDropdowns();
                    case "set_dropdown": return SetDropdown(@params);
                    default:
                        return new ErrorResponse(
                            "Unknown action: '" + action + "'. Valid actions: list_buttons, click, " +
                            "list_inputs, set_input, list_toggles, set_toggle, list_sliders, set_slider, " +
                            "list_dropdowns, set_dropdown");
                }
            }
            catch (Exception e)
            {
                McpLog.Error("[SimulateUI] Action '" + action + "' failed: " + e);
                return new ErrorResponse("Internal error processing action '" + action + "': " + e.Message);
            }
        }

        #region List Actions

        private static object ListButtons()
        {
            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            var results = new List<object>();

            foreach (var btn in buttons)
            {
                if (btn == null || !btn.gameObject.activeInHierarchy) continue;

                results.Add(new
                {
                    name = btn.gameObject.name,
                    path = GetGameObjectPath(btn.gameObject),
                    text = GetButtonText(btn),
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
                {
                    label = labelComp.text;
                }

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

        #region Interaction Actions

        private static object ClickButton(JObject @params)
        {
            string targetText = ParamCoercion.CoerceString(@params["text"], null);
            string targetName = ParamCoercion.CoerceString(@params["name"], null);

            if (string.IsNullOrEmpty(targetText) && string.IsNullOrEmpty(targetName))
            {
                return new ErrorResponse("Either 'text' (fuzzy match on button label) or 'name' (exact GameObject name) is required.");
            }

            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            Button matched = null;

            foreach (var btn in buttons)
            {
                if (btn == null || !btn.gameObject.activeInHierarchy) continue;

                // Match by exact GameObject name first
                if (!string.IsNullOrEmpty(targetName) && btn.gameObject.name == targetName)
                {
                    matched = btn;
                    break;
                }

                // Match by text content (fuzzy: contains, case-insensitive)
                if (!string.IsNullOrEmpty(targetText))
                {
                    string label = GetButtonText(btn);
                    if (!string.IsNullOrEmpty(label) &&
                        label.IndexOf(targetText, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matched = btn;
                        break;
                    }
                }
            }

            if (matched == null)
            {
                string searchDesc = !string.IsNullOrEmpty(targetName)
                    ? "name=" + targetName
                    : "text=" + targetText;
                return new ErrorResponse("No active button found matching " + searchDesc + ".");
            }

            if (!matched.interactable)
            {
                return new ErrorResponse("Button '" + matched.gameObject.name + "' is not interactable.");
            }

            matched.onClick.Invoke();

            return new
            {
                success = true,
                message = "Clicked button '" + matched.gameObject.name + "'.",
                data = new
                {
                    name = matched.gameObject.name,
                    path = GetGameObjectPath(matched.gameObject),
                    text = GetButtonText(matched)
                }
            };
        }

        private static object SetInput(JObject @params)
        {
            string targetName = ParamCoercion.CoerceString(@params["name"], null);
            string newText = ParamCoercion.CoerceString(@params["text"], null);

            if (string.IsNullOrEmpty(targetName))
            {
                return new ErrorResponse("'name' parameter is required (exact GameObject name of the InputField).");
            }
            if (newText == null)
            {
                return new ErrorResponse("'text' parameter is required (the text to set).");
            }

            var inputs = UnityEngine.Object.FindObjectsByType<InputField>(FindObjectsSortMode.None);
            InputField matched = null;

            foreach (var input in inputs)
            {
                if (input == null || !input.gameObject.activeInHierarchy) continue;
                if (input.gameObject.name == targetName)
                {
                    matched = input;
                    break;
                }
            }

            if (matched == null)
            {
                return new ErrorResponse("No active InputField found with name '" + targetName + "'.");
            }

            if (!matched.interactable)
            {
                return new ErrorResponse("InputField '" + targetName + "' is not interactable.");
            }

            matched.text = newText;
            matched.onValueChanged.Invoke(newText);
            matched.onEndEdit.Invoke(newText);

            return new
            {
                success = true,
                message = "Set InputField '" + targetName + "' text to '" + newText + "'.",
                data = new
                {
                    name = matched.gameObject.name,
                    path = GetGameObjectPath(matched.gameObject),
                    text = matched.text
                }
            };
        }

        private static object SetToggle(JObject @params)
        {
            string targetName = ParamCoercion.CoerceString(@params["name"], null);
            bool? newValue = ParamCoercion.CoerceBoolNullable(@params["value"]);

            if (string.IsNullOrEmpty(targetName))
            {
                return new ErrorResponse("'name' parameter is required (exact GameObject name of the Toggle).");
            }
            if (!newValue.HasValue)
            {
                return new ErrorResponse("'value' parameter is required (true or false).");
            }

            var toggles = UnityEngine.Object.FindObjectsByType<Toggle>(FindObjectsSortMode.None);
            Toggle matched = null;

            foreach (var toggle in toggles)
            {
                if (toggle == null || !toggle.gameObject.activeInHierarchy) continue;
                if (toggle.gameObject.name == targetName)
                {
                    matched = toggle;
                    break;
                }
            }

            if (matched == null)
            {
                return new ErrorResponse("No active Toggle found with name '" + targetName + "'.");
            }

            if (!matched.interactable)
            {
                return new ErrorResponse("Toggle '" + targetName + "' is not interactable.");
            }

            matched.isOn = newValue.Value;

            return new
            {
                success = true,
                message = "Set Toggle '" + targetName + "' to " + newValue.Value + ".",
                data = new
                {
                    name = matched.gameObject.name,
                    path = GetGameObjectPath(matched.gameObject),
                    isOn = matched.isOn
                }
            };
        }

        private static object SetSlider(JObject @params)
        {
            string targetName = ParamCoercion.CoerceString(@params["name"], null);
            float? newValue = ParamCoercion.CoerceFloatNullable(@params["value"]);

            if (string.IsNullOrEmpty(targetName))
            {
                return new ErrorResponse("'name' parameter is required (exact GameObject name of the Slider).");
            }
            if (!newValue.HasValue)
            {
                return new ErrorResponse("'value' parameter is required (float value within min/max range).");
            }

            var sliders = UnityEngine.Object.FindObjectsByType<Slider>(FindObjectsSortMode.None);
            Slider matched = null;

            foreach (var slider in sliders)
            {
                if (slider == null || !slider.gameObject.activeInHierarchy) continue;
                if (slider.gameObject.name == targetName)
                {
                    matched = slider;
                    break;
                }
            }

            if (matched == null)
            {
                return new ErrorResponse("No active Slider found with name '" + targetName + "'.");
            }

            if (!matched.interactable)
            {
                return new ErrorResponse("Slider '" + targetName + "' is not interactable.");
            }

            float clamped = Mathf.Clamp(newValue.Value, matched.minValue, matched.maxValue);
            matched.value = clamped;

            return new
            {
                success = true,
                message = "Set Slider '" + targetName + "' to " + clamped + ".",
                data = new
                {
                    name = matched.gameObject.name,
                    path = GetGameObjectPath(matched.gameObject),
                    value = matched.value,
                    minValue = matched.minValue,
                    maxValue = matched.maxValue
                }
            };
        }

        private static object SetDropdown(JObject @params)
        {
            string targetName = ParamCoercion.CoerceString(@params["name"], null);
            int? newIndex = ParamCoercion.CoerceIntNullable(@params["index"]);

            if (string.IsNullOrEmpty(targetName))
            {
                return new ErrorResponse("'name' parameter is required (exact GameObject name of the Dropdown).");
            }
            if (!newIndex.HasValue)
            {
                return new ErrorResponse("'index' parameter is required (0-based option index).");
            }

            var dropdowns = UnityEngine.Object.FindObjectsByType<Dropdown>(FindObjectsSortMode.None);
            Dropdown matched = null;

            foreach (var dropdown in dropdowns)
            {
                if (dropdown == null || !dropdown.gameObject.activeInHierarchy) continue;
                if (dropdown.gameObject.name == targetName)
                {
                    matched = dropdown;
                    break;
                }
            }

            if (matched == null)
            {
                return new ErrorResponse("No active Dropdown found with name '" + targetName + "'.");
            }

            if (!matched.interactable)
            {
                return new ErrorResponse("Dropdown '" + targetName + "' is not interactable.");
            }

            if (newIndex.Value < 0 || newIndex.Value >= matched.options.Count)
            {
                return new ErrorResponse(
                    "Index " + newIndex.Value + " is out of range. Dropdown '" + targetName +
                    "' has " + matched.options.Count + " options (0-" + (matched.options.Count - 1) + ").");
            }

            matched.value = newIndex.Value;
            matched.onValueChanged.Invoke(newIndex.Value);

            string selectedText = matched.options[newIndex.Value].text;
            return new
            {
                success = true,
                message = "Set Dropdown '" + targetName + "' to index " + newIndex.Value +
                          " ('" + selectedText + "').",
                data = new
                {
                    name = matched.gameObject.name,
                    path = GetGameObjectPath(matched.gameObject),
                    selectedIndex = matched.value,
                    selectedText = matched.options[matched.value].text
                }
            };
        }

        #endregion

        #region Helpers

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

        private static string GetButtonText(Button btn)
        {
            var textComp = btn.GetComponentInChildren<Text>();
            if (textComp != null)
                return textComp.text;

            // Try TextMeshPro via reflection to avoid hard dependency
            var components = btn.GetComponentsInChildren<Component>();
            var tmpComp = components.FirstOrDefault(c => c != null && c.GetType().Name.Contains("TextMeshPro"));
            if (tmpComp != null)
            {
                var textProp = tmpComp.GetType().GetProperty("text");
                if (textProp != null)
                    return textProp.GetValue(tmpComp) as string ?? "";
            }

            return "";
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
