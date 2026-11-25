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
                    knownPeersSection
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
                    Text("PRIVAT. DIREKT. DEINS.")
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

            Text("Deine Geräte, direkt verbunden.")
                .font(.system(size: 16, weight: .medium))
                .foregroundStyle(ink.opacity(0.78))
        }
        .foregroundStyle(ink)
        .padding(22)
        .frame(maxWidth: .infinity, alignment: .leading)
        .liquidGlass(cornerRadius: 28)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Ecosys. Deine Geräte, direkt verbunden. Ohne Cloud.")
    }

    private var statusCard: some View {
        HStack(spacing: 12) {
            ZStack {
                Circle()
                    .fill(statusColor.opacity(0.16))
                    .frame(width: 42, height: 42)
                Circle()
                    .fill(statusColor)
                    .frame(width: 10, height: 10)
            }
            VStack(alignment: .leading, spacing: 3) {
                Text(statusTitle)
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
                .foregroundStyle(statusColor)
                .accessibilityHidden(true)
        }
        .padding(16)
        .liquidGlass(cornerRadius: 22)
        .accessibilityElement(children: .combine)
    }

    private var deviceCard: some View {
        VStack(alignment: .leading, spacing: 13) {
            sectionLabel("DIESES GERÄT")

            HStack(spacing: 13) {
                Image(systemName: "iphone.gen3")
                    .font(.system(size: 25, weight: .medium))
                    .foregroundStyle(blue)
                    .frame(width: 52, height: 52)
                    .liquidGlass(cornerRadius: 17)
                    .accessibilityHidden(true)
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
                    .accessibilityLabel("Bereit")
            }
            .padding(16)
            .liquidGlass(cornerRadius: 22)
        }
    }

    private var nearbySection: some View {
        VStack(alignment: .leading, spacing: 12) {
            sectionLabel("GERÄTE IN DER NÄHE")

            if bluetooth.devices.isEmpty {
                VStack(spacing: 12) {
                    Image(systemName: bluetooth.isScanning ? "dot.radiowaves.left.and.right" : "antenna.radiowaves.left.and.right")
                        .font(.system(size: 28, weight: .light))
                        .foregroundStyle(blue)
                        .symbolEffect(.pulse, isActive: bluetooth.isScanning)
                        .accessibilityHidden(true)
                    Text(bluetooth.isScanning ? "Suche läuft…" : "Noch keine Geräte gefunden")
                        .font(.system(size: 15, weight: .semibold))
                        .foregroundStyle(ink)
                    Text("Starte eine Suche und öffne Ecosys auf dem anderen Gerät.")
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
                            .accessibilityHidden(true)
                        VStack(alignment: .leading, spacing: 4) {
                            Text(device.name)
                                .font(.system(size: 15, weight: .semibold))
                                .foregroundStyle(ink)
                            Text(bluetooth.connectedName == device.name ? "Verbunden" : device.detail)
                                .font(.system(size: 12))
                                .foregroundStyle(bluetooth.connectedName == device.name ? green : ink.opacity(0.62))
                        }
                        Spacer(minLength: 4)
                        Button {
                            tapHaptic()
                            bluetooth.connect(to: device)
                        } label: {
                            Text(bluetooth.connectedName == device.name ? "Trennen" : "Verbinden")
                                .font(.system(size: 13, weight: .semibold))
                                .padding(.horizontal, 14)
                                .padding(.vertical, 10)
                                .liquidGlass(cornerRadius: 15)
                        }
                        .buttonStyle(.plain)
                        .foregroundStyle(blue)
                        .accessibilityHint(bluetooth.connectedName == device.name
                            ? "Trennt die bestehende Bluetooth-Verbindung."
                            : "Baut eine Bluetooth-Verbindung zu \(device.name) auf.")
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
                    Text(bluetooth.isScanning ? "Suche läuft…" : "Geräte suchen")
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
            .accessibilityHint("Sucht zehn Sekunden lang nach Ecosys-Geräten in der Nähe.")
        }
    }

    private var testSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            sectionLabel("VERBINDUNG TESTEN")

            Button {
                tapHaptic()
                bluetooth.sendHello()
            } label: {
                HStack(spacing: 10) {
                    Image(systemName: "paperplane.fill")
                    Text("Hello senden")
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
                        colors: bluetooth.canSend
                            ? [Color(red: 0.13, green: 0.55, blue: 0.43), Color(red: 0.10, green: 0.42, blue: 0.37)]
                            : [Color(red: 0.55, green: 0.62, blue: 0.60), Color(red: 0.45, green: 0.53, blue: 0.51)],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                    ),
                    in: RoundedRectangle(cornerRadius: 20, style: .continuous)
                )
            }
            .buttonStyle(.plain)
            .accessibilityHint("Sendet eine Ecosys-Testnachricht an das verbundene Gerät.")

            Text(bluetooth.canSend
                 ? "Bereit: Nachrichten gehen direkt an das verbundene Gerät."
                 : "Noch kein Gerät verbunden – oben suchen und verbinden.")
                .font(.system(size: 12))
                .foregroundStyle(ink.opacity(0.58))
                .padding(.leading, 3)

            if let lastMessage = bluetooth.lastMessage {
                HStack(alignment: .top, spacing: 10) {
                    Image(systemName: "text.bubble.fill")
                        .foregroundStyle(blue)
                        .accessibilityHidden(true)
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

    private var knownPeersSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            sectionLabel("BEKANNTE GERÄTE")

            VStack(alignment: .leading, spacing: 12) {
                HStack {
                    Text("Frühere Verbindungen")
                        .font(.system(size: 15, weight: .semibold))
                        .foregroundStyle(ink)
                    Spacer(minLength: 0)
                    if !bluetooth.knownPeers.isEmpty {
                        Text("\(bluetooth.knownPeers.count)")
                            .font(.system(size: 12, weight: .semibold))
                            .foregroundStyle(ink.opacity(0.55))
                            .padding(.horizontal, 9)
                            .padding(.vertical, 4)
                            .liquidGlass(cornerRadius: 10)
                            .accessibilityLabel("\(bluetooth.knownPeers.count) bekannte Geräte")
                    }
                }

                if bluetooth.knownPeers.isEmpty {
                    Text("Noch keine Geräte bekannt. Nach einem Hello trägt sich die Gegenstelle hier ein.")
                        .font(.system(size: 12))
                        .foregroundStyle(ink.opacity(0.62))
                        .fixedSize(horizontal: false, vertical: true)
                } else {
                    ForEach(bluetooth.knownPeers) { peer in
                        knownPeerRow(peer)
                    }

                    if unconfirmedPeerCount > 0 {
                        Button {
                            tapHaptic()
                            bluetooth.clearPeerHistory()
                        } label: {
                            Text("Verlauf leeren")
                                .font(.system(size: 13, weight: .semibold))
                                .frame(maxWidth: .infinity)
                                .padding(.vertical, 11)
                                .liquidGlass(cornerRadius: 15)
                        }
                        .buttonStyle(.plain)
                        .foregroundStyle(ink)
                        .accessibilityHint("Entfernt Geräte, die du nicht bestätigt hast. Bestätigte Geräte bleiben im Verlauf.")
                    }
                }
            }
            .padding(16)
            .liquidGlass(cornerRadius: 22)

            Text("iOS teilt noch keine Zwischenablage – die Bestätigung merkt sich deine Entscheidung für später.")
                .font(.system(size: 11))
                .foregroundStyle(ink.opacity(0.55))
                .padding(.leading, 3)
        }
    }

    private func knownPeerRow(_ peer: KnownPeer) -> some View {
        HStack(spacing: 12) {
            Image(systemName: peer.trusted ? "checkmark.seal.fill" : "person.crop.circle")
                .font(.system(size: 18, weight: .medium))
                .foregroundStyle(peer.trusted ? green : ink.opacity(0.5))
                .frame(width: 38, height: 38)
                .liquidGlass(cornerRadius: 13)
                .accessibilityHidden(true)

            VStack(alignment: .leading, spacing: 3) {
                Text(peer.name)
                    .font(.system(size: 14, weight: .semibold))
                    .foregroundStyle(ink)
                Text(peerDetail(peer))
                    .font(.system(size: 11))
                    .foregroundStyle(ink.opacity(0.6))
            }

            Spacer(minLength: 4)

            Button {
                tapHaptic()
                bluetooth.setTrusted(peer.id, !peer.trusted)
            } label: {
                Text(peer.trusted ? "Entziehen" : "Bestätigen")
                    .font(.system(size: 12, weight: .semibold))
                    .padding(.horizontal, 12)
                    .padding(.vertical, 9)
                    .liquidGlass(cornerRadius: 14)
            }
            .buttonStyle(.plain)
            .foregroundStyle(peer.trusted ? ink.opacity(0.75) : blue)
            .accessibilityHint(peer.trusted
                ? "Nimmt die Bestätigung für \(peer.name) zurück."
                : "Merkt sich \(peer.name) als bestätigtes Gerät.")
        }
        .padding(12)
        .liquidGlass(cornerRadius: 18)
    }

    private func peerDetail(_ peer: KnownPeer) -> String {
        var parts = [
            peer.platform,
            "zuletzt \(KnownPeers.describeLastSeen(now: KnownPeers.now(), lastSeen: peer.lastSeen))"
        ]
        if peer.trusted { parts.append("bestätigt") }
        if peer.id == bluetooth.currentPeerId { parts.append("verbunden") }
        return parts.joined(separator: " · ")
    }

    private var unconfirmedPeerCount: Int {
        bluetooth.knownPeers.filter { !$0.trusted }.count
    }

    private var footer: some View {
        HStack(spacing: 7) {
            Image(systemName: "lock.shield")
            Text("Gerät zu Gerät. Kein Cloud-Konto nötig.")
        }
        .font(.system(size: 11, weight: .medium))
        .foregroundStyle(ink.opacity(0.55))
        .frame(maxWidth: .infinity)
        .padding(.top, 6)
        .accessibilityElement(children: .combine)
    }

    private func sectionLabel(_ text: String) -> some View {
        Text(text)
            .font(.system(size: 11, weight: .bold, design: .rounded))
            .tracking(1.5)
            .foregroundStyle(ink.opacity(0.62))
            .padding(.leading, 3)
            .accessibilityAddTraits(.isHeader)
    }

    private var statusColor: Color {
        if bluetooth.canSend { return green }
        return bluetooth.isReady ? blue : Color.orange
    }

    private var statusTitle: String {
        if bluetooth.connectedName != nil { return "Verbunden" }
        if bluetooth.hasPeerSubscriber { return "Gerät verbunden" }
        return bluetooth.isReady ? "Bereit" : "Bluetooth nicht bereit"
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
