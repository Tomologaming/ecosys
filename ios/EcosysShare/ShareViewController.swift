import UIKit
import CoreBluetooth
import UniformTypeIdentifiers

final class ShareViewController: UIViewController, CBCentralManagerDelegate, CBPeripheralDelegate {
    private let serviceUUID = CBUUID(string: "6E6F4D4F-0002-4D4F-4D4F-45434F595300")
    private let txUUID = CBUUID(string: "6E6F4D4F-0003-4D4F-4D4F-45434F595300")
    private var central: CBCentralManager!
    private var target: CBPeripheral?
    private var tx: CBCharacteristic?
    private var fileURL: URL?
    private var fileData = Data()
    private var chunks: [Data] = []
    private var chunkIndex = 0
    private var transferID = UUID().uuidString
    private var isSending = false
    private var outgoingFrames: [Data] = []
    private var frameMessageID: UInt16 = 100
    private var filenameField = UITextField()
    private var progressView = UIProgressView(progressViewStyle: .default)
    private var statusLabel = UILabel()
    private var sendButton = UIButton(type: .system)
    private var renameLabel = UILabel()
    private var haptics = UIImpactFeedbackGenerator(style: .light)

    override func viewDidLoad() {
        super.viewDidLoad()
        view.backgroundColor = UIColor.systemBackground
        configureUI()
        central = CBCentralManager(delegate: self, queue: .main)
        loadSharedFile()
    }

    private func configureUI() {
        let root = UIStackView()
        root.axis = .vertical
        root.spacing = 18
        root.translatesAutoresizingMaskIntoConstraints = false
        view.addSubview(root)

        let icon = UIImageView(image: UIImage(systemName: "paperplane.circle.fill"))
        icon.tintColor = .systemBlue
        icon.contentMode = .scaleAspectFit
        icon.heightAnchor.constraint(equalToConstant: 56).isActive = true

        let title = UILabel()
        title.text = "An Ecosys senden"
        title.font = .systemFont(ofSize: 25, weight: .bold)
        title.textAlignment = .center

        let subtitle = UILabel()
        subtitle.text = "Direkt und privat an dein Windows-Gerät"
        subtitle.font = .systemFont(ofSize: 14)
        subtitle.textColor = .secondaryLabel
        subtitle.textAlignment = .center
        subtitle.numberOfLines = 0

        renameLabel.text = "DATEINAME"
        renameLabel.font = .systemFont(ofSize: 11, weight: .bold)
        renameLabel.textColor = .secondaryLabel

        filenameField.borderStyle = .roundedRect
        filenameField.placeholder = "Dateiname"
        filenameField.clearButtonMode = .whileEditing
        filenameField.autocorrectionType = .no
        filenameField.returnKeyType = .done

        let fileCard = UIVisualEffectView(effect: UIBlurEffect(style: .systemMaterial))
        fileCard.layer.cornerRadius = 18
        fileCard.clipsToBounds = true
        let fileStack = UIStackView(arrangedSubviews: [renameLabel, filenameField])
        fileStack.axis = .vertical
        fileStack.spacing = 8
        fileStack.translatesAutoresizingMaskIntoConstraints = false
        fileCard.contentView.addSubview(fileStack)
        NSLayoutConstraint.activate([
            fileStack.leadingAnchor.constraint(equalTo: fileCard.contentView.leadingAnchor, constant: 16),
            fileStack.trailingAnchor.constraint(equalTo: fileCard.contentView.trailingAnchor, constant: -16),
            fileStack.topAnchor.constraint(equalTo: fileCard.contentView.topAnchor, constant: 16),
            fileStack.bottomAnchor.constraint(equalTo: fileCard.contentView.bottomAnchor, constant: -16)
        ])

        progressView.progress = 0
        statusLabel.text = "Datei wird vorbereitet …"
        statusLabel.textColor = .secondaryLabel
        statusLabel.font = .systemFont(ofSize: 13)
        statusLabel.numberOfLines = 0
        statusLabel.textAlignment = .center

        sendButton.setTitle("Gerät suchen und senden", for: .normal)
        sendButton.titleLabel?.font = .systemFont(ofSize: 16, weight: .semibold)
        sendButton.backgroundColor = .systemBlue
        sendButton.tintColor = .white
        sendButton.layer.cornerRadius = 15
        sendButton.isEnabled = false
        sendButton.heightAnchor.constraint(equalToConstant: 52).isActive = true
        sendButton.addTarget(self, action: #selector(startTransfer), for: .touchUpInside)

        let cancelButton = UIButton(type: .system)
        cancelButton.setTitle("Schließen", for: .normal)
        cancelButton.addTarget(self, action: #selector(cancel), for: .touchUpInside)

        root.addArrangedSubview(icon)
        root.addArrangedSubview(title)
        root.addArrangedSubview(subtitle)
        root.addArrangedSubview(fileCard)
        root.addArrangedSubview(progressView)
        root.addArrangedSubview(statusLabel)
        root.addArrangedSubview(sendButton)
        root.addArrangedSubview(cancelButton)
        NSLayoutConstraint.activate([
            root.leadingAnchor.constraint(equalTo: view.leadingAnchor, constant: 22),
            root.trailingAnchor.constraint(equalTo: view.trailingAnchor, constant: -22),
            root.topAnchor.constraint(equalTo: view.safeAreaLayoutGuide.topAnchor, constant: 20)
        ])
    }

    private func loadSharedFile() {
        // Share sheets can provide several representations for the same photo/video.
        // Prefer public media/file types instead of blindly taking the provider's
        // first (sometimes app-private) registered identifier.
        let providers = (extensionContext?.inputItems as? [NSExtensionItem] ?? [])
            .flatMap { $0.attachments ?? [] }
        guard let provider = providers.first else {
            statusLabel.text = "Keine Datei zum Teilen erhalten."
            sendButton.isEnabled = false
            return
        }

        let identifiers = provider.registeredTypeIdentifiers
        let preferredType = identifiers.first(where: {
            guard let type = UTType($0) else { return false }
            return type.conforms(to: .movie)
        }) ?? identifiers.first(where: {
            guard let type = UTType($0) else { return false }
            return type.conforms(to: .image)
        }) ?? identifiers.first(where: {
            guard let type = UTType($0) else { return false }
            return type.conforms(to: .fileURL) || type.conforms(to: .data) || type.conforms(to: .content)
        }) ?? UTType.data.identifier

        provider.loadFileRepresentation(forTypeIdentifier: preferredType) { [weak self] url, error in
            guard let self else { return }
            guard let url, error == nil else {
                DispatchQueue.main.async {
                    self.statusLabel.text = "Foto/Video konnte nicht geladen werden. Bitte teile es erneut aus Fotos oder Dateien."
                    self.sendButton.isEnabled = false
                }
                return
            }

            // The provider's temporary URL is valid only during this callback.
            // Copy it before returning, then update the UI on the main queue.
            let destination = FileManager.default.temporaryDirectory
                .appendingPathComponent(UUID().uuidString + "-" + url.lastPathComponent)
            do {
                try FileManager.default.copyItem(at: url, to: destination)
                let size = (try? destination.resourceValues(forKeys: [.fileSizeKey]).fileSize) ?? 0
                let filename = url.lastPathComponent
                DispatchQueue.main.async {
                    self.fileURL = destination
                    self.filenameField.text = filename
                    self.statusLabel.text = "Bereit · " + ByteCountFormatter.string(fromByteCount: Int64(size), countStyle: .file)
                    self.sendButton.isEnabled = true
                }
            } catch {
                DispatchQueue.main.async {
                    self.statusLabel.text = "Datei konnte nicht vorbereitet werden: \(error.localizedDescription)"
                    self.sendButton.isEnabled = false
                }
            }
        }
    }

    @objc private func startTransfer() {
        guard !isSending, let fileURL else { return }
        guard central.state == .poweredOn else {
            fail("Bluetooth ist nicht bereit. Bitte Bluetooth aktivieren und erneut versuchen.")
            return
        }
        let name = (filenameField.text ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        guard !name.isEmpty else {
            statusLabel.text = "Bitte einen Dateinamen eingeben."
            return
        }
        do {
            fileData = try Data(contentsOf: fileURL, options: .mappedIfSafe)
            let maxChunk = 80
            chunks = stride(from: 0, to: fileData.count, by: maxChunk).map {
                fileData.subdata(in: $0..<min($0 + maxChunk, fileData.count))
            }
            if chunks.isEmpty { chunks = [Data()] }
            chunkIndex = 0
            transferID = UUID().uuidString
            isSending = true
            sendButton.isEnabled = false
            filenameField.isEnabled = false
            statusLabel.text = "Suche nach Ecosys-Geräten …"
            haptics.impactOccurred()
            if central.state == .poweredOn {
                central.scanForPeripherals(withServices: nil, options: [CBCentralManagerScanOptionAllowDuplicatesKey: false])
            }
        } catch {
            statusLabel.text = "Datei konnte nicht gelesen werden: \(error.localizedDescription)"
        }
    }

    func centralManagerDidUpdateState(_ central: CBCentralManager) {
        if central.state == .poweredOn {
            statusLabel.text = "Bluetooth bereit. Tippe auf Senden."
        } else if central.state == .unauthorized {
            statusLabel.text = "Bluetooth-Zugriff fehlt. Bitte in iOS-Einstellungen erlauben."
        } else {
            statusLabel.text = "Bluetooth ist nicht verfügbar."
        }
    }

    func centralManager(_ central: CBCentralManager, didDiscover peripheral: CBPeripheral,
                        advertisementData: [String : Any], rssi RSSI: NSNumber) {
        let services = advertisementData[CBAdvertisementDataServiceUUIDsKey] as? [CBUUID] ?? []
        let name = advertisementData[CBAdvertisementDataLocalNameKey] as? String ?? peripheral.name ?? ""
        guard services.contains(serviceUUID) || name.localizedCaseInsensitiveCompare("Ecosys") == .orderedSame else { return }
        central.stopScan()
        target = peripheral
        peripheral.delegate = self
        statusLabel.text = "Verbinde mit Ecosys …"
        central.connect(peripheral, options: nil)
    }

    func centralManager(_ central: CBCentralManager, didConnect peripheral: CBPeripheral) {
        peripheral.discoverServices([serviceUUID])
    }

    func centralManager(_ central: CBCentralManager, didFailToConnect peripheral: CBPeripheral, error: Error?) {
        fail("Verbindung fehlgeschlagen: \(error?.localizedDescription ?? "unbekannter Fehler")")
    }

    func peripheral(_ peripheral: CBPeripheral, didDiscoverServices error: Error?) {
        guard error == nil, let service = peripheral.services?.first(where: { $0.uuid == serviceUUID }) else {
            fail("Ecosys-Dienst nicht gefunden.")
            return
        }
        peripheral.discoverCharacteristics([txUUID], for: service)
    }

    func peripheral(_ peripheral: CBPeripheral, didDiscoverCharacteristicsFor service: CBService, error: Error?) {
        guard error == nil, let characteristic = service.characteristics?.first(where: { $0.uuid == txUUID }) else {
            fail("Ecosys-Sendekanal nicht gefunden.")
            return
        }
        tx = characteristic
        sendNextMessage()
    }

    private func sendNextMessage() {
        guard let target, let tx else { return }
        let filename = safeFilename(filenameField.text ?? "Datei")
        let message: [String: Any]
        if chunkIndex == -1 {
            message = ["protocol":"ecosys/1", "type":"file.complete", "transferId":transferID]
        } else if chunkIndex == 0 {
            message = ["protocol":"ecosys/1", "type":"file.start", "transferId":transferID,
                       "fileName":filename, "size":fileData.count, "chunks":chunks.count]
        } else {
            let index = chunkIndex - 1
            message = ["protocol":"ecosys/1", "type":"file.chunk", "transferId":transferID,
                       "index":index, "data":chunks[index].base64EncodedString()]
        }
        guard let data = try? JSONSerialization.data(withJSONObject: message) else {
            fail("Dateiübertragung konnte nicht vorbereitet werden.")
            return
        }
        let maximum = target.maximumWriteValueLength(for: .withResponse)
        guard maximum > 10 else {
            fail("Bluetooth-Verbindung unterstützt keine ausreichend großen Nachrichten.")
            return
        }
        frameMessageID &+= 1
        let payloadLimit = maximum - 10
        let frameCount = max(1, Int(ceil(Double(data.count) / Double(payloadLimit))))
        guard frameCount <= Int(UInt16.max) else {
            fail("Die Datei-Nachricht ist zu groß.")
            return
        }
        outgoingFrames = (0..<frameCount).map { index in
            let start = index * payloadLimit
            let end = min(start + payloadLimit, data.count)
            var frame = Data([0x45, 0x43, 0x53, 0x31])
            func appendLE(_ value: UInt16, to targetData: inout Data) {
                targetData.append(UInt8(value & 0xff))
                targetData.append(UInt8((value >> 8) & 0xff))
            }
            appendLE(frameMessageID, to: &frame)
            appendLE(UInt16(index), to: &frame)
            appendLE(UInt16(frameCount), to: &frame)
            if start < end { frame.append(data.subdata(in: start..<end)) }
            return frame
        }
        target.writeValue(outgoingFrames.removeFirst(), for: tx, type: .withResponse)
    }

    func peripheral(_ peripheral: CBPeripheral, didWriteValueFor characteristic: CBCharacteristic, error: Error?) {
        guard error == nil else {
            fail("Übertragung fehlgeschlagen: \(error!.localizedDescription)")
            return
        }
        if !outgoingFrames.isEmpty {
            guard let target else {
                fail("Bluetooth-Verbindung wurde getrennt.")
                return
            }
            target.writeValue(outgoingFrames.removeFirst(), for: characteristic, type: .withResponse)
            return
        }
        if chunkIndex == -1 {
            progressView.setProgress(1, animated: true)
            statusLabel.text = "Datei erfolgreich an Windows gesendet."
            haptics.impactOccurred()
            isSending = false
            sendButton.isEnabled = true
            sendButton.setTitle("Gesendet ✓", for: .normal)
            sendButton.isEnabled = false
            return
        }
        if chunkIndex == 0 {
            chunkIndex = 1
        } else {
            chunkIndex += 1
        }
        if chunkIndex > chunks.count {
            chunkIndex = -1
        }
        let completed = max(0, min(chunks.count, chunkIndex == -1 ? chunks.count : chunkIndex - 1))
        progressView.setProgress(Float(completed) / Float(max(1, chunks.count)), animated: true)
        statusLabel.text = "Übertrage Datei … \(Int(Float(completed) / Float(max(1, chunks.count)) * 100)) %"
        sendNextMessage()
    }

    func centralManager(_ central: CBCentralManager, didDisconnectPeripheral peripheral: CBPeripheral, error: Error?) {
        if isSending { fail("Bluetooth-Verbindung wurde unterbrochen.") }
    }

    private func safeFilename(_ value: String) -> String {
        let cleaned = URL(fileURLWithPath: value).lastPathComponent
        return cleaned.isEmpty ? "Datei" : cleaned
    }

    private func fail(_ message: String) {
        central.stopScan()
        isSending = false
        statusLabel.text = message
        sendButton.isEnabled = true
        filenameField.isEnabled = true
        UINotificationFeedbackGenerator().notificationOccurred(.error)
    }

    @objc private func cancel() {
        central?.stopScan()
        if let target { central?.cancelPeripheralConnection(target) }
        extensionContext?.cancelRequest(withError: NSError(domain: "EcosysShare", code: 1))
    }
}
