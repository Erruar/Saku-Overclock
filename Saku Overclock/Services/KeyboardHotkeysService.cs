using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Wrappers;
using Windows.System;
using WinRT.Interop;
using WinUIEx.Messaging;
using static Saku_Overclock.Services.PresetManagerService;

namespace Saku_Overclock.Services;

public partial class KeyboardHotkeysService(IAppSettingsService settingsService, IApplyerService applyerService)
    : IKeyboardHotkeysService
{
    private IntPtr _hwnd;
    private WindowMessageMonitor? _monitor;
    private bool _enabled;

    private const int HkCustom = 1;
    private const int HkPremade = 2;
    private const int HkRtss = 3;

    public void Initialize()
    {
        _hwnd = WindowNative.GetWindowHandle(App.MainWindow);

        _monitor = new WindowMessageMonitor(App.MainWindow);
        _monitor.WindowMessageReceived += OnWindowMessage;

        Enable();
    }

    public void Enable()
    {
        if (_enabled || !settingsService.HotkeysEnabled)
        {
            return;
        }

        RegisterHotkeys();
        _enabled = true;
    }

    public void Disable()
    {
        if (!_enabled)
        {
            return;
        }

        UnregisterHotkeys();
        _enabled = false;
    }

    private void RegisterHotkeys()
    {
        KeyboardHookWrapper.RegisterHotKey(
            _hwnd,
            HkCustom,
            (uint)(KeyboardHookWrapper.HotKeyModifiers.Ctrl | KeyboardHookWrapper.HotKeyModifiers.Alt),
            (uint)VirtualKey.W);

        KeyboardHookWrapper.RegisterHotKey(
            _hwnd,
            HkPremade,
            (uint)(KeyboardHookWrapper.HotKeyModifiers.Ctrl | KeyboardHookWrapper.HotKeyModifiers.Alt),
            (uint)VirtualKey.P);

        KeyboardHookWrapper.RegisterHotKey(
            _hwnd,
            HkRtss,
            (uint)(KeyboardHookWrapper.HotKeyModifiers.Ctrl | KeyboardHookWrapper.HotKeyModifiers.Alt),
            (uint)VirtualKey.R);
    }

    private void UnregisterHotkeys()
    {
        KeyboardHookWrapper.UnregisterHotKey(_hwnd, HkCustom);
        KeyboardHookWrapper.UnregisterHotKey(_hwnd, HkPremade);
        KeyboardHookWrapper.UnregisterHotKey(_hwnd, HkRtss);
    }

    private void OnWindowMessage(object? sender, WindowMessageEventArgs? e)
    {
        if (e?.Message.MessageId != KeyboardHookWrapper.WM_HOTKEY)
        {
            return;
        }

        switch (e.Message.WParam)
        {
            case HkCustom:
                var customPreset = applyerService.SwitchCustomPreset();
                HandlePreset(customPreset);
                PresetChanged?.Invoke(this, customPreset);
                break;

            case HkPremade:
                var premadePreset = applyerService.SwitchPremadePreset();
                HandlePreset(premadePreset);
                PresetChanged?.Invoke(this, premadePreset);
                break;

            case HkRtss:
                ToggleRtss();
                break;
        }

        e.Handled = true;
    }

    private void HandlePreset(PresetId? preset)
    {
        if (!preset.HasValue)
        {
            return;
        }

        App.MainWindow.DispatcherQueue.TryEnqueue(async void () =>
        {
            try
            {
                await PresetSwitcher.PresetSwitcher.ShowOverlay(
                    App.GetService<IThemeSelectorService>(),
                    settingsService,
                    preset.Value.PresetName,
                    preset.Value.PresetIcon,
                    preset.Value.PresetDesc);
            }
            catch (Exception ex)
            {
                await LogHelper.LogError(ex);
            }
        });
    }

    public event EventHandler<PresetId>? PresetChanged;

    private void ToggleRtss()
    {
        settingsService.RtssMetricsEnabled = !settingsService.RtssMetricsEnabled;
        settingsService.SaveSettings();
        if (!settingsService.RtssMetricsEnabled)
        {
            RtssHandler.ResetOsdText();
        }

        App.MainWindow.DispatcherQueue.TryEnqueue(async void () =>
        {
            try
            {
                await PresetSwitcher.PresetSwitcher.ShowOverlay(
                    App.GetService<IThemeSelectorService>(),
                    settingsService,
                    settingsService.RtssMetricsEnabled
                        ? "RTSS " + "Cooler_Service_Enabled/Content".GetLocalized()
                        : "RTSS " + "Cooler_Service_Disabled/Content".GetLocalized(),
                    "\uE7AC");
            }
            catch (Exception ex)
            {
                await LogHelper.LogError(ex);
            }
        });
    }

    public void Dispose()
    {
        Disable();
        if (_monitor != null)
        {
            _monitor.WindowMessageReceived -= OnWindowMessage;
        }

        GC.SuppressFinalize(this);
    }
}