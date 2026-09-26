using Ecosys.Windows.Protocol;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace Ecosys.Windows.Transport;

public sealed record BluetoothDeviceInfo(string Id, string Name);

public sealed class BluetoothTransport : ITransport
{
    private StreamSocket? socket;
    private DataWriter? writer;
    private GattDeviceService? gattService;
    private GattCharacteristic? gattTx;
    private GattCharacteristic? gattRx;
    private bool running;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<IReadOnlyList<BluetoothDeviceInfo>>? DevicesChanged;

    public async Task<IReadOnlyList<BluetoothDeviceInfo>> ScanAsync(CancellationToken cancellationToken = default)
    {
        running = true;
        StatusChanged?.Invoke(this, "Bluetooth-Geräte werden gesucht …");

        var classicSelector = RfcommDeviceService.GetDeviceSelector(
            RfcommServiceId.FromUuid(BluetoothTransportUuids.ServiceUuid));
        var bleSelector = GattDeviceService.GetDeviceSelectorFromUuid(
            BluetoothTransportUuids.BleServiceUuid);

        var classicTask = DeviceInformation.FindAllAsync(classicSelector).AsTask(cancellationToken);
        var bleTask = DeviceInformation.FindAllAsync(bleSelector).AsTask(cancellationToken);

        var classic = await classicTask;
        var ble = await bleTask;

        var result = classic
            .Select(d => new BluetoothDeviceInfo(
                "rfcomm:" + d.Id,
                string.IsNullOrWhiteSpace(d.Name) ? "Unbekanntes Gerät" : d.Name))
            .Concat(ble.Select(d => new BluetoothDeviceInfo(
                "gatt:" + d.Id,
                string.IsNullOrWhiteSpace(d.Name) ? "BLE-Gerät" : d.Name)))
            .GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        DevicesChanged?.Invoke(this, result);
        StatusChanged?.Invoke(this, result.Count == 0
            ? "Keine Ecosys-Geräte gefunden"
            : $"{result.Count} Ecosys-Gerät{(result.Count == 1 ? "" : "e")} gefunden");

        return result;
    }

    public async Task ConnectAsync(BluetoothDeviceInfo deviceInfo, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();

        if (deviceInfo.Id.StartsWith("gatt:", StringComparison.Ordinal))
        {
            await ConnectBleAsync(deviceInfo, cancellationToken);
            return;
        }

        await ConnectRfcommAsync(deviceInfo, cancellationToken);
    }

    private async Task ConnectRfcommAsync(BluetoothDeviceInfo deviceInfo, CancellationToken cancellationToken)
    {
        var id = deviceInfo.Id["rfcomm:".Length..];
        StatusChanged?.Invoke(this, $"Verbinde mit {deviceInfo.Name} …");

        var service = await RfcommDeviceService.FromIdAsync(id);
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

    private async Task ConnectBleAsync(BluetoothDeviceInfo deviceInfo, CancellationToken cancellationToken)
    {
        var id = deviceInfo.Id["gatt:".Length..];
        StatusChanged?.Invoke(this, $"Verbinde per BLE mit {deviceInfo.Name} …");

        gattService = await GattDeviceService.FromIdAsync(id);
        if (gattService is null)
            throw new InvalidOperationException("Der Ecosys-BLE-Dienst ist nicht mehr verfügbar.");

        var access = await gattService.RequestAccessAsync();
        if (access != DeviceAccessStatus.Allowed)
            throw new UnauthorizedAccessException(
                $"Windows hat den Zugriff auf den BLE-Dienst nicht freigegeben ({access}).");

        var characteristicsResult = await gattService.GetCharacteristicsAsync();
        var characteristics = characteristicsResult.Characteristics;
        gattTx = characteristics.FirstOrDefault(c => c.Uuid == BluetoothTransportUuids.BleTxUuid);
        gattRx = characteristics.FirstOrDefault(c => c.Uuid == BluetoothTransportUuids.BleRxUuid);

        if (gattTx is null)
            throw new InvalidOperationException("Die BLE-TX-Characteristic wurde nicht gefunden.");

        if (gattRx is not null)
        {
            gattRx.ValueChanged += OnBleValueChanged;
            var notifyStatus = await gattRx.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);
            if (notifyStatus != GattCommunicationStatus.Success)
                StatusChanged?.Invoke(this, $"BLE verbunden, Benachrichtigungen konnten nicht aktiviert werden ({notifyStatus}).");
        }

        StatusChanged?.Invoke(this, $"Verbunden mit {deviceInfo.Name}");
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
        if (gattTx is not null)
        {
            var buffer = CryptographicBuffer.ConvertStringToBinary(message, BinaryStringEncoding.Utf8);
            var status = await gattTx.WriteValueAsync(buffer, GattWriteOption.WriteWithResponse)
                .AsTask(cancellationToken);
            if (status != GattCommunicationStatus.Success)
                throw new InvalidOperationException($"BLE-Senden fehlgeschlagen: {status}");
            return;
        }

        if (writer is null)
            throw new InvalidOperationException("Keine Bluetooth-Verbindung aktiv.");

        writer.WriteString(message + "\n");
        await writer.StoreAsync().AsTask(cancellationToken);
    }

    public async Task DisconnectAsync()
    {
        running = false;

        if (gattRx is not null)
        {
            gattRx.ValueChanged -= OnBleValueChanged;
            try
            {
                await gattRx.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.None);
            }
            catch
            {
            }
        }

        gattTx = null;
        gattRx = null;
        gattService?.Dispose();
        gattService = null;

        writer?.Dispose();
        writer = null;
        socket?.Dispose();
        socket = null;
    }

    private void OnBleValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out var bytes);
        var message = System.Text.Encoding.UTF8.GetString(bytes);
        MessageReceived?.Invoke(this, message);
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
        if (gattRx is not null) gattRx.ValueChanged -= OnBleValueChanged;
        gattRx = null;
        gattTx = null;
        gattService?.Dispose();
        gattService = null;
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

    public static readonly Guid BleServiceUuid =
        Guid.Parse("6e6f4d4f-0002-4d4f-4d4f-45434f595300");

    public static readonly Guid BleTxUuid =
        Guid.Parse("6e6f4d4f-0003-4d4f-4d4f-45434f595300");

    public static readonly Guid BleRxUuid =
        Guid.Parse("6e6f4d4f-0004-4d4f-4d4f-45434f595300");
}
