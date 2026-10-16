import SwiftUI
import UIKit

struct ContentView: View {
    @StateObject private var bluetooth = BluetoothManager()
    @State private var showInfo = false

    private let ink = Color(red: 0.08, green: 0.13, blue: 0.20)
    private let mint = Color(red: 0.35, green: 0.91, blue: 0.72)

    var body: some View {
        ZStack {
            LinearGradient(colors: [Color(red: 0.04, green: 0.09, blue: 0.17),
                                    Color(red: 0.07, green: 0.17, blue: 0.25),
                                    Color(red: 0.08, green: 0.23, blue: 0.22)],
                           startPoint: .topLeading, endPoint: .bottomTrailing)
                .ignoresSafeArea()
            Circle().fill(mint.opacity(0.12)).frame(width: 260).blur(radius: 65)
                .offset(x: 150, y: -330)
            ScrollView(showsIndicators: false) {
                VStack(alignment: .leading, spacing: 20) {
                    header
                    statusCard
                    deviceCard
                    nearbyCard
                    helloCard
                    footer
                }
                .padding(.horizontal, 20)
                .padding(.top, 16)
                .padding(.bottom, 32)
            }
        }
        .preferredColorScheme(.dark)
        .tint(mint)
        .alert("Ecosys", isPresented: $showInfo) {
            Button("OK", role: .cancel) { }
        } message: {
            Text("Direkte Geräteverbindung. Wähle Ecosys im Teilen-Menü, um eine Datei an Windows zu senden.")
        }
    }

    private var header: some View {
        HStack(alignment: .center, spacing: 14) {
            ZStack {
                RoundedRectangle(cornerRadius: 20, style: .continuous)
                    .fill(LinearGradient(colors: [mint, Color(red: 0.22, green: 0.65, blue: 0.98)],
                                         startPoint: .topLeading, endPoint: .bottomTrailing))
                Image(systemName: "arrow.left.arrow.right")
                    .font(.system(size: 25, weight: .bold))
                    .foregroundStyle(ink)
            }
            .frame(width: 62, height: 62)
            VStack(alignment: .leading, spacing: 4) {
                Text("Ecosys").font(.system(size: 32, weight: .bold, design: .rounded))
                    .foregroundStyle(.white)
                Text("DEINE GERÄTE. DIREKT VERBUNDEN.")
                    .font(.system(size: 10, weight: .bold))
                    .tracking(1.4).foregroundStyle(.white.opacity(0.58))
            }
            Spacer()
            Button { showInfo = true; UIImpactFeedbackGenerator(style: .light).impactOccurred() } label: {
                Image(systemName: "info.circle").font(.system(size: 19, weight: .medium))
                    .foregroundStyle(.white.opacity(0.8)).frame(width: 42, height: 42)
                    .background(.ultraThinMaterial, in: Circle())
            }
            .buttonStyle(.plain)
        }
    }

    private var statusCard: some View {
        glassCard {
            HStack(spacing: 12) {
                ZStack {
                    Circle().fill((bluetooth.isReady ? mint : Color.orange).opacity(0.18)).frame(width: 42, height: 42)
                    Circle().fill(bluetooth.isReady ? mint : Color.orange).frame(width: 9, height: 9)
                }
                VStack(alignment: .leading, spacing: 4) {
                    Text(bluetooth.isReady ? "Bluetooth bereit" : "Bluetooth-Status")
                        .font(.system(size: 15, weight: .semibold)).foregroundStyle(.white)
                    Text(bluetooth.status).font(.system(size: 12)).foregroundStyle(.white.opacity(0.6))
                        .lineLimit(2)
                }
                Spacer()
                Image(systemName: bluetooth.isReady ? "checkmark.circle.fill" : "wave.3.right")
                    .font(.system(size: 20)).foregroundStyle(bluetooth.isReady ? mint : .orange)
            }
        }
    }

    private var deviceCard: some View {
        glassCard {
            HStack(spacing: 14) {
                Image(systemName: "iphone.gen3").font(.system(size: 28, weight: .medium))
                    .foregroundStyle(mint).frame(width: 54, height: 54)
                    .background(mint.opacity(0.12), in: RoundedRectangle(cornerRadius: 16))
                VStack(alignment: .leading, spacing: 5) {
                    Text("Dieses iPhone").font(.system(size: 16, weight: .semibold)).foregroundStyle(.white)
                    Text("Bereit zum Senden und Empfangen").font(.system(size: 12)).foregroundStyle(.white.opacity(0.58))
                }
                Spacer()
                Circle().fill(mint).frame(width: 7, height: 7)
            }
        }
    }

    private var nearbyCard: some View {
        glassCard {
            VStack(alignment: .leading, spacing: 16) {
                HStack {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("Geräte in der Nähe").font(.system(size: 19, weight: .bold)).foregroundStyle(.white)
                        Text("Nur Ecosys-kompatible Geräte").font(.system(size: 12)).foregroundStyle(.white.opacity(0.55))
                    }
                    Spacer()
                    Text("\(bluetooth.devices.count)")
                        .font(.system(size: 13, weight: .bold, design: .rounded)).foregroundStyle(mint)
                        .padding(.horizontal, 10).padding(.vertical, 6)
                        .background(mint.opacity(0.12), in: Capsule())
                }
                if bluetooth.devices.isEmpty {
                    VStack(spacing: 10) {
                        Image(systemName: "dot.radiowaves.left.and.right")
                            .font(.system(size: 27)).foregroundStyle(.white.opacity(0.35))
                        Text("Noch keine Geräte gefunden").font(.system(size: 13, weight: .medium)).foregroundStyle(.white.opacity(0.6))
                        Text("Starte die Suche, während Ecosys unter Windows geöffnet ist.")
                            .font(.system(size: 11)).foregroundStyle(.white.opacity(0.4)).multilineTextAlignment(.center)
                    }
                    .frame(maxWidth: .infinity).padding(.vertical, 18)
                } else {
                    ForEach(bluetooth.devices) { device in
                        HStack(spacing: 12) {
                            Image(systemName: device.icon).foregroundStyle(mint)
                                .frame(width: 42, height: 42)
                                .background(mint.opacity(0.12), in: RoundedRectangle(cornerRadius: 13))
                            VStack(alignment: .leading, spacing: 4) {
                                Text(device.name).font(.system(size: 14, weight: .semibold)).foregroundStyle(.white)
                                Text(device.detail).font(.system(size: 11)).foregroundStyle(.white.opacity(0.5))
                            }
                            Spacer()
                            Button {
                                UIImpactFeedbackGenerator(style: .medium).impactOccurred()
                                bluetooth.connect(to: device)
                            } label: {
                                Text("Verbinden").font(.system(size: 12, weight: .semibold))
                                    .foregroundStyle(ink).padding(.horizontal, 13).padding(.vertical, 9)
                                    .background(mint, in: Capsule())
                            }.buttonStyle(.plain)
                        }
                        .padding(10).background(.white.opacity(0.045), in: RoundedRectangle(cornerRadius: 16))
                    }
                }
                Button {
                    UIImpactFeedbackGenerator(style: .medium).impactOccurred()
                    bluetooth.scan()
                } label: {
                    HStack(spacing: 9) {
                        Image(systemName: bluetooth.isScanning ? "hourglass" : "arrow.clockwise")
                        Text(bluetooth.isScanning ? "Suche läuft …" : "Geräte suchen")
                    }
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundStyle(ink).frame(maxWidth: .infinity).padding(.vertical, 15)
                    .background(LinearGradient(colors: [mint, Color(red: 0.43, green: 0.77, blue: 0.98)],
                                               startPoint: .leading, endPoint: .trailing), in: RoundedRectangle(cornerRadius: 15))
                }
                .buttonStyle(.plain)
            }
        }
    }

    private var helloCard: some View {
        glassCard {
            HStack(spacing: 12) {
                Image(systemName: "paperplane.fill").font(.system(size: 20))
                    .foregroundStyle(mint).frame(width: 46, height: 46)
                    .background(mint.opacity(0.12), in: RoundedRectangle(cornerRadius: 14))
                VStack(alignment: .leading, spacing: 4) {
                    Text("Verbindung testen").font(.system(size: 15, weight: .semibold)).foregroundStyle(.white)
                    Text(bluetooth.lastMessage ?? "Sende eine kurze Testnachricht an das verbundene Gerät.")
                        .font(.system(size: 11)).foregroundStyle(.white.opacity(0.55)).lineLimit(2)
                }
                Spacer(minLength: 4)
                Button {
                    UIImpactFeedbackGenerator(style: .light).impactOccurred()
                    bluetooth.sendHello()
                } label: {
                    Image(systemName: "arrow.up.right").font(.system(size: 15, weight: .bold))
                        .foregroundStyle(.white).frame(width: 40, height: 40)
                        .background(.white.opacity(0.12), in: Circle())
                }.buttonStyle(.plain)
            }
        }
    }

    private var footer: some View {
        HStack(spacing: 6) {
            Image(systemName: "lock.shield").foregroundStyle(mint.opacity(0.8))
            Text("Direkte Verbindung · Kein Cloud-Konto").foregroundStyle(.white.opacity(0.42))
        }
        .font(.system(size: 11, weight: .medium)).frame(maxWidth: .infinity).padding(.top, 4)
    }

    @ViewBuilder
    private func glassCard<Content: View>(@ViewBuilder content: () -> Content) -> some View {
        let card = content()
            .padding(17)
            .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 24, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: 24, style: .continuous)
                .stroke(.white.opacity(0.11), lineWidth: 1))
        if #available(iOS 26.0, *) {
            card.glassEffect(.regular, in: RoundedRectangle(cornerRadius: 24, style: .continuous))
        } else {
            card
        }
    }
}
