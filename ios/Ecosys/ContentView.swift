import SwiftUI
import UIKit

struct ContentView: View {
    @StateObject private var bluetooth = BluetoothManager()

    private let ink = Color(red: 0.08, green: 0.16, blue: 0.23)
    private let blue = Color(red: 0.20, green: 0.48, blue: 0.82)
    private let green = Color(red: 0.15, green: 0.65, blue: 0.46)

    var body: some View {
        ZStack {
            background

            ScrollView(showsIndicators: false) {
                VStack(alignment: .leading, spacing: 18) {
                    hero
                    statusCard
                    deviceCard
                    nearbySection
                    testSection
                    footer
                }
                .padding(.horizontal, 20)
                .padding(.top, 12)
                .padding(.bottom, 30)
            }
        }
        .preferredColorScheme(.light)
        .tint(blue)
    }

    private var background: some View {
        ZStack {
            LinearGradient(
                colors: [
                    Color(red: 0.89, green: 0.94, blue: 0.98),
                    Color(red: 0.91, green: 0.96, blue: 0.93),
                    Color(red: 0.96, green: 0.95, blue: 0.91)
                ],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )
            Circle()
                .fill(blue.opacity(0.15))
                .frame(width: 250, height: 250)
                .blur(radius: 55)
                .offset(x: 150, y: -300)
            Circle()
                .fill(green.opacity(0.13))
                .frame(width: 240, height: 240)
                .blur(radius: 60)
                .offset(x: -150, y: 170)
        }
        .ignoresSafeArea()
    }

    private var hero: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 6) {
                    Text("Ecosys")
                        .font(.system(size: 38, weight: .bold, design: .rounded))
                        .tracking(-1.2)
                    Text("PRIVATE. DIRECT. YOURS.")
                        .font(.system(size: 10, weight: .bold, design: .rounded))
                        .tracking(2.1)
                        .foregroundStyle(ink.opacity(0.62))
                }
                Spacer()
                Image(systemName: "dot.radiowaves.left.and.right")
                    .font(.system(size: 22, weight: .semibold))
                    .foregroundStyle(blue)
                    .frame(width: 54, height: 54)
                    .liquidGlass(cornerRadius: 18)
                    .accessibilityHidden(true)
            }

            Text("Your devices, connected directly.")
                .font(.system(size: 16, weight: .medium))
                .foregroundStyle(ink.opacity(0.78))
        }
        .foregroundStyle(ink)
        .padding(22)
        .frame(maxWidth: .infinity, alignment: .leading)
        .liquidGlass(cornerRadius: 28)
    }

    private var statusCard: some View {
        HStack(spacing: 12) {
            ZStack {
                Circle()
                    .fill((bluetooth.isReady ? green : Color.orange).opacity(0.16))
                    .frame(width: 42, height: 42)
                Circle()
                    .fill(bluetooth.isReady ? green : Color.orange)
                    .frame(width: 10, height: 10)
            }
            VStack(alignment: .leading, spacing: 3) {
                Text(bluetooth.isReady ? "Bluetooth ready" : "Bluetooth status")
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundStyle(ink)
                Text(bluetooth.status)
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(ink.opacity(0.65))
                    .fixedSize(horizontal: false, vertical: true)
            }
            Spacer(minLength: 0)
            Image(systemName: bluetooth.isReady ? "checkmark.circle.fill" : "wave.3.right")
                .font(.system(size: 20))
                .foregroundStyle(bluetooth.isReady ? green : Color.orange)
        }
        .padding(16)
        .liquidGlass(cornerRadius: 22)
    }

    private var deviceCard: some View {
        VStack(alignment: .leading, spacing: 13) {
            sectionLabel("THIS DEVICE")

            HStack(spacing: 13) {
                Image(systemName: "iphone.gen3")
                    .font(.system(size: 25, weight: .medium))
                    .foregroundStyle(blue)
                    .frame(width: 52, height: 52)
                    .liquidGlass(cornerRadius: 17)
                VStack(alignment: .leading, spacing: 4) {
                    Text(UIDevice.current.name)
                        .font(.system(size: 16, weight: .semibold))
                        .foregroundStyle(ink)
                    Text("iPhone · Ecosys")
                        .font(.system(size: 13))
                        .foregroundStyle(ink.opacity(0.60))
                }
                Spacer()
                Image(systemName: "checkmark.seal.fill")
                    .foregroundStyle(green)
                    .font(.system(size: 18))
            }
            .padding(16)
            .liquidGlass(cornerRadius: 22)
        }
    }

    private var nearbySection: some View {
        VStack(alignment: .leading, spacing: 12) {
            sectionLabel("NEARBY DEVICES")

            if bluetooth.devices.isEmpty {
                VStack(spacing: 12) {
                    Image(systemName: "dot.radiowaves.left.and.right")
                        .font(.system(size: 28, weight: .light))
                        .foregroundStyle(blue)
                    Text("No devices found yet")
                        .font(.system(size: 15, weight: .semibold))
                        .foregroundStyle(ink)
                    Text("Scan nearby for another Ecosys device.")
                        .font(.system(size: 13))
                        .foregroundStyle(ink.opacity(0.62))
                        .multilineTextAlignment(.center)
                }
                .frame(maxWidth: .infinity)
                .padding(.vertical, 24)
                .padding(.horizontal, 16)
                .liquidGlass(cornerRadius: 22)
            } else {
                ForEach(bluetooth.devices) { device in
                    HStack(spacing: 12) {
                        Image(systemName: device.icon)
                            .font(.system(size: 20))
                            .foregroundStyle(blue)
                            .frame(width: 44, height: 44)
                            .liquidGlass(cornerRadius: 14)
                        VStack(alignment: .leading, spacing: 4) {
                            Text(device.name)
                                .font(.system(size: 15, weight: .semibold))
                                .foregroundStyle(ink)
                            Text(device.detail)
                                .font(.system(size: 12))
                                .foregroundStyle(ink.opacity(0.62))
                        }
                        Spacer(minLength: 4)
                        Button {
                            tapHaptic()
                            bluetooth.connect(to: device)
                        } label: {
                            Text("Connect")
                                .font(.system(size: 13, weight: .semibold))
                                .padding(.horizontal, 14)
                                .padding(.vertical, 10)
                                .liquidGlass(cornerRadius: 15)
                        }
                        .buttonStyle(.plain)
                        .foregroundStyle(blue)
                    }
                    .padding(13)
                    .liquidGlass(cornerRadius: 22)
                }
            }

            Button {
                tapHaptic()
                bluetooth.scan()
            } label: {
                HStack(spacing: 10) {
                    Image(systemName: bluetooth.isScanning ? "dot.radiowaves.left.and.right" : "magnifyingglass")
                    Text(bluetooth.isScanning ? "Scanning…" : "Find nearby devices")
                }
                .font(.system(size: 15, weight: .semibold))
                .frame(maxWidth: .infinity)
                .padding(.vertical, 16)
                .liquidGlass(cornerRadius: 19)
            }
            .buttonStyle(.plain)
            .foregroundStyle(ink)
            .disabled(bluetooth.isScanning)
            .opacity(bluetooth.isScanning ? 0.72 : 1)
        }
    }

    private var testSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            sectionLabel("TEST CONNECTION")

            Button {
                tapHaptic()
                bluetooth.sendHello()
            } label: {
                HStack(spacing: 10) {
                    Image(systemName: "paperplane.fill")
                    Text("Send Hello")
                    Spacer()
                    Image(systemName: "arrow.up.right")
                        .font(.system(size: 12, weight: .bold))
                }
                .font(.system(size: 15, weight: .semibold))
                .foregroundStyle(.white)
                .padding(17)
                .frame(maxWidth: .infinity)
                .background(
                    LinearGradient(
                        colors: [Color(red: 0.13, green: 0.55, blue: 0.43), Color(red: 0.10, green: 0.42, blue: 0.37)],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                    ),
                    in: RoundedRectangle(cornerRadius: 20, style: .continuous)
                )
                .liquidGlass(cornerRadius: 20)
            }
            .buttonStyle(.plain)

            if let lastMessage = bluetooth.lastMessage {
                HStack(alignment: .top, spacing: 10) {
                    Image(systemName: "text.bubble.fill")
                        .foregroundStyle(blue)
                    Text(lastMessage)
                        .font(.system(size: 12, design: .monospaced))
                        .foregroundStyle(ink.opacity(0.82))
                        .frame(maxWidth: .infinity, alignment: .leading)
                }
                .padding(15)
                .liquidGlass(cornerRadius: 18)
                .transition(.opacity.combined(with: .move(edge: .top)))
            }
        }
        .animation(.snappy(duration: 0.25), value: bluetooth.lastMessage)
    }

    private var footer: some View {
        HStack(spacing: 7) {
            Image(systemName: "lock.shield")
            Text("Device-to-device. No cloud account required.")
        }
        .font(.system(size: 11, weight: .medium))
        .foregroundStyle(ink.opacity(0.55))
        .frame(maxWidth: .infinity)
        .padding(.top, 6)
    }

    private func sectionLabel(_ text: String) -> some View {
        Text(text)
            .font(.system(size: 11, weight: .bold, design: .rounded))
            .tracking(1.5)
            .foregroundStyle(ink.opacity(0.62))
            .padding(.leading, 3)
    }

    private func tapHaptic() {
        let generator = UIImpactFeedbackGenerator(style: .light)
        generator.prepare()
        generator.impactOccurred()
    }
}

private extension View {
    @ViewBuilder
    func liquidGlass(cornerRadius: CGFloat) -> some View {
        if #available(iOS 26.0, *) {
            self.glassEffect(
                .regular,
                in: RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
            )
        } else {
            self
                .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: cornerRadius, style: .continuous))
                .overlay {
                    RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                        .strokeBorder(.white.opacity(0.55), lineWidth: 1)
                }
        }
    }
}
