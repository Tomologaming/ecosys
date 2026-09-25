# Protocol Versioning

The `protocol` field uses `ecosys/<major>`. A major version change may introduce incompatible wire-level behavior.

## Compatibility rules
1. Receivers MUST reject unsupported major versions explicitly.
2. New optional fields may be added without changing the major version.
3. Unknown JSON fields MUST be ignored.
4. New message types MUST be handled as unsupported when not recognized.
5. Security-sensitive changes require an explicit protocol/security version update.
6. Application capabilities are negotiated separately from the protocol version.

## Transport independence

The protocol does not encode Bluetooth-specific concepts into application messages.

A transport adapter handles discovery, connection establishment, framing/stream delivery, reconnect behavior, and transport-specific errors.

The protocol layer handles identity, message validation, session state, capability negotiation, and application semantics.

This separation allows Bluetooth to be replaced or supplemented by LAN/Wi-Fi or USB later.
