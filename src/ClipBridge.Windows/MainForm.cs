using System.Security.Cryptography;
using System.Runtime.InteropServices;
using ClipBridge.Protocol;
using QRCoder;

namespace ClipBridge.Windows;

public sealed class MainForm : Form
{
    private const int Port = 24891;
    private readonly Label _status = new() { AutoSize = true, Font = new Font(SystemFonts.DefaultFont.FontFamily, 11, FontStyle.Bold) };
    private readonly Label _detail = new() { AutoSize = true, MaximumSize = new Size(430, 0) };
    private readonly PictureBox _qr = new() { Width = 280, Height = 280, SizeMode = PictureBoxSizeMode.Zoom };
    private readonly Button _pause = new() { Text = "Pause sync", AutoSize = true };
    private readonly Button _repair = new() { Text = "Pair a new phone", AutoSize = true };
    private readonly NotifyIcon _tray = new() { Text = "ClipBridge", Visible = true, Icon = SystemIcons.Application };
    private readonly System.Windows.Forms.Timer _clipboardTimer = new() { Interval = 400 };
    private readonly PairingStore _pairingStore;
    private readonly ClipboardCoordinator _coordinator;
    private ClipBridgeServer? _server;
    private byte[] _secret = [];
    private bool _closing;

    public MainForm()
    {
        Text = "ClipBridge";
        Width = 520;
        Height = 570;
        MinimumSize = new Size(500, 550);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipBridge");
        _pairingStore = new PairingStore(Path.Combine(appData, "pairing.dat"), new DpapiSecretProtector());
        _secret = _pairingStore.LoadOrCreate();
        _coordinator = new ClipboardCoordinator(new WindowsClipboardService(), SendTextAsync);

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(24),
            AutoScroll = true
        };
        layout.Controls.Add(new Label { Text = "ClipBridge", AutoSize = true, Font = new Font(SystemFonts.DefaultFont.FontFamily, 22, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) });
        layout.Controls.Add(_status);
        layout.Controls.Add(_detail);
        layout.Controls.Add(_qr);
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.Add(_pause);
        buttons.Controls.Add(_repair);
        layout.Controls.Add(buttons);
        Controls.Add(layout);

        _pause.Click += (_, _) => TogglePause();
        _repair.Click += async (_, _) => await RotatePairingAsync();
        _clipboardTimer.Tick += async (_, _) => await PollClipboardAsync();
        _tray.DoubleClick += (_, _) => RestoreWindow();
        _tray.ContextMenuStrip = BuildTrayMenu();
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
        };
        FormClosing += OnFormClosing;
        Shown += async (_, _) => await StartServerAsync();

        RenderPairingCode();
        SetStatus("Starting…", "Keep this window open and scan the code from the Android app.");
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open ClipBridge", null, (_, _) => RestoreWindow());
        menu.Items.Add("Exit", null, (_, _) => { _closing = true; Close(); });
        return menu;
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private async Task StartServerAsync()
    {
        try
        {
            _server = new ClipBridgeServer(_secret, PairingIdentity.FromSecret(_secret), Port);
            _server.TextReceived += text => BeginInvoke(() =>
            {
                if (_coordinator.ApplyRemoteText(text))
                {
                    SetStatus("Connected", "Text received from your phone and copied to Windows.");
                }
            });
            _server.ConnectionChanged += connected => BeginInvoke(() =>
                SetStatus(connected ? "Connected" : "Waiting for phone",
                    connected ? "Copy text on Windows or use Send to PC on Android." : "Scan the pairing code from your phone."));
            await _server.StartAsync();
            _clipboardTimer.Start();
            SetStatus("Waiting for phone", "Scan the pairing code from your phone.");
        }
        catch (Exception error)
        {
            SetStatus("Could not start", $"Port {Port} is unavailable: {error.Message}");
        }
    }

    private async Task SendTextAsync(string text)
    {
        if (_server is not null)
        {
            await _server.SendTextAsync(text);
        }
    }

    private async Task PollClipboardAsync()
    {
        try
        {
            await _coordinator.HandleLocalClipboardChangedAsync();
        }
        catch (ExternalException)
        {
            // Another application can briefly lock the clipboard; the next poll retries.
        }
    }

    private void TogglePause()
    {
        _coordinator.IsPaused = !_coordinator.IsPaused;
        _pause.Text = _coordinator.IsPaused ? "Resume sync" : "Pause sync";
        SetStatus(_coordinator.IsPaused ? "Paused" : "Waiting for phone",
            _coordinator.IsPaused ? "Clipboard changes stay on this device." : "Sync is enabled.");
    }

    private async Task RotatePairingAsync()
    {
        _clipboardTimer.Stop();
        if (_server is not null)
        {
            await _server.DisposeAsync();
        }

        _secret = _pairingStore.Rotate();
        RenderPairingCode();
        await StartServerAsync();
    }

    private void RenderPairingCode()
    {
        var pairingUri = PairingUri.Format(new PairingInfo(
            Environment.MachineName, NetworkAddress.FindLanIpv4(), Port, _secret,
            PairingIdentity.FromSecret(_secret), DateTimeOffset.UtcNow.AddMinutes(2)));
        using var data = QRCodeGenerator.GenerateQrCode(pairingUri, QRCodeGenerator.ECCLevel.Q);
        using var code = new QRCode(data);
        var old = _qr.Image;
        _qr.Image = code.GetGraphic(8, Color.FromArgb(18, 24, 38), Color.White, true);
        old?.Dispose();
        _qr.AccessibleDescription = pairingUri;
    }

    private void SetStatus(string title, string detail)
    {
        _status.Text = title;
        _detail.Text = detail;
        _tray.Text = $"ClipBridge — {title}";
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_closing && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _clipboardTimer.Stop();
        _tray.Visible = false;
        if (_server is not null)
        {
            await _server.DisposeAsync();
        }
    }
}
