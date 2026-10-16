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

final class BluetoothManager: NSObject, ObservableObject {
    static let serviceUUID = CBUUID(string: "6E6F4D4F-0002-4D4F-4D4F-45434F595300")
    static let txUUID = CBUUID(string: "6E6F4D4F-0003-4D4F-4D4F-45434F595300")
    static let rxUUID = CBUUID(string: "6E6F4D4F-0004-4D4F-4D4F-45434F595300")

    @Published var devices: [EcosysDevice] = []
    @Published var status = "Bluetooth wird initialisiert…"
    @Published var lastMessage: String?
    @Published var isScanning = false
    @Published private(set) var isReady = false

    private var central: CBCentralManager!
    private var connected: CBPeripheral?
    private var txCharacteristic: CBCharacteristic?
    private var peripheralManager: CBPeripheralManager!
    private var peripheralService: CBMutableService?
    private var peripheralTxCharacteristic: CBMutableCharacteristic?
    private var peripheralRxCharacteristic: CBMutableCharacteristic?
    private var hasPeripheralSubscriber = false
    private var pendingWriteFrames: [Data] = []
    private var frameMessageID: UInt16 = 1
    private var helloWriteInProgress = false
    private var incomingFrameBuffer = Data()
    private var incomingFrameID: UInt16?
    private var incomingFrameCount: UInt16 = 0
    private var incomingNextFrame: UInt16 = 0

    override init() {
        super.init()
        central = CBCentralManager(delegate: self, queue: .main)
        peripheralManager = CBPeripheralManager(delegate: self, queue: .main)
    }

    func scan() {
        guard central.state == .poweredOn else {
            status = "Bluetooth ist nicht verfügbar"
            return
        }
        devices.removeAll()
        isScanning = true
        status = "Suche nach Ecosys-Geräten…"
        // Some Windows BLE stacks omit the service UUID from the first advertisement.
        // Scan broadly, then only add devices advertising the Ecosys service UUID
        // or the explicit Ecosys local name in didDiscover.
        central.scanForPeripherals(withServices: nil, options: [
            CBCentralManagerScanOptionAllowDuplicatesKey: false
        ])
        DispatchQueue.main.asyncAfter(deadline: .now() + 10) { [weak self] in
            self?.stopScan()
        }
    }

    private func stopScan() {
        central.stopScan()
        isScanning = false
        if connected == nil {
            status = devices.isEmpty ? "Keine Ecosys-Geräte gefunden" : "\(devices.count) Gerät(e) gefunden"
        }
    }

    func connect(to device: EcosysDevice) {
        status = "Verbinde mit \(device.name)…"
        connected = device.peripheral
        txCharacteristic = nil
        device.peripheral.delegate = self
        central.connect(device.peripheral, options: [CBConnectPeripheralOptionNotifyOnDisconnectionKey: true])
    }


    private static func frame(_ data: Data, messageID: UInt16, payloadLimit: Int) -> [Data] {
        let safeLimit = max(1, payloadLimit)
        let count = max(1, Int(ceil(Double(data.count) / Double(safeLimit))))
        return (0..<count).map { index in
            let start = index * safeLimit
            let end = min(start + safeLimit, data.count)
            var frame = Data([0x45, 0x43, 0x53, 0x31])
            func appendLE(_ value: UInt16, to data: inout Data) {
                data.append(UInt8(value & 0xff))
                data.append(UInt8((value >> 8) & 0xff))
            }
            appendLE(messageID, to: &frame)
            appendLE(UInt16(index), to: &frame)
            appendLE(UInt16(count), to: &frame)
            if start < end { frame.append(data.subdata(in: start..<end)) }
            return frame
        }
    }

    private func writeNextHelloFrame() {
        guard let connected, let txCharacteristic else {
            helloWriteInProgress = false
            lastMessage = "Bluetooth-Verbindung wurde getrennt."
            return
        }
        guard !pendingWriteFrames.isEmpty else {
            helloWriteInProgress = false
            lastMessage = "Hello erfolgreich gesendet."
            status = "Hello erfolgreich gesendet."
            UINotificationFeedbackGenerator().notificationOccurred(.success)
            return
        }
        connected.writeValue(pendingWriteFrames.removeFirst(), for: txCharacteristic, type: .withResponse)
    }


    private func processIncomingData(_ data: Data) {
        let magic: [UInt8] = [0x45, 0x43, 0x53, 0x31]
        var message = data
        if data.count >= 10 && Array(data.prefix(4)) == magic {
            func readLE(_ offset: Int) -> UInt16 {
                UInt16(data[offset]) | (UInt16(data[offset + 1]) << 8)
            }
            let id = readLE(4)
            let index = readLE(6)
            let count = readLE(8)
            guard count > 0, index < count else { return }
            if index == 0 {
                incomingFrameBuffer.removeAll(keepingCapacity: true)
                incomingFrameID = id
                incomingFrameCount = count
                incomingNextFrame = 0
            }
            guard incomingFrameID == id, incomingFrameCount == count, incomingNextFrame == index else {
                incomingFrameBuffer.removeAll(keepingCapacity: false)
                incomingFrameID = nil
                return
            }
            incomingFrameBuffer.append(data.subdata(in: 10..<data.count))
            incomingNextFrame += 1
            guard incomingNextFrame == incomingFrameCount else { return }
            message = incomingFrameBuffer
            incomingFrameBuffer.removeAll(keepingCapacity: true)
            incomingFrameID = nil
            incomingFrameCount = 0
            incomingNextFrame = 0
        }
        lastMessage = String(data: message, encoding: .utf8) ?? "Nachricht empfangen"
    }

    func sendHello() {
        let payload: [String: Any] = [
            "protocol": "ecosys/1",
            "type": "hello",
            "messageId": UUID().uuidString,
            "sender": UIDevice.current.name,
            "timestamp": Int(Date().timeIntervalSince1970 * 1000),
            "payload": [
                "deviceName": UIDevice.current.name,
                "platform": "ios"
            ]
        ]
        guard let data = try? JSONSerialization.data(withJSONObject: payload) else { return }

        guard let txCharacteristic, let connected else {
            lastMessage = "Kein Windows-Gerät verbunden. Bitte zuerst verbinden."
            status = lastMessage ?? status
            UINotificationFeedbackGenerator().notificationOccurred(.warning)
            return
        }
        guard !helloWriteInProgress else { return }
        helloWriteInProgress = true
        frameMessageID &+= 1
        pendingWriteFrames = Self.frame(data, messageID: frameMessageID,
                                        payloadLimit: max(1, connected.maximumWriteValueLength(for: .withResponse) - 10))
        lastMessage = "Sende Hello …"
        writeNextHelloFrame()
    }
}

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
                        advertisementData: [String : Any], rssi RSSI: NSNumber) {
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
        connected = nil
        txCharacteristic = nil
        status = "Verbindung fehlgeschlagen\(error.map { ": \($0.localizedDescription)" } ?? "")"
    }

    func centralManager(_ central: CBCentralManager, didDisconnectPeripheral peripheral: CBPeripheral, error: Error?) {
        guard connected?.identifier == peripheral.identifier else { return }
        connected = nil
        txCharacteristic = nil
        status = error == nil ? "Bluetooth-Verbindung getrennt" : "Bluetooth-Verbindung getrennt: \(error!.localizedDescription)"
    }
}

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
        status = "Verbunden mit \(peripheral.name ?? "Ecosys device")"
    }

    func peripheral(_ peripheral: CBPeripheral, didWriteValueFor characteristic: CBCharacteristic, error: Error?) {
        guard characteristic.uuid == Self.txUUID, helloWriteInProgress else { return }
        if let error {
            helloWriteInProgress = false
            pendingWriteFrames.removeAll()
            lastMessage = "Hello fehlgeschlagen: \(error.localizedDescription)"
            status = lastMessage ?? status
            UINotificationFeedbackGenerator().notificationOccurred(.error)
            return
        }
        writeNextHelloFrame()
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
        processIncomingData(data)
    }
}

extension BluetoothManager: CBPeripheralManagerDelegate {
    func peripheralManagerDidUpdateState(_ peripheral: CBPeripheralManager) {
        if peripheral.state == .poweredOn {
            publishPeripheralService()
        } else if peripheral.state == .unauthorized {
            status = "Bluetooth-Berechtigung fehlt – bitte in Einstellungen erlauben"
        }
    }

    private func publishPeripheralService() {
        guard peripheralManager.state == .poweredOn else { return }
        if peripheralService != nil { return }

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
        guard peripheralManager.state == .poweredOn else { return }
        if peripheralManager.isAdvertising { return }
        publishPeripheralService()
        peripheralManager.startAdvertising([
            CBAdvertisementDataServiceUUIDsKey: [Self.serviceUUID],
            CBAdvertisementDataLocalNameKey: "Ecosys"
        ])
    }

    func peripheralManager(_ peripheral: CBPeripheralManager,
                           didReceiveWrite requests: [CBATTRequest]) {
        for request in requests {
            guard request.characteristic.uuid == Self.txUUID,
                  let value = request.value else {
                peripheral.respond(to: request, withResult: .requestNotSupported)
                continue
            }
            processIncomingData(value)
            peripheral.respond(to: request, withResult: .success)
        }
    }

    func peripheralManager(_ peripheral: CBPeripheralManager,
                           central: CBCentral,
                           didSubscribeTo characteristic: CBCharacteristic) {
        guard characteristic.uuid == Self.rxUUID else { return }
        hasPeripheralSubscriber = true
        status = "Windows-Gerät verbunden"
    }

    func peripheralManager(_ peripheral: CBPeripheralManager,
                           central: CBCentral,
                           didUnsubscribeFrom characteristic: CBCharacteristic) {
        guard characteristic.uuid == Self.rxUUID else { return }
        hasPeripheralSubscriber = false
        if connected == nil {
            status = "Bluetooth bereit"
        }
    }
}
