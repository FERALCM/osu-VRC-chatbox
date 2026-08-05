using System.Drawing;
using System.Windows.Forms;

namespace OsuVrcChatbox.App.Services;

/// <summary>
/// System-tray presence (plan §22) via WinForms <see cref="NotifyIcon"/> (BCL, no extra dependency).
/// Exposes callbacks for Show, toggle-pause, clear-chatbox, and exit. Uses a stock icon so no asset
/// needs bundling.
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _pauseItem;

    public event Action? ShowRequested;
    public event Action? TogglePauseRequested;
    public event Action? ClearRequested;
    public event Action? ExitRequested;

    public TrayService()
    {
        _pauseItem = new ToolStripMenuItem("Pause output", null, (_, _) => TogglePauseRequested?.Invoke());

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Show window", null, (_, _) => ShowRequested?.Invoke()));
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new ToolStripMenuItem("Clear chatbox", null, (_, _) => ClearRequested?.Invoke()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitRequested?.Invoke()));

        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "osu! → VRChat chatbox",
            Visible = true,
            ContextMenuStrip = menu
        };
        _icon.DoubleClick += (_, _) => ShowRequested?.Invoke();
    }

    /// <summary>Reflects output state in the menu and tooltip.</summary>
    public void SetOutputEnabled(bool enabled)
    {
        _pauseItem.Text = enabled ? "Pause output" : "Resume output";
        _icon.Text = enabled ? "osu! → VRChat chatbox (active)" : "osu! → VRChat chatbox (paused)";
    }

    public void ShowBalloon(string title, string message) =>
        _icon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
