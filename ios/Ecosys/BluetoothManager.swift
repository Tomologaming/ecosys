import Foundation
import CoreBluetooth
import UIKit
import Combine

struct EcosysDevice: Identifiable {
    let id: UUID
    let name: String
    let detail: String
    let icon: String
    let peripheral: CBPeripheral
}

/// ECS1 framing shared with the Windows client and the web bridge.
///
/// | offset | size | field                     |
/// |--------|------|---------------------------|
/// | 0      | 4    | magic "ECS1"              |
/// | 4      | 2    | frame id (uint16 LE)      |
/// | 6      | 2    | frame index (uint16 LE)   |
/// | 8      | 2    | frame count (uint16 LE)   |
/// | 10     | n    | UTF-8 payload fragment    |
///
/// Messages that fit into a single BLE write are sent as plain UTF-8 JSON, which every
/// peer accepts. Framing is only used when a message exceeds one write.
enum EcosysFraming {
    static let headerSize = 10
    static let maxMessagesBytes = 4 * 1024 * 1024

    static func isFrame(_ data: Data) -> Bool {
        guard data.count >= 4 else { return false }
        let bytes = [UInt8](data.prefix(4))
        return bytes[0] == 0x45 && bytes[1] == 0x43 && bytes[2] == 0x53 && bytes[3] == 0x31
    }

    static func frames(for payload: Data, payloadSize: Int) -> [Data] {
        let chunkSize = max(1, payloadSize)
        let count = max(1, (payload.count + chunkSize - 1) / chunkSize)
        let frameId = UInt16.random(in: 1...UInt16.max)
        var frames: [Data] = []
        frames.reserveCapacity(count)

        for index in 0..<count {
            let start = payload.startIndex + index * chunkSize
            let end = min(payload.endIndex, start + chunkSize)
            var frame = Data()
            frame.append(contentsOf: [0x45, 0x43, 0x53, 0x31])
            frame.append(littleEndian: frameId)
            frame.append(littleEndian: UInt16(index))
            frame.append(littleEndian: UInt16(count))
            frame.append(payload[start..<end])
            frames.append(frame)
        }

        return frames
    }
}

private extension Data {
    mutating func append(littleEndian value: UInt16) {
        append(UInt8(value & 0xFF))
        append(UInt8((value >> 8) & 0xFF))
    }
}

/// Reassembles ECS1 frames and passes plain JSON through unchanged.
struct EcosysFrameAssembler {
    private var frameId: UInt16 = 0
    private var frameCount: UInt16 = 0
    private var nextIndex: UInt16 = 0
    private var parts: [Data] = []

    mutating func push(_ data: Data) -> Data? {
        guard !data.isEmpty else { return nil }

        guard EcosysFraming.isFrame(data) else {
            // Plain UTF-8 JSON from a native peer.
            return data
        }

        guard data.count >= EcosysFraming.headerSize else { return nil }
        let bytes = [UInt8](data)
        let id = UInt16(bytes[4]) | UInt16(bytes[5]) << 8
        let index = UInt16(bytes[6]) | UInt16(bytes[7]) << 8
        let count = UInt16(bytes[8]) | UInt16(bytes[9]) << 8

        guard count > 0, index < count else { return nil }

        if index == 0 {
            frameId = id
            frameCount = count
            nextIndex = 0
            parts.removeAll(keepingCapacity: true)
        }

        guard id == frameId, count == frameCount, index == nextIndex else {
            reset()
            return nil
        }

        parts.append(data.subdata(in: EcosysFraming.headerSize..<data.count))
        nextIndex += 1

        guard nextIndex == frameCount else { return nil }

        let total = parts.reduce(0) { $0 + $1.count }
        guard total <= EcosysFraming.maxMessagesBytes else {
            reset()
            return nil
        }

        var joined = Data(capacity: total)
        for part in parts { joined.append(part) }
        reset()
        return joined
    }

    mutating func reset() {
        parts.removeAll(keepingCapacity: false)
        nextIndex = 0
    }
}

final class BluetoothManager: NSObject, ObservableObject {
    static let serviceUUID = CBUUID(string: "6E6F4D4F-0002-4D4F-4D4F-45434F595300")
    static let txUUID = CBUUID(string: "6E6F4D4F-0003-4D4F-4D4F-45434F595300")
    static let rxUUID = CBUUID(string: "6E6F4D4F-0004-4D4F-4D4F-45434F595300")

    @Published var devices: [EcosysDevice] = []
    @Published var status = "Bluetooth wird initialisiert…"
    @Published var lastMessage: String?
    @Published var isScanning = false
    @Published private(set) var isReady = false
    @Published private(set) var connectedName: String?
    @Published private(set) var hasPeerSubscriber = false

    /// True when a hello can actually reach another device.
    var canSend: Bool { connectedName != nil || hasPeerSubscriber }

    private var central: CBCentralManager!
    private var connected: CBPeripheral?
    private var txCharacteristic: CBCharacteristic?
    private var peripheralManager: CBPeripheralManager!
    private var peripheralService: CBMutableService?
    private var peripheralTxCharacteristic: CBMutableCharacteristic?
    private var peripheralRxCharacteristic: CBMutableCharacteristic?
    private var incomingFrames = EcosysFrameAssembler()
    private var pendingNotifications: [Data] = []

    override init() {
        super.init()
        central = CBCentralManager(delegate: self, queue: .main)
        peripheralManager = CBPeripheralManager(delegate: self, queue: .main)
    }

    // MARK: - Scanning

    func scan() {
        guard central.state == .poweredOn else {
            status = "Bluetooth ist nicht verfügbar"
            return
        }
        devices.removeAll()
        isScanning = true
        status = "Suche nach Ecosys-Geräten…"
        // Do not filter at the controller level. Some Windows BLE stacks omit the
        // service UUID from the first advertisement even though the service is
        // present. We validate the service after discovery instead.
        central.scanForPeripherals(withServices: nil, options: [
            CBCentralManagerScanOptionAllowDuplicatesKey: false
        ])
        DispatchQueue.main.asyncAfter(deadline: .now() + 10) { [weak self] in
            self?.stopScan()
        }
    }

    private func stopScan() {
        if central.state == .poweredOn {
            central.stopScan()
        }
        isScanning = false
        if connected == nil {
            status = devices.isEmpty ? "Keine Ecosys-Geräte gefunden" : "\(devices.count) Gerät(e) gefunden"
        }
    }

    // MARK: - Connection

    func connect(to device: EcosysDevice) {
        if let current = connected, current.identifier != device.peripheral.identifier {
            central.cancelPeripheralConnection(current)
        }
        if let current = connected, current.identifier == device.peripheral.identifier {
            disconnect()
            return
        }

        status = "Verbinde mit \(device.name)…"
        connected = device.peripheral
        txCharacteristic = nil
        incomingFrames.reset()
        device.peripheral.delegate = self
        central.connect(device.peripheral, options: [CBConnectPeripheralOptionNotifyOnDisconnectionKey: true])
    }

    func disconnect() {
        if let current = connected {
            central.cancelPeripheralConnection(current)
        }
        connected = nil
        txCharacteristic = nil
        connectedName = nil
        incomingFrames.reset()
        status = hasPeerSubscriber ? "Windows-Gerät verbunden" : "Bluetooth bereit"
    }

    // MARK: - Sending

    func sendHello() {
        let name = UIDevice.current.name
        let message: [String: Any] = [
            "protocol": "ecosys/1",
            "type": "hello",
            "messageId": UUID().uuidString,
            "sender": "ios-\(name)",
            "timestamp": Int(Date().timeIntervalSince1970 * 1000),
            "payload": [
                "deviceName": name,
                "platform": "ios",
                "capabilities": ["hello", "ping"]
            ]
        ]
        guard let data = try? JSONSerialization.data(withJSONObject: message) else {
            lastMessage = "Hello konnte nicht erstellt werden."
            return
        }

        if let txCharacteristic, let connected, connected.state == .connected {
            write(data, to: txCharacteristic, on: connected)
            return
        }

        if hasPeerSubscriber {
            notify(data)
            return
        }

        lastMessage = "Kein Gerät verbunden."
    }

    private func write(_ data: Data, to characteristic: CBCharacteristic, on peripheral: CBPeripheral) {
        let limit = peripheral.maximumWriteValueLength(for: .withResponse)
        guard data.count > limit else {
            peripheral.writeValue(data, for: characteristic, type: .withResponse)
            return
        }

        // Too large for a single attribute write: split into ECS1 frames.
        let payloadSize = max(1, limit - EcosysFraming.headerSize)
        for frame in EcosysFraming.frames(for: data, payloadSize: payloadSize) {
            peripheral.writeValue(frame, for: characteristic, type: .withResponse)
        }
    }

    private func notify(_ data: Data) {
        guard let characteristic = peripheralRxCharacteristic else { return }
        if peripheralManager.updateValue(data, for: characteristic, onSubscribedCentrals: nil) {
            pendingNotifications.removeAll()
        } else {
            // The queue is full; retry in peripheralManagerIsReady(toUpdateSubscribers:).
            pendingNotifications.append(data)
        }
    }

    // MARK: - Receiving

    private func receive(_ data: Data) {
        guard let assembled = incomingFrames.push(data) else { return }
        guard let object = try? JSONSerialization.jsonObject(with: assembled) as? [String: Any],
              let type = object["type"] as? String else {
            return
        }

        let payload = object["payload"] as? [String: Any] ?? [:]
        let deviceName = payload["deviceName"] as? String
            ?? payload["platform"] as? String
            ?? object["sender"] as? String
            ?? "Gerät"

        switch type {
        case "hello":
            lastMessage = "hello · \(deviceName)"
            status = "Nachricht von \(deviceName)"
        case "hello.ack":
            lastMessage = "hello.ack · \(deviceName)"
        case "error":
            let message = payload["message"] as? String ?? "unbekannter Fehler"
            lastMessage = "error · \(message)"
        default:
            lastMessage = "\(type) · \(deviceName)"
        }
    }
}

// MARK: - Central

extension BluetoothManager: CBCentralManagerDelegate {
    func centralManagerDidUpdateState(_ central: CBCentralManager) {
        switch central.state {
        case .poweredOn:
            isReady = true
            status = "Bluetooth bereit"
        case .poweredOff:
            isReady = false
            status = "Bluetooth ist ausgeschaltet"
        case .unauthorized:
            isReady = false
            status = "Bluetooth-Berechtigung fehlt – bitte in Einstellungen erlauben"
        case .unsupported:
            isReady = false
            status = "Bluetooth wird nicht unterstützt"
        default:
            isReady = false
            status = "Bluetooth wird initialisiert…"
        }
    }

    func centralManager(_ central: CBCentralManager, didDiscover peripheral: CBPeripheral,
                        advertisementData: [String: Any], rssi RSSI: NSNumber) {
        guard devices.first(where: { $0.id == peripheral.identifier }) == nil else { return }

        let advertisedServices = advertisementData[CBAdvertisementDataServiceUUIDsKey] as? [CBUUID] ?? []
        let hasEcosysService = advertisedServices.contains(Self.serviceUUID)
        let localName = advertisementData[CBAdvertisementDataLocalNameKey] as? String
        // Prefer the advertised local name: peripheral.name may be the user's
        // system Bluetooth name (for example, "Momo's iPhone"), not the app name.
        let advertisedName = localName?.trimmingCharacters(in: .whitespacesAndNewlines)
        let name = advertisedName?.isEmpty == false ? advertisedName! : (peripheral.name ?? "Ecosys device")

        // Only show devices that advertise our private service UUID or explicitly
        // identify this app. Windows publishes the "Ecosys" local name for discovery.
        let hasEcosysName = advertisedName?.localizedCaseInsensitiveCompare("Ecosys") == .orderedSame
        guard hasEcosysService || hasEcosysName else { return }

        devices.append(EcosysDevice(
            id: peripheral.identifier,
            name: name,
            detail: "Bluetooth Low Energy",
            icon: "antenna.radiowaves.left.and.right",
            peripheral: peripheral
        ))
    }

    func centralManager(_ central: CBCentralManager, didConnect peripheral: CBPeripheral) {
        status = "Verbunden mit \(peripheral.name ?? "Ecosys device") – suche Dienste…"
        peripheral.discoverServices([Self.serviceUUID])
    }

    func centralManager(_ central: CBCentralManager, didFailToConnect peripheral: CBPeripheral, error: Error?) {
        if connected?.identifier == peripheral.identifier {
            connected = nil
            txCharacteristic = nil
            connectedName = nil
        }
        status = "Verbindung fehlgeschlagen\(error.map { ": \($0.localizedDescription)" } ?? "")"
    }

    func centralManager(_ central: CBCentralManager, didDisconnectPeripheral peripheral: CBPeripheral, error: Error?) {
        guard connected?.identifier == peripheral.identifier else { return }
        connected = nil
        txCharacteristic = nil
        connectedName = nil
        incomingFrames.reset()
        status = error == nil
            ? "Bluetooth-Verbindung getrennt"
            : "Bluetooth-Verbindung getrennt: \(error!.localizedDescription)"
    }
}

// MARK: - Peripheral (client side)

extension BluetoothManager: CBPeripheralDelegate {
    func peripheral(_ peripheral: CBPeripheral, didDiscoverServices error: Error?) {
        if let error {
            status = "BLE-Dienste konnten nicht geladen werden: \(error.localizedDescription)"
            return
        }
        guard let service = peripheral.services?.first(where: { $0.uuid == Self.serviceUUID }) else {
            status = "Ecosys-BLE-Dienst auf dem Gerät nicht gefunden"
            return
        }
        peripheral.discoverCharacteristics([Self.txUUID, Self.rxUUID], for: service)
    }

    func peripheral(_ peripheral: CBPeripheral, didDiscoverCharacteristicsFor service: CBService, error: Error?) {
        if let error {
            status = "BLE-Characteristics konnten nicht geladen werden: \(error.localizedDescription)"
            return
        }
        txCharacteristic = service.characteristics?.first(where: { $0.uuid == Self.txUUID })
        let rx = service.characteristics?.first(where: { $0.uuid == Self.rxUUID })
        guard txCharacteristic != nil || rx != nil else {
            status = "Ecosys-BLE-Characteristics nicht gefunden"
            return
        }
        if let rx {
            peripheral.setNotifyValue(true, for: rx)
        }
        connectedName = peripheral.name ?? "Ecosys device"
        status = "Verbunden mit \(connectedName ?? "Ecosys device")"
    }

    func peripheral(_ peripheral: CBPeripheral, didUpdateNotificationStateFor characteristic: CBCharacteristic, error: Error?) {
        if let error {
            status = "BLE-Benachrichtigungen konnten nicht aktiviert werden: \(error.localizedDescription)"
        }
    }

    func peripheral(_ peripheral: CBPeripheral, didUpdateValueFor characteristic: CBCharacteristic, error: Error?) {
        if let error {
            status = "BLE-Empfang fehlgeschlagen: \(error.localizedDescription)"
            return
        }
        guard let data = characteristic.value else { return }
        receive(data)
    }

    func peripheral(_ peripheral: CBPeripheral, didWriteValueFor characteristic: CBCharacteristic, error: Error?) {
        if let error {
            status = "BLE-Senden fehlgeschlagen: \(error.localizedDescription)"
        }
    }
}

// MARK: - Peripheral role (server side)

extension BluetoothManager: CBPeripheralManagerDelegate {
    func peripheralManagerDidUpdateState(_ peripheral: CBPeripheralManager) {
        switch peripheral.state {
        case .poweredOn:
            publishPeripheralService()
        case .unauthorized:
            status = "Bluetooth-Berechtigung fehlt – bitte in Einstellungen erlauben"
        case .poweredOff:
            hasPeerSubscriber = false
        default:
            break
        }
    }

    private func publishPeripheralService() {
        guard peripheralManager.state == .poweredOn else { return }
        if peripheralService != nil {
            startAdvertisingIfReady()
            return
        }

        let tx = CBMutableCharacteristic(
            type: Self.txUUID,
            properties: [.write, .writeWithoutResponse],
            value: nil,
            permissions: [.writeable]
        )
        let rx = CBMutableCharacteristic(
            type: Self.rxUUID,
            properties: [.notify, .read],
            value: nil,
            permissions: [.readable]
        )
        let service = CBMutableService(type: Self.serviceUUID, primary: true)
        service.characteristics = [tx, rx]
        peripheralTxCharacteristic = tx
        peripheralRxCharacteristic = rx
        peripheralService = service
        peripheralManager.add(service)
    }

    func peripheralManager(_ peripheral: CBPeripheralManager, didAdd service: CBService, error: Error?) {
        if let error {
            status = "Ecosys-BLE-Dienst konnte nicht veröffentlicht werden: \(error.localizedDescription)"
            peripheralService = nil
            return
        }
        startAdvertisingIfReady()
    }

    private func startAdvertisingIfReady() {
        guard peripheralManager.state == .poweredOn, peripheralService != nil else { return }
        if peripheralManager.isAdvertising { return }
        peripheralManager.startAdvertising([
            CBAdvertisementDataServiceUUIDsKey: [Self.serviceUUID],
            CBAdvertisementDataLocalNameKey: "Ecosys"
        ])
    }

    func peripheralManager(_ peripheral: CBPeripheralManager, didStartAdvertising error: Error?) {
        if let error {
            status = "BLE-Werbung fehlgeschlagen: \(error.localizedDescription)"
        }
    }

    func peripheralManagerIsReady(toUpdateSubscribers peripheral: CBPeripheralManager) {
        guard !pendingNotifications.isEmpty else { return }
        for data in pendingNotifications {
            guard let characteristic = peripheralRxCharacteristic else { break }
            _ = peripheralManager.updateValue(data, for: characteristic, onSubscribedCentrals: nil)
        }
        pendingNotifications.removeAll()
    }

    func peripheralManager(_ peripheral: CBPeripheralManager, didReceiveWrite requests: [CBATTRequest]) {
        // Multiple writes may arrive in one ATT packet; only the first request may
        // be answered, so the buffered frames are processed before responding.
        var failure: CBATTError.Code?
        for request in requests {
            guard request.characteristic.uuid == Self.txUUID, let value = request.value else {
                failure = .requestNotSupported
                continue
            }
            receive(value)
        }

        if let first = requests.first {
            peripheral.respond(to: first, withResult: failure ?? .success)
        }
    }

    func peripheralManager(_ peripheral: CBPeripheralManager, central: CBCentral,
                           didSubscribeTo characteristic: CBCharacteristic) {
        guard characteristic.uuid == Self.rxUUID else { return }
        hasPeerSubscriber = true
        status = "Ein Gerät ist per BLE verbunden"
    }

    func peripheralManager(_ peripheral: CBPeripheralManager, central: CBCentral,
                           didUnsubscribeFrom characteristic: CBCharacteristic) {
        guard characteristic.uuid == Self.rxUUID else { return }
        hasPeerSubscriber = false
        if connected == nil {
            status = "Bluetooth bereit"
        }
    }
}
