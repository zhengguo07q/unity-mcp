using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MCPForUnity.Runtime
{
    /// <summary>
    /// Runtime MonoBehaviour that processes UI/input simulation actions from the game Update loop.
    /// Editor/MCP code cannot directly trigger UGUI events because it runs outside the game
    /// Update loop. This singleton sits as a DontDestroyOnLoad object and dequeues pending
    /// actions each frame, executing them via ExecuteEvents which works correctly from Update().
    /// </summary>
    public class UIInputSimulator : MonoBehaviour
    {
        private static UIInputSimulator _instance;
        private static readonly Queue<Action> _actionQueue = new Queue<Action>();
        private static readonly object _lock = new object();

        public static UIInputSimulator Instance
        {
            get
            {
                EnsureInstance();
                return _instance;
            }
        }

        public static void EnsureInstance()
        {
            if (_instance != null) return;
            _instance = FindObjectOfType<UIInputSimulator>();
            if (_instance != null) return;
            var go = new GameObject("[MCP] UIInputSimulator");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UIInputSimulator>();
        }

        public static bool IsReady => _instance != null;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            while (true)
            {
                Action action;
                lock (_lock)
                {
                    if (_actionQueue.Count == 0) break;
                    action = _actionQueue.Dequeue();
                }
                try { action?.Invoke(); }
                catch (Exception e) { Debug.LogError("[UIInputSimulator] Error executing queued action: " + e); }
            }
        }

        private static void Enqueue(Action action)
        {
            lock (_lock) { _actionQueue.Enqueue(action); }
            EnsureInstance();
        }

        #region UI Click Actions

        public static void EnqueueClickByText(string text)
        {
            Enqueue(() =>
            {
                var button = FindButtonByText(text);
                if (button == null) { Debug.LogWarning("[UIInputSimulator] No active button with text: " + text); return; }
                SimulateButtonClick(button);
            });
        }

        public static void EnqueueClickByName(string name)
        {
            Enqueue(() =>
            {
                var button = FindButtonByName(name);
                if (button == null) { Debug.LogWarning("[UIInputSimulator] No active button with name: " + name); return; }
                SimulateButtonClick(button);
            });
        }

        public static void EnqueueClickAtPosition(float x, float y)
        {
            Enqueue(() => SimulateClickAtPosition(new Vector2(x, y)));
        }

        public static void EnqueueSetInput(string name, string text)
        {
            Enqueue(() =>
            {
                var input = FindComponentByName<InputField>(name);
                if (input == null) { Debug.LogWarning("[UIInputSimulator] No active InputField: " + name); return; }
                input.text = text;
                input.onValueChanged?.Invoke(text);
                input.onEndEdit?.Invoke(text);
            });
        }

        public static void EnqueueSetToggle(string name, bool value)
        {
            Enqueue(() =>
            {
                var toggle = FindComponentByName<Toggle>(name);
                if (toggle == null) { Debug.LogWarning("[UIInputSimulator] No active Toggle: " + name); return; }
                toggle.isOn = value;
            });
        }

        public static void EnqueueSetSlider(string name, float value)
        {
            Enqueue(() =>
            {
                var slider = FindComponentByName<Slider>(name);
                if (slider == null) { Debug.LogWarning("[UIInputSimulator] No active Slider: " + name); return; }
                slider.value = Mathf.Clamp(value, slider.minValue, slider.maxValue);
            });
        }

        public static void EnqueueSetDropdown(string name, int index)
        {
            Enqueue(() =>
            {
                var dropdown = FindComponentByName<Dropdown>(name);
                if (dropdown == null) { Debug.LogWarning("[UIInputSimulator] No active Dropdown: " + name); return; }
                if (index < 0 || index >= dropdown.options.Count)
                {
                    Debug.LogWarning("[UIInputSimulator] Dropdown index " + index + " out of range");
                    return;
                }
                dropdown.value = index;
                dropdown.onValueChanged?.Invoke(index);
            });
        }

        #endregion

        #region Mouse Actions

        public static void EnqueueMouseClick(float x, float y, int button = 0, int clickCount = 1)
        {
            Enqueue(() =>
            {
                var pos = new Vector2(x, y);
                if (EventSystem.current == null) { Debug.LogWarning("[UIInputSimulator] No EventSystem"); return; }
                var target = RaycastUI(pos);
                if (target == null) { Debug.LogWarning("[UIInputSimulator] No UI at (" + x + ", " + y + ")"); return; }
                var pd = CreatePointerEventData(pos, button, clickCount);
                ExecuteEvents.Execute(target, pd, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(target, pd, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(target, pd, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(target, pd, ExecuteEvents.pointerClickHandler);
            });
        }

        public static void EnqueueMouseDown(float x, float y, int button = 0)
        {
            Enqueue(() =>
            {
                var pos = new Vector2(x, y);
                var target = RaycastUI(pos);
                if (target == null) return;
                var pd = CreatePointerEventData(pos, button);
                ExecuteEvents.Execute(target, pd, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(target, pd, ExecuteEvents.pointerDownHandler);
            });
        }

        public static void EnqueueMouseUp(float x, float y, int button = 0)
        {
            Enqueue(() =>
            {
                var pos = new Vector2(x, y);
                var target = RaycastUI(pos);
                if (target == null) return;
                var pd = CreatePointerEventData(pos, button);
                ExecuteEvents.Execute(target, pd, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(target, pd, ExecuteEvents.pointerClickHandler);
            });
        }

        public static void EnqueueMouseMove(float x, float y)
        {
            Enqueue(() =>
            {
                var pos = new Vector2(x, y);
                var target = RaycastUI(pos);
                if (target == null) return;
                var pd = CreatePointerEventData(pos, 0);
                ExecuteEvents.Execute(target, pd, ExecuteEvents.pointerMoveHandler);
            });
        }

        public static void EnqueueMouseDrag(float fromX, float fromY, float toX, float toY)
        {
            Enqueue(() =>
            {
                var fromPos = new Vector2(fromX, fromY);
                var toPos = new Vector2(toX, toY);
                var target = RaycastUI(fromPos);
                if (target == null) return;
                var pd = CreatePointerEventData(fromPos, 0);
                pd.pressPosition = fromPos;
                ExecuteEvents.Execute(target, pd, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(target, pd, ExecuteEvents.beginDragHandler);
                pd.position = toPos;
                pd.delta = toPos - fromPos;
                ExecuteEvents.Execute(target, pd, ExecuteEvents.dragHandler);
                ExecuteEvents.Execute(target, pd, ExecuteEvents.endDragHandler);
                ExecuteEvents.Execute(target, pd, ExecuteEvents.pointerUpHandler);
            });
        }

        public static void EnqueueMouseScroll(float x, float y, float scrollDelta)
        {
            Enqueue(() =>
            {
                var pos = new Vector2(x, y);
                var target = RaycastUI(pos);
                if (target == null) return;
                var pd = CreatePointerEventData(pos, 0);
                pd.scrollDelta = new Vector2(0, scrollDelta);
                ExecuteEvents.Execute(target, pd, ExecuteEvents.scrollHandler);
            });
        }

        #endregion

        #region Keyboard Actions

        public static void EnqueueKeyDown(KeyCode key)
        {
            Enqueue(() =>
            {
                var es = EventSystem.current;
                if (es == null || es.currentSelectedGameObject == null)
                {
                    Debug.LogWarning("[UIInputSimulator] No selected UI element for key: " + key);
                    return;
                }
                var bed = new BaseEventData(es);
                ExecuteEvents.Execute(es.currentSelectedGameObject, bed, ExecuteEvents.updateSelectedHandler);
            });
        }

        public static void EnqueueKeyUp(KeyCode key)
        {
            Enqueue(() =>
            {
                var es = EventSystem.current;
                if (es == null || es.currentSelectedGameObject == null) return;
                var bed = new BaseEventData(es);
                ExecuteEvents.Execute(es.currentSelectedGameObject, bed, ExecuteEvents.updateSelectedHandler);
            });
        }

        public static void EnqueueKeyPress(KeyCode key)
        {
            EnqueueKeyDown(key);
            EnqueueKeyUp(key);
        }

        public static void EnqueueTextInput(string text)
        {
            Enqueue(() =>
            {
                var es = EventSystem.current;
                if (es == null) return;
                var selected = es.currentSelectedGameObject;
                if (selected == null)
                {
                    var inputs = FindObjectsByType<InputField>(FindObjectsSortMode.None);
                    foreach (var input in inputs)
                    {
                        if (input != null && input.isFocused)
                        {
                            input.text += text;
                            input.onValueChanged?.Invoke(input.text);
                            return;
                        }
                    }
                    Debug.LogWarning("[UIInputSimulator] No focused InputField for text input");
                    return;
                }
                var inputField = selected.GetComponent<InputField>();
                if (inputField != null)
                {
                    inputField.text += text;
                    inputField.onValueChanged?.Invoke(inputField.text);
                    return;
                }
                var tmpInput = selected.GetComponents<Component>()
                    .FirstOrDefault(c => c != null && c.GetType().Name.Contains("TMP_InputField"));
                if (tmpInput != null)
                {
                    var textProp = tmpInput.GetType().GetProperty("text");
                    if (textProp != null)
                    {
                        string current = textProp.GetValue(tmpInput) as string ?? "";
                        textProp.SetValue(tmpInput, current + text);
                    }
                    return;
                }
                Debug.LogWarning("[UIInputSimulator] Selected object is not an InputField");
            });
        }

        #endregion

        #region Internal Helpers

        private static PointerEventData CreatePointerEventData(Vector2 position, int button, int clickCount = 1)
        {
            var eventSystem = EventSystem.current;
            var pointerData = new PointerEventData(eventSystem)
            {
                position = position,
                pressPosition = position,
                button = (PointerEventData.InputButton)button,
                clickCount = clickCount,
                eligibleForClick = true,
            };
            var raycastResults = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerData, raycastResults);
            if (raycastResults.Count > 0)
            {
                pointerData.pointerCurrentRaycast = raycastResults[0];
                pointerData.pointerPressRaycast = raycastResults[0];
            }
            return pointerData;
        }

        private static GameObject RaycastUI(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return null;
            var pointerData = new PointerEventData(eventSystem) { position = screenPosition };
            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerData, results);
            return results.Count > 0 ? results[0].gameObject : null;
        }

        private static Button FindButtonByText(string text)
        {
            var buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
            foreach (var btn in buttons)
            {
                if (btn == null || !btn.gameObject.activeInHierarchy || !btn.interactable) continue;
                string label = GetButtonText(btn);
                if (!string.IsNullOrEmpty(label) && label.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    return btn;
            }
            return null;
        }

        private static Button FindButtonByName(string name)
        {
            var buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
            foreach (var btn in buttons)
            {
                if (btn == null || !btn.gameObject.activeInHierarchy) continue;
                if (btn.gameObject.name == name) return btn;
            }
            return null;
        }

        private static T FindComponentByName<T>(string name) where T : Component
        {
            var components = FindObjectsByType<T>(FindObjectsSortMode.None);
            foreach (var comp in components)
            {
                if (comp == null || !comp.gameObject.activeInHierarchy) continue;
                if (comp.gameObject.name == name) return comp;
            }
            return null;
        }

        private static string GetButtonText(Button btn)
        {
            var textComp = btn.GetComponentInChildren<Text>();
            if (textComp != null) return textComp.text;
            var components = btn.GetComponentsInChildren<Component>();
            var tmpComp = components.FirstOrDefault(c => c != null && c.GetType().Name.Contains("TextMeshPro"));
            if (tmpComp != null)
            {
                var textProp = tmpComp.GetType().GetProperty("text");
                if (textProp != null) return textProp.GetValue(tmpComp) as string ?? "";
            }
            return "";
        }

        private static void SimulateButtonClick(Button button)
        {
            var go = button.gameObject;
            var eventSystem = EventSystem.current;
            if (eventSystem == null) { button.onClick?.Invoke(); return; }

            var rectTransform = go.GetComponent<RectTransform>();
            Vector2 screenPos;
            if (rectTransform != null)
            {
                var canvas = go.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    Vector3[] corners = new Vector3[4];
                    rectTransform.GetWorldCorners(corners);
                    screenPos = new Vector2((corners[0].x + corners[2].x) / 2f, (corners[0].y + corners[2].y) / 2f);
                }
                else if (canvas != null && canvas.worldCamera != null)
                {
                    screenPos = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectTransform.position);
                }
                else
                {
                    screenPos = RectTransformUtility.WorldToScreenPoint(null, rectTransform.position);
                }
            }
            else
            {
                screenPos = new Vector2(Screen.width / 2f, Screen.height / 2f);
            }

            var pointerData = CreatePointerEventData(screenPos, 0, 1);
            pointerData.pointerPress = go;
            ExecuteEvents.Execute(go, pointerData, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(go, pointerData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(go, pointerData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(go, pointerData, ExecuteEvents.pointerClickHandler);
        }

        private static void SimulateClickAtPosition(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) { Debug.LogWarning("[UIInputSimulator] No EventSystem"); return; }
            var target = RaycastUI(screenPosition);
            if (target == null)
            {
                Debug.LogWarning("[UIInputSimulator] No UI at (" + screenPosition.x + ", " + screenPosition.y + ")");
                return;
            }
            var pointerData = CreatePointerEventData(screenPosition, 0, 1);
            pointerData.pointerPress = target;
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerClickHandler);
        }

        #endregion
    }
}
