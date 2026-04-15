using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public bool isDebugMode = false;
    public bool buttonPressed;
    public bool buttonPressedThisFrame;
    public bool upPressed;
    public bool downPressed;
    public bool leftPressed;
    public bool rightPressed;
    public bool upPressedThisFrame;
    public bool downPressedThisFrame;
    public bool leftPressedThisFrame;
    public bool rightPressedThisFrame;

    public void Init()
    {
        if (isDebugMode)
        {
            Debug.Log("InputManager is in debug mode. Using keyboard input.");
        }
    }

    public void UpdateInput()
    {
        buttonPressed = false;
        buttonPressedThisFrame = false;
        upPressed = false;
        downPressed = false;
        leftPressed = false;
        rightPressed = false;
        upPressedThisFrame = false;
        downPressedThisFrame = false;
        leftPressedThisFrame = false;
        rightPressedThisFrame = false;

        if (isDebugMode)
        {
            if (Keyboard.current == null) return;

            buttonPressed = Keyboard.current.anyKey.isPressed;
            buttonPressedThisFrame = Keyboard.current.anyKey.wasPressedThisFrame;
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
