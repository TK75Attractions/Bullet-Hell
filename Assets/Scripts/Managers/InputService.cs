using UnityEngine;
using UnityEngine.InputSystem;


public class ExtensionOfNativeClassAttribute : System.Attribute
{
}
[ExtensionOfNativeClass]
public class InputService : IInputService
{
    public bool isDebugMode { get; private set;} = false;
    public bool buttonPressed { get; private set;}
    public bool buttonPressedThisFrame { get; private set;}
    public bool upPressed { get; private set;}
    public bool downPressed { get; private set;}
    public bool leftPressed { get; private set;}
    public bool rightPressed { get; private set;}
    public bool upPressedThisFrame { get; private set;}
    public bool downPressedThisFrame { get; private set;}
    public bool leftPressedThisFrame { get; private set;}
    public bool rightPressedThisFrame { get; private set;}

    public void Init()
    {
        if (isDebugMode)
        {
            Debug.Log("InputManager is in debug mode. Using keyboard input.");
        }
    }

    public void UpdateInput()
    {
        if (isDebugMode)
        {
            buttonPressed = Keyboard.current.spaceKey.isPressed;
            buttonPressedThisFrame = Keyboard.current.spaceKey.wasPressedThisFrame;
            upPressed = Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed;
            downPressed = Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;
            leftPressed = Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed;
            rightPressed = Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed;

            upPressedThisFrame = Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame;
            downPressedThisFrame = Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame;
            leftPressedThisFrame = Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame;
            rightPressedThisFrame = Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame;
        }
    }
}