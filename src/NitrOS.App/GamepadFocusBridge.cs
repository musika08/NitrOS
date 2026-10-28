using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Input;
using NitrOS.App.Logic.Navigation;
using Windows.Gaming.Input;

namespace NitrOS.App;

public sealed class GamepadFocusBridge
{
    private readonly Window _window;
    private readonly Navigator _navigator;
    private readonly DispatcherQueueTimer _timer;

    private GamepadButtons _lastButtons;
    private DateTimeOffset _lastMoveTime = DateTimeOffset.MinValue;
    private const double StickDeadZone = 0.5;
    private static readonly TimeSpan InitialRepeatDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan RepeatRate = TimeSpan.FromMilliseconds(120);
    private FocusNavigationDirection? _lastDirection;

    public GamepadFocusBridge(Window window, Navigator navigator)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));

        _timer = _window.DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += OnTick;

        _window.Activated += (s, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated)
            {
                _timer.Stop();
            }
            else
            {
                _timer.Start();
            }
        };

        _timer.Start();
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        var gamepads = Gamepad.Gamepads;
        if (gamepads.Count == 0 || _window.Content?.XamlRoot is null) return;

        var reading = gamepads[0].GetCurrentReading();
        var buttons = reading.Buttons;
        var now = DateTimeOffset.UtcNow;

        // Navigation (D-Pad or Left Stick)
        FocusNavigationDirection? direction = null;

        if (buttons.HasFlag(GamepadButtons.DPadUp) || reading.LeftThumbstickY > StickDeadZone)
            direction = FocusNavigationDirection.Up;
        else if (buttons.HasFlag(GamepadButtons.DPadDown) || reading.LeftThumbstickY < -StickDeadZone)
            direction = FocusNavigationDirection.Down;
        else if (buttons.HasFlag(GamepadButtons.DPadLeft) || reading.LeftThumbstickX < -StickDeadZone)
            direction = FocusNavigationDirection.Left;
        else if (buttons.HasFlag(GamepadButtons.DPadRight) || reading.LeftThumbstickX > StickDeadZone)
            direction = FocusNavigationDirection.Right;

        if (direction is not null)
        {
            bool isNew = direction != _lastDirection;
            bool repeatDue = (now - _lastMoveTime) >= (_lastDirection is not null ? RepeatRate : InitialRepeatDelay);

            if (isNew || repeatDue)
            {
                _lastDirection = direction;
                _lastMoveTime = now;
                try
                {
                    FocusManager.TryMoveFocus(
                        direction.Value,
                        new FindNextElementOptions { SearchRoot = _window.Content.XamlRoot.Content });
                }
                catch
                {
                    // Ignore focus move failures
                }
            }
        }
        else
        {
            _lastDirection = null;
        }

        // A Button (Invoke or Toggle)
        if (buttons.HasFlag(GamepadButtons.A) && !_lastButtons.HasFlag(GamepadButtons.A))
        {
            InvokeFocusedElement();
        }

        // B Button (Back)
        if (buttons.HasFlag(GamepadButtons.B) && !_lastButtons.HasFlag(GamepadButtons.B))
        {
            _navigator.GoBack();
        }

        _lastButtons = buttons;
    }

    private void InvokeFocusedElement()
    {
        if (_window.Content?.XamlRoot is null) return;

        var focused = FocusManager.GetFocusedElement(_window.Content.XamlRoot) as UIElement;
        if (focused is null) return;

        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(focused);
        if (peer is null) return;

        if (peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoker)
        {
            invoker.Invoke();
        }
        else if (peer.GetPattern(PatternInterface.Toggle) is IToggleProvider toggler)
        {
            toggler.Toggle();
        }
    }
}
