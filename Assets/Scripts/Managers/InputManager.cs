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
    public bool upPressed;
    public bool downPressed;
    public bool leftPressed;
    public bool rightPressed;

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
            upPressed = Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed;
            downPressed = Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;
            leftPressed = Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed;
            rightPressed = Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed;
        }
    }
}