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

        if let txCharacteristic, let connected {
            connected.writeValue(data, for: txCharacteristic, type: .withResponse)
            lastMessage = "Hello gesendet"
            return
        }

        if hasPeripheralSubscriber, let peripheralRxCharacteristic,
           peripheralManager.updateValue(data, for: peripheralRxCharacteristic, onSubscribedCentrals: nil) {
            lastMessage = "Hello gesendet"
            return
        }

        lastMessage = "Kein Gerät verbunden."
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
        let name = peripheral.name ?? localName ?? "Ecosys device"

        // Keep discovery broad for unreliable advertisements, but only expose
        // devices that identify themselves as Ecosys or advertise our service.
        let looksLikeEcosys = hasEcosysService ||
            name.localizedCaseInsensitiveContains("ecosys") ||
            (localName?.localizedCaseInsensitiveContains("ecosys") ?? false)
        guard looksLikeEcosys else { return }

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
        lastMessage = String(data: data, encoding: .utf8) ?? "Nachricht empfangen"
    }
}

extension BluetoothManager: CBPeripheralManagerDelegate {
    func peripheralManagerDidUpdateState(_ peripheral: CBPeripheralManager) {
        if peripheral.state == .poweredOn {
            publishPeripheralService()
            startAdvertisingIfReady()
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

    private func startAdvertisingIfReady() {
        guard peripheralManager.state == .poweredOn else { return }
        if peripheralManager.isAdvertising { return }
        publishPeripheralService()
        peripheralManager.startAdvertising([
            CBAdvertisementDataServiceUUIDsKey: [Self.serviceUUID],
            CBAdvertisementDataLocalNameKey: UIDevice.current.name
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
            lastMessage = String(data: value, encoding: .utf8) ?? "Nachricht empfangen"
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
