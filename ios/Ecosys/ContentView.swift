import SwiftUI
import UIKit

struct ContentView: View {
    @StateObject private var bluetooth = BluetoothManager()

    var body: some View {
        ZStack {
            Color(red: 0.91, green: 0.93, blue: 0.94).ignoresSafeArea()

            VStack(spacing: 0) {
                hero
                ScrollView {
                    VStack(alignment: .leading, spacing: 18) {
                        statusCard
                        deviceCard
                        nearbySection
                        testSection
                        footer
                    }
                    .padding(20)
                }
            }
            .background(.white)
            .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
            .padding(16)
        }
        .preferredColorScheme(.light)
    }

    private var hero: some View {
        ZStack(alignment: .bottomLeading) {
            LinearGradient(
                colors: [Color(red: 0.07, green: 0.23, blue: 0.37), Color(red: 0.11, green: 0.43, blue: 0.31)],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )
            VStack(alignment: .leading, spacing: 5) {
                Text("Ecosys")
                    .font(.system(size: 34, weight: .bold, design: .rounded))
                    .foregroundStyle(.white)
                Text("PRIVATE. DIRECT. YOURS.")
                    .font(.system(size: 12, weight: .semibold))
                    .tracking(2)
                    .foregroundStyle(.white.opacity(0.82))
            }
            .padding(22)
        }
        .frame(height: 138)
    }

    private var statusCard: some View {
        HStack(spacing: 10) {
            Circle()
                .fill(bluetooth.isReady ? Color.green : Color.orange)
                .frame(width: 9, height: 9)
            Text(bluetooth.status)
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(.white)
            Spacer()
        }
        .padding(14)
        .background(Color(red: 0.09, green: 0.20, blue: 0.29))
        .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
    }

    private var deviceCard: some View {
        VStack(alignment: .leading, spacing: 8) {
            label("THIS DEVICE")
            HStack {
                Image(systemName: "iphone")
                    .font(.system(size: 25))
                    .foregroundStyle(Color(red: 0.18, green: 0.44, blue: 0.69))
                VStack(alignment: .leading, spacing: 2) {
                    Text(UIDevice.current.name)
                        .font(.system(size: 16, weight: .semibold))
                    Text("iPhone • Ecosys")
                        .font(.system(size: 12))
                        .foregroundStyle(.secondary)
                }
                Spacer()
            }
        }
    }

    private var nearbySection: some View {
        VStack(alignment: .leading, spacing: 10) {
            label("NEARBY DEVICES")

            if bluetooth.devices.isEmpty {
                Text("No Ecosys devices found yet.")
                    .font(.system(size: 14))
                    .foregroundStyle(.secondary)
                    .padding(.vertical, 10)
            } else {
                ForEach(bluetooth.devices) { device in
                    HStack {
                        Image(systemName: device.icon)
                            .foregroundStyle(Color(red: 0.18, green: 0.44, blue: 0.69))
                            .frame(width: 28)
                        VStack(alignment: .leading, spacing: 2) {
                            Text(device.name).font(.system(size: 15, weight: .semibold))
                            Text(device.detail).font(.system(size: 12)).foregroundStyle(.secondary)
                        }
                        Spacer()
                        Button("Connect") {
                            bluetooth.connect(to: device)
                        }
                        .buttonStyle(.borderedProminent)
                        .tint(Color(red: 0.18, green: 0.44, blue: 0.69))
                    }
                    .padding(13)
                    .background(Color(red: 0.96, green: 0.97, blue: 0.98))
                    .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
                }
            }

            Button {
                bluetooth.scan()
            } label: {
                Label(bluetooth.isScanning ? "Scanning…" : "Find nearby devices", systemImage: "dot.radiowaves.left.and.right")
                    .frame(maxWidth: .infinity)
            }
            .buttonStyle(.borderedProminent)
            .tint(Color(red: 0.18, green: 0.44, blue: 0.69))
        }
    }

    private var testSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            label("TEST CONNECTION")
            Button {
                bluetooth.sendHello()
            } label: {
                Label("Send Hello", systemImage: "paperplane.fill")
                    .frame(maxWidth: .infinity)
            }
            .buttonStyle(.borderedProminent)
            .tint(Color(red: 0.18, green: 0.56, blue: 0.36))

            if let lastMessage = bluetooth.lastMessage {
                Text(lastMessage)
                    .font(.system(size: 12, design: .monospaced))
                    .foregroundStyle(.white)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(14)
                    .background(Color(red: 0.09, green: 0.20, blue: 0.29))
                    .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
            }
        }
    }

    private var footer: some View {
        Text("Direct device-to-device. No cloud account required.")
            .font(.system(size: 11))
            .italic()
            .foregroundStyle(.secondary)
            .frame(maxWidth: .infinity, alignment: .center)
            .padding(.top, 4)
    }

    private func label(_ text: String) -> some View {
        Text(text)
            .font(.system(size: 11, weight: .bold))
            .tracking(1.2)
            .foregroundStyle(Color(red: 0.09, green: 0.20, blue: 0.29))
    }
}
