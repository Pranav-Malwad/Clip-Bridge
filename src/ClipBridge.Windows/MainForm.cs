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
    private readonly Button _privateMode = new() { Text = "Private for 10 minutes", AutoSize = true };
    private readonly CheckBox _manualMode = new() { Text = "Manual mode (do not send copies automatically)", AutoSize = true };
    private readonly NotifyIcon _tray = new() { Text = "ClipBridge", Visible = true, Icon = SystemIcons.Application };
    private readonly System.Windows.Forms.Timer _clipboardTimer = new() { Interval = 400 };
    private readonly PairingStore _pairingStore;
    private readonly ClipboardCoordinator _coordinator;
    private readonly PrivacySettingsStore _privacyStore;
    private PrivacySettings _privacy;
    private ClipBridgeServer? _server;
    private DiscoveryService? _discovery;
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
        _privacyStore = new PrivacySettingsStore(Path.Combine(appData, "privacy.json"));
        _privacy = _privacyStore.Load();
        _secret = _pairingStore.LoadOrCreate();
        _coordinator = new ClipboardCoordinator(new WindowsClipboardService(), SendTextAsync, new SensitiveContentPolicy(), ForegroundProcess.TryGetName)
        {
            IsPaused = _privacy.IsPaused(DateTimeOffset.UtcNow),
            IsManualMode = _privacy.ManualMode
        };
        _coordinator.ContentBlocked += _ => BeginInvoke(() => SetStatus("Protected", "Sensitive text was not shared."));
        _manualMode.Checked = _privacy.ManualMode;

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
        buttons.Controls.Add(_privateMode);
        layout.Controls.Add(buttons);
        layout.Controls.Add(_manualMode);
        Controls.Add(layout);

        _pause.Click += (_, _) => TogglePause();
        _repair.Click += async (_, _) => await RotatePairingAsync();
        _privateMode.Click += (_, _) => EnablePrivateMode();
        _manualMode.CheckedChanged += (_, _) => SetManualMode(_manualMode.Checked);
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
        _qr.Visible = !_pairingStore.IsPaired;
        SetStatus("Starting…", _pairingStore.IsPaired ? "Reconnecting to your trusted phone…" : "Scan the code from the Android app.");
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open ClipBridge", null, (_, _) => RestoreWindow());
        menu.Items.Add("Exit", null, (_, _) => { _closing = true; Close(); });
        return menu;
    }

    public void RestoreWindow()
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
            {
                if (connected)
                {
                    _pairingStore.MarkPaired();
                    _qr.Visible = false;
                }
                SetStatus(connected ? "Connected" : "Reconnecting",
                    connected ? "Copy text on Windows or use Send to PC on Android." : "Looking for your trusted phone…");
            });
            await _server.StartAsync();
            _discovery = new DiscoveryService(_secret, PairingIdentity.FromSecret(_secret), Port);
            _discovery.Start();
            _clipboardTimer.Start();
            SetStatus(_pairingStore.IsPaired ? "Reconnecting" : "Waiting for phone",
                _pairingStore.IsPaired ? "Looking for your trusted phone…" : "Scan the pairing code from your phone.");
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
            if (_coordinator.IsPaused && !_privacy.IsPaused(DateTimeOffset.UtcNow))
            {
                _coordinator.IsPaused = false;
                _privacy = _privacy with { PauseUntil = null };
                _privacyStore.Save(_privacy);
                _pause.Text = "Pause sync";
                SetStatus("Waiting for phone", "Private Mode ended. Sync is enabled.");
            }
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
        _privacy = _privacy with { Paused = _coordinator.IsPaused, PauseUntil = null };
        _privacyStore.Save(_privacy);
        _pause.Text = _coordinator.IsPaused ? "Resume sync" : "Pause sync";
        SetStatus(_coordinator.IsPaused ? "Paused" : "Waiting for phone",
            _coordinator.IsPaused ? "Clipboard changes stay on this device." : "Sync is enabled.");
    }

    private void EnablePrivateMode()
    {
        _privacy = _privacy with { Paused = false, PauseUntil = DateTimeOffset.UtcNow.AddMinutes(10) };
        _privacyStore.Save(_privacy);
        _coordinator.IsPaused = true;
        _pause.Text = "Resume sync";
        SetStatus("Private Mode", "Sync is paused for 10 minutes. Clipboard changes stay on this device.");
    }

    private void SetManualMode(bool enabled)
    {
        _privacy = _privacy with { ManualMode = enabled };
        _privacyStore.Save(_privacy);
        _coordinator.IsManualMode = enabled;
        if (enabled) SetStatus("Manual mode", "Automatic Windows clipboard sharing is off.");
    }

    private async Task RotatePairingAsync()
    {
        _clipboardTimer.Stop();
        if (_server is not null)
        {
            await _server.DisposeAsync();
        }
        if (_discovery is not null)
        {
            await _discovery.DisposeAsync();
            _discovery = null;
        }

        _secret = _pairingStore.Rotate();
        RenderPairingCode();
        _qr.Visible = true;
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
        if (_discovery is not null)
        {
            await _discovery.DisposeAsync();
        }
    }
}
