# API Contract: Coordinator <-> Game Servers / Clients

This file is the single source of truth for how the coordinator (Node/Express)
talks to the Unity game servers and Unity clients. If you need to change
anything here, edit this file in the same commit and tell the team.

## General rules

- Base URL (local development): `http://localhost:3000`
- All requests and responses use JSON (`Content-Type: application/json`).
- Field names are `camelCase`. `port` is always a **number**, never a string.
- IDs (`serverId`, `playerId`, `matchId`) are strings.
- Success with no body returns `200 OK`.
- Errors return a non-200 status and `{ "error": "message" }`.

## Shared constants

| Name                | Value  | Notes                                              |
|---------------------|--------|----------------------------------------------------|
| Heartbeat interval  | 2 s    | How often each game server calls `/heartbeat`      |
| Heartbeat timeout   | 6 s    | No heartbeat for this long -> server is unhealthy  |
| Timeout check rate  | 1 s    | How often the coordinator checks for timeouts      |
| Client poll rate    | 1 s    | How often a client polls `/match/:playerId`        |
| Players per match   | 2      |                                                    |
| Matches per server  | 1      | One match at a time per server process             |

---

## Game server -> Coordinator

### POST /register

Called once when a game server starts (and again if a heartbeat returns 404).

Request:
```json
{ "serverId": "a1b2c3", "ip": "127.0.0.1", "port": 7777 }
```

Response: `200 OK`

Notes:
- Registering an already-known `serverId` updates its `ip`/`port`, resets
  `load` to 0, and marks it healthy.
- `ip` is the address clients should connect to.

### POST /heartbeat

Called every 2 s by each game server.

Request:
```json
{ "serverId": "a1b2c3", "activeMatches": 0 }
```

Response:
- `200 OK` on success.
- `404` with `{ "error": "unknown server" }` if the `serverId` was never
  registered. The server must then call `/register` again.

Notes:
- The coordinator stores `activeMatches` as the server's `load`, records
  `lastSeen = now`, and marks the server healthy again if it had timed out.
- `activeMatches` is the server's own count and overrides the coordinator's
  estimate. A server that finishes a match reports `0` on its next heartbeat.

---

## Client -> Coordinator

### POST /queue

Called when the player presses "Find Match".

Request:
```json
{ "playerId": "player-42" }
```

Response: `200 OK`

Notes:
- Calling it again for a player who is already waiting does nothing (no
  duplicate entries).
- Calling it clears any old match result stored for that `playerId`, so a
  player can queue for a new game after finishing one.
- The coordinator pairs players in the order they joined (FIFO).

### POST /cancel

Called if the player backs out of the queue before being matched.

Request:
```json
{ "playerId": "player-42" }
```

Response: `200 OK` (also returned if the player was not queued).

### GET /match/:playerId

Polled by the client every 1 s after `/queue`.

Response while waiting:
```json
{ "status": "waiting" }
```

Response once matched:
```json
{ "status": "ready", "ip": "127.0.0.1", "port": 7777, "matchId": "1732300000000" }
```

Notes:
- Both players of a pair receive the same `ip`, `port` and `matchId`.
- Once `status` is `ready`, the client sets the transport address and
  connects directly to the game server using Netcode for GameObjects.
  The coordinator is no longer involved in gameplay.
- Unknown `playerId` returns `{ "status": "waiting" }` (not an error).

---

## Debug / evaluation endpoints

Used by the testing tools and for the report. Not called by the game.

### GET /servers

Response:
```json
[
  { "serverId": "a1b2c3", "ip": "127.0.0.1", "port": 7777,
    "load": 1, "healthy": true, "lastSeenMsAgo": 850 }
]
```

### GET /health

Response: `{ "status": "ok" }`

---

## Assignment rules (coordinator behaviour)

1. Only servers with `healthy = true` are eligible.
2. Pick the eligible server with the lowest `load`. Ties are broken by whichever
   server registered first.
3. Servers with `load >= 1` are skipped (one match per server). If no server is
   free, the pair stays in the queue and is retried on the next pairing check.
4. When a match is assigned, the coordinator immediately increases that
   server's `load` by 1 (without waiting for the next heartbeat).
5. A server that misses heartbeats for more than 6 s is marked unhealthy and
   receives no new matches. It becomes eligible again after a successful
   `/heartbeat` or `/register`.
6. Matches already running on a server that goes unhealthy are not recovered.
   Clients lose their connection and must queue again (documented as a
   limitation in the report).

## Game server <-> Client (Netcode for GameObjects)

Not part of this HTTP API, but both sides must agree on:
- The server listens on the `port` it registered with.
- The server accepts at most 2 client connections.
- Clients send input only. The server decides movement results, damage,
  score and match end.

## Changelog

| Date       | Change              | By |
|------------|---------------------|----|
| 2026-11-22 | Initial version     |    |
