"""
Tool for simulating UI interactions in Unity Game view during Play Mode.

Supports listing and interacting with legacy UI (UnityEngine.UI) elements:
Button, InputField, Toggle, Slider, and Dropdown.
Also supports low-level mouse, keyboard, and text input simulation.

All interaction actions use a runtime queue pattern - they are enqueued and
executed in the next game Update() frame, ensuring ExecuteEvents works correctly.
Requires Play Mode to be active.
"""
from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    group="ui",
    description=(
        "Simulates UI interactions in Unity Game view during Play Mode. "
        "Works with legacy UI (UnityEngine.UI) elements and supports low-level input simulation.\n\n"
        "List actions (read-only, immediate):\n"
        "- list_buttons: List all active Button elements with text, name, path, interactable state\n"
        "- list_inputs: List all active InputField elements\n"
        "- list_toggles: List all active Toggle elements\n"
        "- list_sliders: List all active Slider elements with current/min/max values\n"
        "- list_dropdowns: List all active Dropdown elements with options\n\n"
        "UI interaction actions (enqueued, execute next frame):\n"
        "- click: Click a Button by 'text' (fuzzy), 'name' (exact), or 'x'+'y' (screen position)\n"
        "- set_input: Set InputField text by 'name' and 'text'\n"
        "- set_toggle: Set Toggle state by 'name' and 'value' (true/false)\n"
        "- set_slider: Set Slider value by 'name' and 'value' (float, auto-clamped)\n"
        "- set_dropdown: Select Dropdown option by 'name' and 'index' (0-based)\n\n"
        "Mouse actions (enqueued, execute next frame):\n"
        "- mouse_click: Click at screen position with 'x', 'y', optional 'button' (0=left,1=right,2=middle), 'click_count'\n"
        "- mouse_down: Press mouse button at 'x', 'y'\n"
        "- mouse_up: Release mouse button at 'x', 'y'\n"
        "- mouse_move: Move cursor to 'x', 'y'\n"
        "- mouse_drag: Drag from 'from_x','from_y' to 'to_x','to_y'\n"
        "- mouse_scroll: Scroll at 'x','y' with 'scroll_delta' (positive=up)\n\n"
        "Keyboard actions (enqueued, execute next frame):\n"
        "- key_down: Press key by Unity KeyCode name (e.g. 'Return', 'Space', 'A')\n"
        "- key_up: Release key\n"
        "- key_press: Press and release key\n"
        "- text_input: Type text into focused InputField\n\n"
        "IMPORTANT: Requires Play Mode. Interaction actions are enqueued and execute on the next frame."
    ),
    annotations=ToolAnnotations(
        title="Simulate UI",
        destructiveHint=True,
    ),
)
async def simulate_ui(
    ctx: Context,
    action: Annotated[Literal[
        "list_buttons",
        "click",
        "list_inputs",
        "set_input",
        "list_toggles",
        "set_toggle",
        "list_sliders",
        "set_slider",
        "list_dropdowns",
        "set_dropdown",
        "mouse_click",
        "mouse_down",
        "mouse_up",
        "mouse_move",
        "mouse_drag",
        "mouse_scroll",
        "key_down",
        "key_up",
        "key_press",
        "text_input",
    ], "Action to perform."],

    # For click action
    text: Annotated[str,
                    "Button text to match (fuzzy, case-insensitive contains). For 'click' action."] | None = None,

    # For click, set_input, set_toggle, set_slider, set_dropdown
    name: Annotated[str,
                    "Exact GameObject name. For 'click' (alternative to text), 'set_input', "
                    "'set_toggle', 'set_slider', 'set_dropdown'."] | None = None,

    # For set_toggle, set_slider
    value: Annotated[bool | float | str,
                     "Value to set. For 'set_toggle': true/false. For 'set_slider': float number."] | None = None,

    # For set_dropdown
    index: Annotated[int,
                     "0-based option index. For 'set_dropdown' action."] | None = None,

    # For mouse actions and click-at-position
    x: Annotated[float,
                 "Screen X coordinate. For mouse actions and click-at-position."] | None = None,

    y: Annotated[float,
                 "Screen Y coordinate. For mouse actions and click-at-position."] | None = None,

    # For mouse_click
    button: Annotated[int,
                      "Mouse button: 0=left, 1=right, 2=middle. For 'mouse_click', 'mouse_down', 'mouse_up'."] | None = None,

    click_count: Annotated[int,
                           "Number of clicks (1=single, 2=double). For 'mouse_click'."] | None = None,

    # For mouse_drag
    from_x: Annotated[float,
                      "Drag start X coordinate. For 'mouse_drag'."] | None = None,

    from_y: Annotated[float,
                      "Drag start Y coordinate. For 'mouse_drag'."] | None = None,

    to_x: Annotated[float,
                    "Drag end X coordinate. For 'mouse_drag'."] | None = None,

    to_y: Annotated[float,
                    "Drag end Y coordinate. For 'mouse_drag'."] | None = None,

    # For mouse_scroll
    scroll_delta: Annotated[float,
                            "Scroll amount. Positive=up, negative=down. For 'mouse_scroll'."] | None = None,

    # For key actions
    key: Annotated[str,
                   "Unity KeyCode name (e.g. 'Return', 'Space', 'A', 'Escape'). For key actions."] | None = None,

) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params_dict: dict[str, Any] = {
        "action": action,
    }

    if text is not None:
        params_dict["text"] = text
    if name is not None:
        params_dict["name"] = name
    if value is not None:
        params_dict["value"] = value
    if index is not None:
        params_dict["index"] = index
    if x is not None:
        params_dict["x"] = x
    if y is not None:
        params_dict["y"] = y
    if button is not None:
        params_dict["button"] = button
    if click_count is not None:
        params_dict["click_count"] = click_count
    if from_x is not None:
        params_dict["from_x"] = from_x
    if from_y is not None:
        params_dict["from_y"] = from_y
    if to_x is not None:
        params_dict["to_x"] = to_x
    if to_y is not None:
        params_dict["to_y"] = to_y
    if scroll_delta is not None:
        params_dict["scroll_delta"] = scroll_delta
    if key is not None:
        params_dict["key"] = key

    try:
        response = await send_with_unity_instance(
            async_send_command_with_retry,
            unity_instance,
            "simulate_ui",
            params_dict,
        )

        if isinstance(response, dict):
            return response
        return {"success": False, "message": str(response)}

    except Exception as e:
        return {"success": False, "message": f"Error simulating UI interaction: {e!s}"}
