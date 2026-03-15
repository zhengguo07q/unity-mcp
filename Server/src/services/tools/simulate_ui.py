"""
Tool for simulating UI interactions in Unity's Game view during Play Mode.

Supports listing and interacting with legacy UI (UnityEngine.UI) elements:
Button, InputField, Toggle, Slider, and Dropdown.
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
        "Simulates UI interactions in Unity's Game view during Play Mode. "
        "Works with legacy UI (UnityEngine.UI) elements: Button, InputField, Toggle, Slider, Dropdown.\n\n"
        "List actions (read-only):\n"
        "- list_buttons: List all active Button elements with text, name, path, interactable state\n"
        "- list_inputs: List all active InputField elements\n"
        "- list_toggles: List all active Toggle elements\n"
        "- list_sliders: List all active Slider elements with current/min/max values\n"
        "- list_dropdowns: List all active Dropdown elements with options\n\n"
        "Interaction actions:\n"
        "- click: Click a Button by 'text' (fuzzy, case-insensitive contains) or 'name' (exact GameObject name)\n"
        "- set_input: Set InputField text by 'name' (exact)\n"
        "- set_toggle: Set Toggle state by 'name' (exact) and 'value' (true/false)\n"
        "- set_slider: Set Slider value by 'name' (exact) and 'value' (float, auto-clamped)\n"
        "- set_dropdown: Select Dropdown option by 'name' (exact) and 'index' (0-based)\n\n"
        "IMPORTANT: Requires Play Mode. Returns an error if the editor is not playing."
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
    ], "Action to perform."],

    # For click action
    text: Annotated[str,
                    "Button text to match (fuzzy, case-insensitive contains). For 'click' action."] | None = None,

    # For click, set_input, set_toggle, set_slider, set_dropdown
    name: Annotated[str,
                    "Exact GameObject name. For 'click' (alternative to text), 'set_input', "
                    "'set_toggle', 'set_slider', 'set_dropdown'."] | None = None,

    # For set_toggle
    value: Annotated[bool | float | str,
                     "Value to set. For 'set_toggle': true/false. For 'set_slider': float number."] | None = None,

    # For set_dropdown
    index: Annotated[int,
                     "0-based option index. For 'set_dropdown' action."] | None = None,

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
