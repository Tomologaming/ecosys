# Ecosys Message Protocol v1

## Envelope

Every control message uses an envelope:

```json
{
  "protocol": "ecosys/1",
  "type": "hello",
  "messageId": "01J...",
  "sender": "device_01...",
  "timestamp": 1770000000000,
  "payload": {}
}
```

Fields:
- `protocol`: required; current value `ecosys/1`
- `type`: required message type
- `messageId`: required unique message ID
- `sender`: required stable Ecosys device ID
- `timestamp`: required Unix epoch time in milliseconds
- `payload`: required type-specific object

Unknown fields MUST be ignored. Required fields MUST be validated before dispatch.

## Core messages

### hello
Sent after a transport connection is established. Payload contains `deviceName`, `platform`, and `capabilities`.

### hello.ack
Acknowledges hello and communicates the responder's protocol capabilities.

### ping / pong
Keep-alive and connection diagnostics. Payload is an empty object.

### pairing.request / pairing.response
These messages belong to the device trust flow. A transport connection alone MUST NOT be treated as authorization. The cryptographic handshake will be specified before sensitive data is exchanged.

### error
Payload contains a stable error code and a human-readable message.

## Future application messages
- `clipboard.update`
- `file.offer`
- `file.accept`
- `file.chunk`
- `file.complete`

Large binary data SHOULD NOT be embedded as base64 in ordinary control messages. A future transport layer will define framed binary streams.
