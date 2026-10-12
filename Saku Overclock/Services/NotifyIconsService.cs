using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using H.NotifyIcon;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Shared;
using Saku_Overclock.Shared.Models;
using Saku_Overclock.Views;
using Icon = System.Drawing.Icon;

namespace Saku_Overclock.Services;

public partial class NotifyIconsService(IpcConnectionService ipc)
    : SimpleIpcSettingsBase<List<NiIconsElements>>(ipc, "NotifyIcons", IpcJsonContext.Default.ListNiIconsElements, [])
        , INotifyIconsService
{
    public List<NiIconsElements> Elements
    {
        get => Get(s => s);
        set => Set(cache => { cache.Clear(); cache.AddRange(value); });
    }

    public void UpdateTrayMonIcons()
    {
        // ignored, // TODO: add IPC call
    }

    public void LoadSettings() => _ = LoadSettingsAsync();

    public void SaveSettings() => Save();
}