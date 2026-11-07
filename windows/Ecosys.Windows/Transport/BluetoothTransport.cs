using System.Text;
using Ecosys.Windows.Protocol;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace Ecosys.Windows.Transport;

public sealed class BluetoothTransport : ITransport
{
    private StreamSocket? socket;
    private DataWriter? writer;
    private GattDeviceService? gattService;
    private GattCharacteristic? gattTx;
    private GattCharacteristic? gattRx;
    private GattServiceProvider? gattServer;
    private GattLocalCharacteristic? gattServerTx;
    private GattLocalCharacteristic? gattServerRx;
    private BluetoothLEAdvertisementPublisher? discoveryPublisher;
    private readonly EcosysFrameAssembler incomingFrames = new();
    private bool running;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? MessageReceived;
    public event EventHandler<IReadOnlyList<BluetoothDeviceInfo>>? DevicesChanged;

    public bool IsConnected =>
        gattTx is not null ||
        writer is not null ||
        (gattServerRx is not null && gattServerRx.SubscribedClients.Count > 0);

    private void ReportStatus(string message) => StatusChanged?.Invoke(this, message);

    private void ReportMessage(ReadOnlySpan<byte> data)
    {
        foreach (var message in incomingFrames.Push(data))
            MessageReceived?.Invoke(this, message);
    }

    private async Task EnsureGattServerAsync()
    {
        if (gattServer is not null)
            return;

        try
        {
            var result = await GattServiceProvider.CreateAsync(BluetoothTransportUuids.BleServiceUuid);
            if (result.Error != BluetoothError.Success)
            {
                ReportStatus($"BLE-Server konnte nicht erstellt werden: {result.Error}");
                return;
            }

            gattServer = result.ServiceProvider;

            var txParameters = new GattLocalCharacteristicParameters
            {
                UserDescription = "Ecosys messages (in)",
                CharacteristicProperties =
                    GattCharacteristicProperties.Write | GattCharacteristicProperties.WriteWithoutResponse,
                WriteProtectionLevel = GattProtectionLevel.Plain
            };
            var txResult = await gattServer.Service.CreateCharacteristicAsync(
                BluetoothTransportUuids.BleTxUuid, txParameters);
            if (txResult.Error != BluetoothError.Success)
                throw new InvalidOperationException($"BLE-TX konnte nicht erstellt werden: {txResult.Error}");

            gattServerTx = txResult.Characteristic;
            gattServerTx.WriteRequested += OnGattServerWriteRequested;

            var rxParameters = new GattLocalCharacteristicParameters
            {
                UserDescription = "Ecosys messages (out)",
                CharacteristicProperties = GattCharacteristicProperties.Notify,
                ReadProtectionLevel = GattProtectionLevel.Plain
            };
            var rxResult = await gattServer.Service.CreateCharacteristicAsync(
                BluetoothTransportUuids.BleRxUuid, rxParameters);
            if (rxResult.Error != BluetoothError.Success)
                throw new InvalidOperationException($"BLE-RX konnte nicht erstellt werden: {rxResult.Error}");

            gattServerRx = rxResult.Characteristic;
            gattServerRx.SubscribedClientsChanged += OnGattServerSubscribedClientsChanged;

            var advertisingParameters = new GattServiceProviderAdvertisingParameters
            {
                IsDiscoverable = true,
                IsConnectable = true
            };
            gattServer.StartAdvertising(advertisingParameters);

            // GattServiceProvider advertises the service, but Windows does not expose the
            // local advertisement name through that API. Publish the Ecosys name separately
            // so iOS can identify the app even when the service UUID is missing from a packet.
            try
            {
                discoveryPublisher = new BluetoothLEAdvertisementPublisher();
                discoveryPublisher.Advertisement.LocalName = "Ecosys";
                discoveryPublisher.Advertisement.ServiceUuids.Add(BluetoothTransportUuids.BleServiceUuid);
                discoveryPublisher.Start();
            }
            catch (Exception ex)
            {
                discoveryPublisher = null;
                ReportStatus($"BLE-Name konnte nicht beworben werden: {ex.Message}");
            }

            ReportStatus("Ecosys BLE ist für andere Geräte sichtbar");
        }
        catch (Exception ex)
        {
            gattServer = null;
            gattServerTx = null;
            gattServerRx = null;
            ReportStatus($"BLE-Server konnte nicht gestartet werden: {ex.Message}");
        }
    }

    private void OnGattServerSubscribedClientsChanged(GattLocalCharacteristic sender, object args)
    {
        var count = sender.SubscribedClients.Count;
        ReportStatus(count == 0
            ? "Kein BLE-Gerät mehr verbunden"
            : $"{count} BLE-Gerät{(count == 1 ? "" : "e")} für Nachrichten verbunden");
    }

    private async void OnGattServerWriteRequested(
        GattLocalCharacteristic sender,
        GattWriteRequestedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var request = await args.GetRequestAsync();
            if (request is null)
                return;

            CryptographicBuffer.CopyToByteArray(request.Value, out var bytes);
            ReportMessage(bytes);

            if (request.Option == GattWriteOption.WriteWithResponse)
                request.Respond();
        }
        catch (Exception ex)
        {
            // Completing the deferral without Respond() answers the write with an error.
            ReportStatus($"BLE-Empfang fehlgeschlagen: {ex.Message}");
        }
        finally
        {
            deferral.Complete();
        }
    }

    public async Task<IReadOnlyList<BluetoothDeviceInfo>> ScanAsync(CancellationToken cancellationToken = default)
    {
        running = true;
        await EnsureGattServerAsync();
        ReportStatus("Bluetooth-Geräte werden gesucht …");

        var classicSelector = RfcommDeviceService.GetDeviceSelector(
            RfcommServiceId.FromUuid(BluetoothTransportUuids.ServiceUuid));
        var bleSelector = GattDeviceService.GetDeviceSelectorFromUuid(
            BluetoothTransportUuids.BleServiceUuid);

        var classic = await DeviceInformation.FindAllAsync(classicSelector).AsTask(cancellationToken);
        var ble = await DeviceInformation.FindAllAsync(bleSelector).AsTask(cancellationToken);

        // Both selectors are scoped to the private Ecosys RFCOMM/BLE service UUIDs.
        // Keep identities distinct: two devices may legitimately share a display name.
        var result = classic
            .Select(device => new BluetoothDeviceInfo(
                "rfcomm:" + device.Id,
                string.IsNullOrWhiteSpace(device.Name) ? "Ecosys-Gerät" : device.Name,
                "Bluetooth Classic · RFCOMM"))
            .Concat(ble.Select(device => new BluetoothDeviceInfo(
                "gatt:" + device.Id,
                string.IsNullOrWhiteSpace(device.Name) ? "Ecosys-BLE-Gerät" : device.Name,
                "Bluetooth Low Energy")))
            .GroupBy(device => device.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        DevicesChanged?.Invoke(this, result);
        ReportStatus(result.Count == 0
            ? "Keine Ecosys-Geräte gefunden"
            : $"{result.Count} Ecosys-Gerät{(result.Count == 1 ? "" : "e")} gefunden");

        return result;
    }

    public async Task ConnectAsync(BluetoothDeviceInfo deviceInfo, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();
        running = true;

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
        ReportStatus($"Verbinde mit {deviceInfo.Name} …");

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
        ReportStatus($"Verbunden mit {deviceInfo.Name}");

        _ = ReadLoopAsync(socket.InputStream);
        await SendAsync(
            EcosysMessage.Hello($"windows-{Environment.MachineName}", Environment.MachineName, "windows")
                .ToJsonString(),
            cancellationToken);
    }

    private async Task ConnectBleAsync(BluetoothDeviceInfo deviceInfo, CancellationToken cancellationToken)
    {
        var id = deviceInfo.Id["gatt:".Length..];
        ReportStatus($"Verbinde per BLE mit {deviceInfo.Name} …");

        // Force a fresh GATT lookup: Windows can keep stale service/characteristic
        // metadata between scans, which makes valid peer services appear missing.
        gattService = await GattDeviceService.FromIdAsync(id, GattSharingMode.Exclusive);
        if (gattService is null)
            throw new InvalidOperationException("Der Ecosys-BLE-Dienst ist nicht mehr verfügbar.");

        var access = await gattService.RequestAccessAsync();
        if (access != DeviceAccessStatus.Allowed)
            throw new UnauthorizedAccessException(
                $"Windows hat den Zugriff auf den BLE-Dienst nicht freigegeben ({access}).");

        var characteristicsResult = await gattService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
        if (characteristicsResult.Status != GattCommunicationStatus.Success)
            throw new InvalidOperationException(
                $"Windows konnte die BLE-Characteristics nicht lesen ({characteristicsResult.Status}).");

        var characteristics = characteristicsResult.Characteristics;
        gattTx = characteristics.FirstOrDefault(c => c.Uuid == BluetoothTransportUuids.BleTxUuid);
        gattRx = characteristics.FirstOrDefault(c => c.Uuid == BluetoothTransportUuids.BleRxUuid);

        if (gattTx is null)
            throw new InvalidOperationException(
                $"Der Ecosys-BLE-Dienst wurde gefunden, aber TX ({BluetoothTransportUuids.BleTxUuid}) fehlt. " +
                $"Gefundene Characteristics: {string.Join(", ", characteristics.Select(c => c.Uuid))}");

        if (gattRx is not null)
        {
            gattRx.ValueChanged += OnBleValueChanged;
            var notifyStatus = await gattRx.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);
            if (notifyStatus != GattCommunicationStatus.Success)
                ReportStatus($"BLE verbunden, Benachrichtigungen konnten nicht aktiviert werden ({notifyStatus}).");
        }

        ReportStatus($"Verbunden mit {deviceInfo.Name}");
        await SendAsync(
            EcosysMessage.Hello($"windows-{Environment.MachineName}", Environment.MachineName, "windows")
                .ToJsonString(),
            cancellationToken);
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

        if (gattServerRx is not null && gattServerRx.SubscribedClients.Count > 0)
        {
            var buffer = CryptographicBuffer.ConvertStringToBinary(message, BinaryStringEncoding.Utf8);
            var results = await gattServerRx.NotifyValueAsync(buffer).AsTask(cancellationToken);
            var failedResult = results.FirstOrDefault(result => result.Status != GattCommunicationStatus.Success);
            if (failedResult is not null)
                throw new InvalidOperationException($"BLE-Benachrichtigung fehlgeschlagen: {failedResult.Status}");
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
                // The peer may already be gone.
            }
        }

        gattTx = null;
        gattRx = null;
        gattService?.Dispose();
        gattService = null;
        incomingFrames.Reset();

        writer?.Dispose();
        writer = null;
        socket?.Dispose();
        socket = null;
    }

    private void OnBleValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        CryptographicBuffer.CopyToByteArray(args.CharacteristicValue, out var bytes);
        ReportMessage(bytes);
    }

    private async Task ReadLoopAsync(IInputStream input)
    {
        using var reader = new DataReader(input) { InputStreamOptions = InputStreamOptions.Partial };
        var buffer = new List<byte>();

        try
        {
            while (running)
            {
                var loaded = await reader.LoadAsync(4096);
                if (loaded == 0)
                    break;

                for (var index = 0u; index < loaded; index++)
                {
                    var value = reader.ReadByte();
                    if (value == (byte)'\n')
                    {
                        if (buffer.Count > 0)
                        {
                            ReportMessage(buffer.ToArray());
                            buffer.Clear();
                        }
                    }
                    else if (value != (byte)'\r' && value != 0)
                    {
                        buffer.Add(value);
                        if (buffer.Count > 256 * 1024)
                            buffer.Clear();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (running)
                ReportStatus($"Bluetooth-Verbindung beendet: {ex.Message}");
        }
    }

    public ValueTask DisposeAsync()
    {
        running = false;
        if (gattServerTx is not null)
            gattServerTx.WriteRequested -= OnGattServerWriteRequested;
        if (gattServerRx is not null)
            gattServerRx.SubscribedClientsChanged -= OnGattServerSubscribedClientsChanged;
        try
        {
            gattServer?.StopAdvertising();
            discoveryPublisher?.Stop();
        }
        catch
        {
            // Shutdown is best effort.
        }

        discoveryPublisher = null;
        gattServer = null;
        gattServerTx = null;
        gattServerRx = null;
        if (gattRx is not null)
            gattRx.ValueChanged -= OnBleValueChanged;
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
