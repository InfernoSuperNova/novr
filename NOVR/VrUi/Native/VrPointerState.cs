using UnityEngine;
using UnityEngine.InputSystem;

namespace NOVR.VrUi.Native;

public class VrPointerState
{
    public bool IsAvailable { get; private set; }
    public Vector2 ScreenPosition { get; private set; }
    public Vector3 WorldPosition { get; private set; }
    public bool PrimaryButtonPressed { get; private set; }
    public bool PrimaryButtonHeld { get; private set; }
    public Vector2 Scroll { get; private set; }

    public void Update(VrUiCursor? cursor)
    {
        var mouse = Mouse.current;
        IsAvailable = cursor != null && cursor.IsActive && mouse != null;
        if (!IsAvailable)
        {
            PrimaryButtonPressed = false;
            PrimaryButtonHeld = false;
            Scroll = Vector2.zero;
            return;
        }

        ScreenPosition = cursor!.GetScreenPoint();
        WorldPosition = cursor.CursorPosition;
        PrimaryButtonPressed = mouse!.leftButton.wasPressedThisFrame;
        PrimaryButtonHeld = mouse.leftButton.isPressed;
        Scroll = mouse.scroll.ReadValue();
    }
}
