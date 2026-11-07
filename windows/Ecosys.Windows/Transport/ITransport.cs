namespace Ecosys.Windows.Transport;

public sealed record BluetoothDeviceInfo(string Id, string Name, string Detail);

public interface ITransport : IAsyncDisposable
{
    event EventHandler<string>? StatusChanged;
    event EventHandler<string>? MessageReceived;
    event EventHandler<IReadOnlyList<BluetoothDeviceInfo>>? DevicesChanged;

    bool IsConnected { get; }

    Task<IReadOnlyList<BluetoothDeviceInfo>> ScanAsync(CancellationToken cancellationToken = default);
    Task ConnectAsync(BluetoothDeviceInfo device, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task SendAsync(string message, CancellationToken cancellationToken = default);
}
