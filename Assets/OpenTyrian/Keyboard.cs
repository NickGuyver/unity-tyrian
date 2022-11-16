using JE_longint = System.Int32;
using JE_integer = System.Int16;
using JE_shortint = System.SByte;
using JE_word = System.UInt16;
using JE_byte = System.Byte;
using JE_boolean = System.Boolean;
using JE_char = System.Char;
using JE_real = System.Single;

using static JoystickC;
using static VideoC;
using static MouseC;

using UnityEngine;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public static class KeyboardC
{
    public static JE_boolean ESCPressed;
    public static bool OverrideEscapePress;

    public static JE_boolean newkey, newmouse, keydown, mousedown;
    public static byte lastmouse_but;
    public static int lastmouse_x, lastmouse_y;

    public static bool[] mouse_pressed = new bool[4];
    public static int mouse_x;// => Mathf.Clamp((int)Input.mousePosition.x, 0, vga_width);
    public static int mouse_y;// => Screen.height - Mathf.Clamp((int)Input.mousePosition.y, 0, vga_height);

    private static readonly KeyCode[] SupportedKeys = 
        Enum.GetValues(typeof(KeyCode)).
            Cast<KeyCode>().Where(e => e < KeyCode.Mouse0).ToArray();     //Mouse0 is the first entry we don't support
    public static bool[] keysactive = new bool[(int)SupportedKeys.Max() + 1];
    public static bool[] keyswereactive = new bool[(int)SupportedKeys.Max() + 1];

    public static KeyCode lastkey_sym;
    public static char lastkey_char;

    public static bool input_grab_enabled;

    public static WaitWhile coroutine_wait_input(JE_boolean keyboard, JE_boolean mouse, JE_boolean joystick)
    {
        service_SDL_events(false);
        return new WaitWhile(() => {
            push_joysticks_as_keyboard();
            service_SDL_events(false);
#if WITH_NETWORK
            if (isNetworkGame)
                network_check();
#endif
            return !((keyboard && keydown) || (mouse && mousedown) || (joystick && joydown));
        });
    }
    public static WaitWhile coroutine_wait_noinput(JE_boolean keyboard, JE_boolean mouse, JE_boolean joystick)
    {
        service_SDL_events(false);
        return new WaitWhile(() => {
            push_joysticks_as_keyboard();
            service_SDL_events(false);
#if WITH_NETWORK
            if (isNetworkGame)
                network_check();
#endif
            return ((keyboard && keydown) || (mouse && mousedown) || (joystick && joydown));
        });
    }

    private static Coroutine mouseMotionListener;
    private static float accumulatedMouseX, accumulatedMouseY;
    private static bool[] accumulatedMouseDowns = new bool[3];
    private static bool[] accumulatedMouseUps = new bool[3];
    public static void input_grab(bool enable)
    {
        input_grab_enabled = enable;
        Cursor.visible = !enable;
        Cursor.lockState = enable ? CursorLockMode.Locked : CursorLockMode.None;
        if (mouseMotionListener != null)
        {
            CoroutineRunner.Instance.StopCoroutine(mouseMotionListener);
            mouseMotionListener = null;
        }

        if (enable)
        {
            if (touchscreen)
            {
                mouseMotionListener = CoroutineRunner.Run(listenForTouches());
            }
            else
            {
                mouseMotionListener = CoroutineRunner.Run(listenForMouseMotion());
            }
        }
    }

    private static IEnumerator listenForMouseMotion()
    {
        accumulatedMouseX = 0;
        accumulatedMouseY = 0;
        while (true)
        {
            accumulatedMouseX += Input.GetAxis("Mouse X");
            accumulatedMouseY -= Input.GetAxis("Mouse Y");
            for (int i = 0; i < 3; ++i)
            {
                if (Input.GetMouseButtonDown(i))
                {
                    accumulatedMouseDowns[i] = true;
                }
                else if (Input.GetMouseButtonUp(i))
                {
                    accumulatedMouseUps[i] = true;
                }
            }
            yield return null;
        }
    }

    private static IEnumerator listenForTouches()
    {
        accumulatedMouseX = 0;
        accumulatedMouseY = 0;
        while (true)
        {
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                Vector2 deltaPos = touch.deltaPosition;
                deltaPos = scaleToVGA(deltaPos);
                accumulatedMouseX += deltaPos.x;
                accumulatedMouseY -= deltaPos.y;
                if (touch.tapCount > 1) {
                    if (touch.phase == TouchPhase.Began) {
                        accumulatedMouseDowns[0] = true;
                    } else if (touch.phase == TouchPhase.Ended) {
                        accumulatedMouseUps[0] = true;
                    }
                }
            }
            yield return null;
        }
    }

    public static byte JE_mousePosition(out JE_word mouseX, out JE_word mouseY)
    {
        service_SDL_events(false);

        mouseX = (JE_word)mouse_x;
        mouseY = (JE_word)mouse_y;

        return mousedown ? lastmouse_but : (byte)0;
    }
    public static void set_mouse_position(int x, int y)
    {
        if (input_grab_enabled)
        {
            //nah...
            //SDL_WarpMouse(x * scalers[scaler].width / vga_width, y * scalers[scaler].height / vga_height);
            mouse_x = x;
            mouse_y = y;
        }
    }

    //When true, tells the game that the joystick is being used
    private static bool JoyStickIsBeingUsed = false;
    
    //A list of keys that need to be returned for input
    private static readonly List<KeyCode> JoyStickKeysToReturn = new List<KeyCode>();

    /// <summary>
    /// Tries to get the input from the joystick and returns it as a keyboard input. This is only for
    /// some inputs that are currently only working for menus
    /// </summary>
    /// <returns></returns>
    private static List<KeyCode> GetButtonInputFromJoysticks()
    {
#if UNITY_STANDALONE_WIN
        
        //Checking for left trigger being clicked
        if (Input.GetAxis("Left Trigger Windows") > 0.2f)
        {
            //Adding this only if this is already not part of the list
            if (JoyStickKeysToReturn.Contains(KeyCode.LeftControl) == false)
            {
                JoyStickKeysToReturn.Add(KeyCode.LeftControl);
            }
        }
        else
        {
            if (JoyStickKeysToReturn.Contains(KeyCode.LeftControl))
            {
                JoyStickKeysToReturn.Remove(KeyCode.LeftControl);
            }
        }
            
        //Checking for right trigger being clicked
        if (Input.GetAxis("Right Trigger Windows") > 0.2f)
        {
            if (JoyStickKeysToReturn.Contains(KeyCode.LeftAlt) == false)
            {
                JoyStickKeysToReturn.Add(KeyCode.LeftAlt);
            }
        }
        else
        {
            if (JoyStickKeysToReturn.Contains(KeyCode.LeftAlt))
            {
                JoyStickKeysToReturn.Remove(KeyCode.LeftAlt);
            }
        }
        
#endif

#if UNITY_STANDALONE_LINUX

        //Checking for left trigger being clicked
        if (Input.GetAxis("Left Trigger") > 0.2f)
        {
            //Adding this only if this is already not part of the list
            if (JoyStickKeysToReturn.Contains(KeyCode.LeftControl) == false)
            {
                JoyStickKeysToReturn.Add(KeyCode.LeftControl);
            }
        }
        else
        {
            if (JoyStickKeysToReturn.Contains(KeyCode.LeftControl))
            {
                JoyStickKeysToReturn.Remove(KeyCode.LeftControl);
            }
        }
            
        //Checking for right trigger being clicked
        if (Input.GetAxis("Right Trigger") > 0.2f)
        {
            if (JoyStickKeysToReturn.Contains(KeyCode.LeftAlt) == false)
            {
                JoyStickKeysToReturn.Add(KeyCode.LeftAlt);
            }
        }
        else
        {
            if (JoyStickKeysToReturn.Contains(KeyCode.LeftAlt))
            {
                JoyStickKeysToReturn.Remove(KeyCode.LeftAlt);
            }
        }
#endif

        //When the 'Y' button is pressed, we will add Return to the list or if the 'A' button is pressed,
        //we will add Space to the list
        if (Input.GetButton("Change Fire") || Input.GetButton("Jump"))
        {
            if (Input.GetButton("Change Fire"))
            {
                if (JoyStickKeysToReturn.Contains(KeyCode.Return) == false)
                {
                    JoyStickKeysToReturn.Add(KeyCode.Return);
                }
            }
            
            //Running this only if change fire was not pressed
            if (Input.GetButton("Jump"))
            {
                if (JoyStickKeysToReturn.Contains(KeyCode.Space) == false)
                {
                    JoyStickKeysToReturn.Add(KeyCode.Space);
                }
            }
        }
        else //Removing both keys from the list if they already exist
        {
            if (JoyStickKeysToReturn.Contains(KeyCode.Return))
            {
                JoyStickKeysToReturn.Remove(KeyCode.Return);
            }
            
            if (JoyStickKeysToReturn.Contains(KeyCode.Space))
            {
                JoyStickKeysToReturn.Remove(KeyCode.Space);
            }
        }
        
        //When the pause button is clicked, we will add the P key
        if (Input.GetButtonDown("Pause"))
        {
            //Checking if the keycode already exist in the list
            if (JoyStickKeysToReturn.Contains(KeyCode.P) == false)
            {
                JoyStickKeysToReturn.Add(KeyCode.P);
            }
        }
        else
        {
            if (JoyStickKeysToReturn.Contains(KeyCode.P))
            {
                JoyStickKeysToReturn.Remove(KeyCode.P);
            }
        }
        
        //When the cancel button is clicked, we want to add the escape key
        if (Input.GetButton("Cancel"))
        {
            //Checking if the keycode already exist in the list
            if (JoyStickKeysToReturn.Contains(KeyCode.Escape) == false)
            {
                JoyStickKeysToReturn.Add(KeyCode.Escape);
            }
        }
        else
        {
            if (JoyStickKeysToReturn.Contains(KeyCode.Escape))
            {
                JoyStickKeysToReturn.Remove(KeyCode.Escape);
            }
        }

        //Checking if the left joystick is going left
        if (Input.GetAxis("Horizontal") < -0.1f)
        {
            //Checking if the keycode already exist in the list
            if (JoyStickKeysToReturn.Contains(KeyCode.LeftArrow) == false)
            {
                JoyStickKeysToReturn.Add(KeyCode.LeftArrow);
            }
        }
        else
        {
            if (JoyStickKeysToReturn.Contains(KeyCode.LeftArrow))
            {
                JoyStickKeysToReturn.Remove(KeyCode.LeftArrow);
            }
        }
        
        //Checking if the left joystick is going right
        if (Input.GetAxis("Horizontal") > 0.1f)
        {
            //Checking if the keycode already exist in the list
            if (JoyStickKeysToReturn.Contains(KeyCode.RightArrow) == false)
            {
                JoyStickKeysToReturn.Add(KeyCode.RightArrow);
            }
        }
        else
        {
            if (JoyStickKeysToReturn.Contains(KeyCode.RightArrow))
            {
                JoyStickKeysToReturn.Remove(KeyCode.RightArrow);
            }
        }

        //Checking if the left joystick is going up
        if (Input.GetAxis("Vertical") > 0.1f)
        {
            //Checking if the keycode already exist in the list
            if (JoyStickKeysToReturn.Contains(KeyCode.UpArrow) == false)
            {
                JoyStickKeysToReturn.Add(KeyCode.UpArrow);
            }
        }
        else
        {
            if (JoyStickKeysToReturn.Contains(KeyCode.UpArrow))
            {
                JoyStickKeysToReturn.Remove(KeyCode.UpArrow);
            }
        }
        
        //Checking if the left joystick is going down
        if (Input.GetAxis("Vertical") < -0.1f)
        {
            //Checking if the keycode already exist in the list
            if (JoyStickKeysToReturn.Contains(KeyCode.DownArrow) == false)
            {
                JoyStickKeysToReturn.Add(KeyCode.DownArrow);
            }
        }
        else
        {
            if (JoyStickKeysToReturn.Contains(KeyCode.DownArrow))
            {
                JoyStickKeysToReturn.Remove(KeyCode.DownArrow);
            }
        }

        return JoyStickKeysToReturn;
    }
    
    public static void service_SDL_events(JE_boolean clear_new)
    {
        if (clear_new)
            newkey = newmouse = false;

        if (!Application.isFocused)
            input_grab(false);

        mouse_x += (int)accumulatedMouseX;
        mouse_y += (int)accumulatedMouseY;
        mouse_x = Mathf.Clamp(mouse_x, 0, vga_width);
        mouse_y = Mathf.Clamp(mouse_y, 0, vga_height);

        accumulatedMouseX = 0;
        accumulatedMouseY = 0;

        for (byte i = 0; i < 3; ++i)
        {
            if (!input_grab_enabled && ((has_mouse && Input.GetMouseButton(i)) || (touchscreen && Input.touchCount > 0)))
            {
                input_grab(true);
                break;
            }

            if (accumulatedMouseDowns[i])
            {
                newmouse = true;
                lastmouse_x = mouse_x;
                lastmouse_y = mouse_y;
                lastmouse_but = (byte)(i + 1);
                mousedown = true;

                mouse_pressed[i] = true;
                accumulatedMouseDowns[i] = false;
            }
            else if (accumulatedMouseUps[i])
            {
                mouse_pressed[i] = false;
                accumulatedMouseUps[i] = false;
                mousedown = false;
            }
        }

        keydown = false;
        for (int i = 0; i < SupportedKeys.Length; ++i)
        {
            //When there is no keycode
            KeyCode k;
            int idx;
            bool active;

            k = SupportedKeys[i];
            idx = (int)k;
            active = Input.GetKey(k);
            
            //Checking if the joystick array is not equal to null. If it is not equal to zero,
            //it means that the joystick is being used
            if (GetButtonInputFromJoysticks().Count != 0)
            {
                //Checking if GetButtonInputFromJoysticks() contains k
                if (GetButtonInputFromJoysticks().Contains(k))
                {
                    idx = (int) k;
                    //If it does, then we set active to true
                    active = true;
                }
                else
                {
                    idx = (int) k;
                    //If it doesn't, then we set active to false, which means that this key was not active
                    active = false;
                }
            }

            if (k == KeyCode.Escape && OverrideEscapePress)
            {
                active = true;
                OverrideEscapePress = false;
            }
            if (active && !keyswereactive[idx])
            {
                if (k == KeyCode.F10)
                {
                    input_grab(!input_grab_enabled);
                }
                newkey = true;
                lastkey_sym = k;
                Debug.Log("Last clicked key was " + lastkey_sym);
                lastkey_char = (char)lastkey_sym;
            }
            if (active && keyswereactive[idx] && !keysactive[idx])
            {
                //We intentionally turned it off. Don't turn it back on until we get another key down
            }
            else
            {
                keydown |= keysactive[idx] = keyswereactive[idx] = active;
            }
        }
        ESCPressed = keysactive[(int)KeyCode.Escape];
    }
    
    public static void sleep_game()
    {

    }
    
    public static void JE_clearKeyboard()
    {
        // /!\ Doesn't seems important. I think. D:
    }

}