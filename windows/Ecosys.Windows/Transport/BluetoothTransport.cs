namespace Ecosys.Windows.Transport;

public sealed class BluetoothTransport : ITransport
{
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? MessageReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        StatusChanged?.Invoke(this, "Bluetooth transport ready; discovery/connection is next.");
        return Task.CompletedTask;
    }

    public Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        // RFCOMM framing will be implemented in the next Windows transport slice.
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
