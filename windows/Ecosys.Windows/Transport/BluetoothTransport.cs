using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Ecosys.Windows.Transport;

public sealed class BluetoothTransport : ITransport
{
    private StreamSocket? socket;
    private DataWriter? writer;
    private bool running;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? MessageReceived;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        running = true;
        StatusChanged?.Invoke(this, "Searching for nearby Bluetooth devices…");

        var devices = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelector());
        foreach (var info in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var device = await BluetoothDevice.FromIdAsync(info.Id);
                if (device is null) continue;

                var services = await device.GetRfcommServicesForIdAsync(
                    RfcommServiceId.FromUuid(BluetoothTransportUuids.ServiceUuid),
                    BluetoothCacheMode.Uncached);
                var service = services.Services.FirstOrDefault();
                if (service is null) continue;

                socket = new StreamSocket();
                await socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName);
                writer = new DataWriter(socket.OutputStream);
                StatusChanged?.Invoke(this, $"Connected to {info.Name}");
                _ = ReadLoopAsync(socket.InputStream);
                return;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"Bluetooth device skipped: {ex.Message}");
            }
        }

        StatusChanged?.Invoke(this, "No Ecosys Bluetooth service found.");
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        if (writer is null) return;
        writer.WriteString(message + "
");
        await writer.StoreAsync().AsTask(cancellationToken);
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
                if (b == (byte)'
')
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
            if (running) StatusChanged?.Invoke(this, $"Bluetooth read ended: {ex.Message}");
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
