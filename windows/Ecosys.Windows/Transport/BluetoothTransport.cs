using Ecosys.Windows.Protocol;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Ecosys.Windows.Transport;

public sealed record BluetoothDeviceInfo(string Id, string Name);

public sealed class BluetoothTransport : ITransport
{
    private StreamSocket? socket;
    private DataWriter? writer;
    private bool running;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<IReadOnlyList<BluetoothDeviceInfo>>? DevicesChanged;

    public async Task<IReadOnlyList<BluetoothDeviceInfo>> ScanAsync(CancellationToken cancellationToken = default)
    {
        running = true;
        StatusChanged?.Invoke(this, "Bluetooth-Geräte werden gesucht …");

        var selector = RfcommDeviceService.GetDeviceSelector(
            RfcommServiceId.FromUuid(BluetoothTransportUuids.ServiceUuid));

        var devices = await DeviceInformation.FindAllAsync(selector);
        var result = devices
            .Select(d => new BluetoothDeviceInfo(d.Id, string.IsNullOrWhiteSpace(d.Name) ? "Unbekanntes Gerät" : d.Name))
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();
        DevicesChanged?.Invoke(this, result);
        StatusChanged?.Invoke(this, result.Count == 0
            ? "Keine Ecosys-Geräte gefunden"
            : $"{result.Count} Ecosys-Gerät{(result.Count == 1 ? "" : "e")} gefunden");

        return result;
    }

    public async Task ConnectAsync(BluetoothDeviceInfo deviceInfo, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();

        StatusChanged?.Invoke(this, $"Verbinde mit {deviceInfo.Name} …");

        var service = await RfcommDeviceService.FromIdAsync(deviceInfo.Id);
        if (service is null)
            throw new InvalidOperationException("Der Bluetooth-Dienst ist nicht mehr verfügbar.");

        var access = await service.RequestAccessAsync();
        if (access != DeviceAccessStatus.Allowed)
            throw new UnauthorizedAccessException(
                $"Windows hat den Zugriff auf den Bluetooth-Dienst nicht freigegeben ({access}).");

        socket = new StreamSocket();
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            await socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName)
                .AsTask(connectCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            socket.Dispose();
            socket = null;
            throw new TimeoutException("Die Bluetooth-Verbindung konnte innerhalb von 15 Sekunden nicht aufgebaut werden.");
        }

        writer = new DataWriter(socket.OutputStream);
        StatusChanged?.Invoke(this, $"Verbunden mit {deviceInfo.Name}");

        _ = ReadLoopAsync(socket.InputStream);
        await SendAsync(
            EcosysMessage.Hello($"windows-{Environment.MachineName}", Environment.MachineName, "windows").ToJsonString(),
            cancellationToken);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var devices = await ScanAsync(cancellationToken);
        if (devices.Count > 0)
            await ConnectAsync(devices[0], cancellationToken);
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        if (writer is null)
            throw new InvalidOperationException("Keine Bluetooth-Verbindung aktiv.");

        writer.WriteString(message + "\n");
        await writer.StoreAsync().AsTask(cancellationToken);
    }

    public async Task DisconnectAsync()
    {
        running = false;
        writer?.Dispose();
        writer = null;
        socket?.Dispose();
        socket = null;
        await Task.CompletedTask;
    }

    private async Task ReadLoopAsync(IInputStream input)
    {
        using var reader = new DataReader(input) { InputStreamOptions = InputStreamOptions.Partial };
        var buffer = new List<byte>();

        try
        {
            while (running)
            {
                await reader.LoadAsync(1);
                var b = reader.ReadByte();
                if (b == (byte)'\n')
                {
                    MessageReceived?.Invoke(this, System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
                    buffer.Clear();
                }
                else
                {
                    buffer.Add(b);
                    if (buffer.Count > 256 * 1024) buffer.Clear();
                }
            }
        }
        catch (Exception ex)
        {
            if (running) StatusChanged?.Invoke(this, $"Bluetooth-Verbindung beendet: {ex.Message}");
        }
    }

    public ValueTask DisposeAsync()
    {
        running = false;
        writer?.Dispose();
        writer = null;
        socket?.Dispose();
        socket = null;
        return ValueTask.CompletedTask;
    }
}

internal static class BluetoothTransportUuids
{
    public static readonly Guid ServiceUuid =
        Guid.Parse("6e6f4d4f-0001-4d4f-4d4f-45434f595300");
}
