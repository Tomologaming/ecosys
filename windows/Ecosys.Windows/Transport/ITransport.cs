namespace Ecosys.Windows.Transport;

public interface ITransport : IAsyncDisposable
{
    event EventHandler<string>? StatusChanged;
    event EventHandler<string>? MessageReceived;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task SendAsync(string message, CancellationToken cancellationToken = default);
}
