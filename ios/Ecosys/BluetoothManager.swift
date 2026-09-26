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
    // BLE service used by the first iOS implementation. This is intentionally
    // separate from the Android/Windows Classic RFCOMM UUID.
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

    override init() {
        super.init()
        central = CBCentralManager(delegate: self, queue: .main)
        peripheralManager = CBPeripheralManager(delegate: self, queue: .main)
    }

    func scan() {
        guard central.state == .poweredOn else { return }
        devices.removeAll()
        isScanning = true
        status = "Suche nach Ecosys-Geräten…"
        central.scanForPeripherals(withServices: [Self.serviceUUID], options: [
            CBCentralManagerScanOptionAllowDuplicatesKey: false
        ])
        DispatchQueue.main.asyncAfter(deadline: .now() + 8) { [weak self] in
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
        device.peripheral.delegate = self
        central.connect(device.peripheral)
    }

    func sendHello() {
        guard let txCharacteristic, let connected else {
            lastMessage = "Kein Gerät verbunden."
            return
        }
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
        connected.writeValue(data, for: txCharacteristic, type: .withResponse)
        lastMessage = "Hello gesendet"
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
            status = "Bluetooth-Berechtigung fehlt"
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
        let name = peripheral.name ?? (advertisementData[CBAdvertisementDataLocalNameKey] as? String) ?? "Ecosys device"
        devices.append(EcosysDevice(id: peripheral.identifier, name: name, detail: "Bluetooth Low Energy", icon: "antenna.radiowaves.left.and.right", peripheral: peripheral))
    }

    func centralManager(_ central: CBCentralManager, didConnect peripheral: CBPeripheral) {
        status = "Verbunden mit \(peripheral.name ?? "Ecosys device")"
        peripheral.discoverServices([Self.serviceUUID])
    }

    func centralManager(_ central: CBCentralManager, didFailToConnect peripheral: CBPeripheral, error: Error?) {
        status = "Verbindung fehlgeschlagen"
    }
}

extension BluetoothManager: CBPeripheralDelegate {
    func peripheral(_ peripheral: CBPeripheral, didDiscoverServices error: Error?) {
        guard let service = peripheral.services?.first(where: { $0.uuid == Self.serviceUUID }) else { return }
        peripheral.discoverCharacteristics([Self.txUUID, Self.rxUUID], for: service)
    }

    func peripheral(_ peripheral: CBPeripheral, didDiscoverCharacteristicsFor service: CBService, error: Error?) {
        txCharacteristic = service.characteristics?.first(where: { $0.uuid == Self.txUUID })
        let rx = service.characteristics?.first(where: { $0.uuid == Self.rxUUID })
        if let rx {
            peripheral.setNotifyValue(true, for: rx)
        }
    }

    func peripheral(_ peripheral: CBPeripheral, didUpdateValueFor characteristic: CBCharacteristic, error: Error?) {
        guard let data = characteristic.value else { return }
        lastMessage = String(data: data, encoding: .utf8) ?? "Nachricht empfangen"
    }
}


extension BluetoothManager: CBPeripheralManagerDelegate {
    func peripheralManagerDidUpdateState(_ peripheral: CBPeripheralManager) {
        if peripheral.state == .poweredOn {
            publishPeripheralService()
        } else if peripheral.state == .unauthorized {
            status = "Bluetooth-Berechtigung fehlt"
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
        status = "Windows-Gerät verbunden"
    }

    func peripheralManager(_ peripheral: CBPeripheralManager,
                           central: CBCentral,
                           didUnsubscribeFrom characteristic: CBCharacteristic) {
        if connected == nil {
            status = "Bluetooth bereit"
        }
    }
}
