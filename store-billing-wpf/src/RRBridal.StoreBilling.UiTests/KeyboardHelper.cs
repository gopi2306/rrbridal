using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace RRBridal.StoreBilling.UiTests;

internal static class KeyboardHelper
{
    public static void ReplaceText(AutomationElement element, string text)
    {
        element.Focus();
        Keyboard.Press(VirtualKeyShort.CONTROL);
        try
        {
            Keyboard.Type(VirtualKeyShort.KEY_A);
        }
        finally
        {
            Keyboard.Release(VirtualKeyShort.CONTROL);
        }

        Keyboard.Type(text);
    }

    public static void PressEnter()
    {
        Keyboard.Type(VirtualKeyShort.ENTER);
    }

    public static void PressEscape()
    {
        Keyboard.Type(VirtualKeyShort.ESCAPE);
    }

    public static void PressControlShortcut(VirtualKeyShort key)
    {
        Keyboard.Press(VirtualKeyShort.CONTROL);
        try
        {
            Keyboard.Type(key);
        }
        finally
        {
            Keyboard.Release(VirtualKeyShort.CONTROL);
        }
    }
}
