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
        sendButton.heightAnchor.constraint(equalToConstant: 52).isActive = true
        sendButton.addTarget(self, action: #selector(startTransfer), for: .touchUpInside)

        let cancelButton = UIButton(type: .system)
        cancelButton.setTitle("Abbrechen", for: .normal)
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
        guard let item = extensionContext?.inputItems.first as? NSExtensionItem,
              let provider = item.attachments?.first else {
            statusLabel.text = "Keine Datei zum Teilen erhalten."
            sendButton.isEnabled = false
            return
        }
        let type = provider.registeredTypeIdentifiers.first ?? UTType.data.identifier
        provider.loadFileRepresentation(forTypeIdentifier: type) { [weak self] url, error in
            guard let self else { return }
            DispatchQueue.main.async {
                guard let url, error == nil else {
                    self.statusLabel.text = "Datei konnte nicht geöffnet werden."
                    self.sendButton.isEnabled = false
                    return
                }
                do {
                    let destination = FileManager.default.temporaryDirectory.appendingPathComponent(url.lastPathComponent)
                    try? FileManager.default.removeItem(at: destination)
                    try FileManager.default.copyItem(at: url, to: destination)
                    self.fileURL = destination
                    self.filenameField.text = url.lastPathComponent
                    let size = (try? destination.resourceValues(forKeys: [.fileSizeKey]).fileSize) ?? 0
                    self.statusLabel.text = "Bereit · " + ByteCountFormatter.string(fromByteCount: Int64(size), countStyle: .file)
                } catch {
                    self.statusLabel.text = "Datei konnte nicht vorbereitet werden: \(error.localizedDescription)"
                    self.sendButton.isEnabled = false
                }
            }
        }
    }

    @objc private func startTransfer() {
        guard !isSending, let fileURL else { return }
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
        guard data.count <= maximum else {
            fail("Bluetooth-Nachricht zu groß (\(data.count)/\(maximum) Bytes).")
            return
        }
        target.writeValue(data, for: tx, type: .withResponse)
    }

    func peripheral(_ peripheral: CBPeripheral, didWriteValueFor characteristic: CBCharacteristic, error: Error?) {
        guard error == nil else {
            fail("Übertragung fehlgeschlagen: \(error!.localizedDescription)")
            return
        }
        if chunkIndex == -1 {
            progressView.setProgress(1, animated: true)
            statusLabel.text = "Datei erfolgreich an Windows gesendet."
            haptics.impactOccurred()
            isSending = false
            sendButton.isEnabled = true
            sendButton.setTitle("Erneut senden", for: .normal)
            extensionContext?.completeRequest(returningItems: nil)
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
