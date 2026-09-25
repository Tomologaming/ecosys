using Ecosys.Windows.Transport;

var transport = new BluetoothTransport();
transport.StatusChanged += (_, status) => Console.WriteLine($"[Ecosys] {status}");
await transport.StartAsync();
Console.WriteLine("Ecosys Windows client foundation started.");
await transport.DisposeAsync();
