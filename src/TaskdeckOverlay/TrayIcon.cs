using System.Windows;
using Forms = System.Windows.Forms;

namespace TaskdeckOverlay;

/// <summary>
/// Notification-area icon. Lets the user hide the overlay and bring it back, and shows
/// a balloon for task events while the overlay is hidden.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _toggleItem;
    private readonly System.Drawing.Icon _image;

    public event Action? ToggleRequested;
    public event Action? ResetPositionRequested;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        using (var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico")).Stream)
        {
            // Ask for the small size so the tray gets the 16px image instead of a downscaled one.
            _image = new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
        }

        _toggleItem = new Forms.ToolStripMenuItem("Hide overlay", null, (_, _) => ToggleRequested?.Invoke());

        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add(_toggleItem);
        _menu.Items.Add("Reset position", null, (_, _) => ResetPositionRequested?.Invoke());
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        _icon = new Forms.NotifyIcon
        {
            Icon = _image,
            Text = "taskdeck-overlay",
            ContextMenuStrip = _menu,
            Visible = true
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                ToggleRequested?.Invoke();
            }
        };
    }

    public void SetOverlayVisible(bool visible) => _toggleItem.Text = visible ? "Hide overlay" : "Show overlay";

    public void ShowBalloon(string title, string text) =>
        _icon.ShowBalloonTip(5000, title, text, Forms.ToolTipIcon.None);

    public void Dispose()
    {
        // Hide first so the icon does not linger in the tray until the mouse passes over it.
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _image.Dispose();
    }
}
