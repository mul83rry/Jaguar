# Jaguar - WebSocket-based Multiplayer Game Server Framework

## Table of Contents
1. [Overview](#overview)
2. [Key Features](#key-features)
3. [Architecture](#architecture)
4. [Installation](#installation)
5. [Quick Start Guide](#quick-start-guide)
6. [Core Concepts](#core-concepts)
7. [API Reference](#api-reference)
8. [Advanced Usage](#advanced-usage)
9. [Best Practices](#best-practices)
10. [Strengths](#strengths)
11. [Weaknesses](#weaknesses)
12. [Troubleshooting](#troubleshooting)

---

## Overview

**Jaguar** is a .NET 8.0 framework designed specifically for building real-time multiplayer game servers using WebSocket protocol. It provides a comprehensive infrastructure for managing users, rooms, rounds, matchmaking, and message routing in online multiplayer games.

### What is Jaguar?

Jaguar abstracts away the complexities of WebSocket communication and game session management, allowing developers to focus on game logic rather than infrastructure. It supports both UDP-like fire-and-forget messaging and request-response patterns.

### Target Audience

- Game developers building multiplayer games in C#/.NET
- Developers who need a robust room-based game server
- Projects requiring real-time communication with structured game sessions

---

## Key Features

### 1. **WebSocket-Based Communication**
- Built on top of .NET's `HttpListener` and `WebSocket` APIs
- Binary message format for efficient data transfer
- Maximum buffer size of 8000 bytes per message
- Automatic client connection management

### 2. **Room and Round System**
- **Rooms**: Game sessions that can host multiple players
- **Rounds**: Individual game rounds within a room
- Support for multiple rounds per room
- Automatic round progression and game completion detection

### 3. **Advanced Matchmaking**
- Random room joining
- Type-based room matching (`TypeId`)
- Level-based matchmaking using ranges
- Password-protected private rooms
- Public/Private access control

### 4. **User Management**
- Unique user identification system
- User state tracking (online/offline)
- Multiple room participation support
- Current room tracking
- Rejoin functionality for disconnected users

### 5. **Event-Driven Architecture**
- Custom listener registration system
- Type-safe message handling
- Support for both registered and unregistered user listeners
- Request-response pattern support with callbacks

### 6. **Score Tracking System**
- Per-round score management
- Total score calculation across rounds
- Individual user score tracking

### 7. **Lifecycle Hooks**
- `OnNewClientJoined`: Fired when a new client connects
- `OnClientExited`: Fired when a client disconnects
- `NewUserJoinedAsync`: Called when a user joins a room
- `RoomReadyForStartAsync`: Called when minimum users join
- `RoundStartedAsync`: Called when a new round starts
- `GameCompletedAsync`: Called when all rounds complete
- `UserExitedAsync`: Called when a user leaves
- `UserKickedAsync`: Called when a user is kicked
- `OnUserRejoinedAsync`: Called when a user reconnects

---

## Architecture

### Core Components

```
┌─────────────────────────────────────────────────┐
│                   Server                        │
│  - WebSocket Management                         │
│  - Listener Registration                        │
│  - Event Routing                                │
└────────────────┬────────────────────────────────┘
                 │
        ┌────────┴────────┐
        │                 │
   ┌────▼─────┐     ┌────▼─────┐
   │  Room    │     │  User    │
   │ Manager  │     │ Manager  │
   └────┬─────┘     └────┬─────┘
        │                 │
   ┌────▼─────┐     ┌────▼─────┐
   │   Room   │     │   User   │
   │          │     │          │
   │ - Rounds │     │ - Client │
   │ - Users  │     │ - Rooms  │
   │ - Score  │     │ - Score  │
   └──────────┘     └──────────┘
```

### Key Classes

#### 1. **Server**
- Entry point for the framework
- Manages WebSocket connections
- Routes messages to appropriate listeners
- Provides static methods for sending messages

#### 2. **Room** (Abstract)
- Represents a game session
- Manages users, rounds, and game state
- Must be extended by your custom room implementation
- Provides lifecycle hooks for game events

#### 3. **User** (Abstract)
- Represents a connected player
- Tracks user state and room participation
- Must be extended by your custom user implementation
- Provides methods for sending messages and managing scores

#### 4. **Round**
- Represents a single round within a room
- Manages round-specific score tracking
- Automatically created when `StartRoundAsync()` is called

#### 5. **RoomManager**
- Static manager for all active rooms
- Provides room discovery and matchmaking
- Handles room lifecycle

#### 6. **UsersManager**
- Static manager for all connected users
- Provides user lookup by ID or predicate
- Tracks online/offline status

#### 7. **Listeners**
- `UnRegisteredUserListener<TRequest>`: For non-authenticated clients
- `RegisteredUserListener<TUser, TRequest>`: For authenticated users
- `RegisteredUserListener<TUser, TRequest, TResponse>`: With response callback

---

## Installation

### Prerequisites
- .NET 8.0 SDK or higher
- Visual Studio 2022 or VS Code with C# extension

### NuGet Installation
```bash
dotnet add package Jaguar
```

### From Source
```bash
git clone https://github.com/mul83rry/Jaguar.git
cd Jaguar
dotnet build
```

---

## Quick Start Guide

See [QUICK_START.md](QUICK_START.md) for detailed examples on:
- Creating User and Room classes
- Implementing message listeners
- Starting the server
- Client-side connection

---

## Core Concepts

### 1. Message Flow

```
Client → [Sender ID (7 bytes)] + [EventId (1 byte)] + [JSON Message] + [EOF (200)]
↓
Server decodes → Finds Listener → Invokes Handler
↓
Handler processes → Optionally sends response
↓
Server → [EventId] + [JSON Response] + [EOF] → Client
```

### 2. Event IDs

- `0`: None (ignored)
- `1`: Join (initial connection)
- `2`: Already used (reserved)
- `3`: Listener name map (handshake)
- `4+`: Custom listeners (auto-assigned)

### 3. Room Lifecycle

```
Room Created
    ↓
Users Join → NewUserJoinedAsync()
    ↓
Min Users Reached → RoomReadyForStartAsync()
    ↓
StartRoundAsync() → RoundStartedAsync()
    ↓
[Game Logic]
    ↓
Round.RoundComplete()
    ↓
StartRoundAsync() (next round) or GameCompletedAsync()
    ↓
Room Destroyed
```

---

See [API_REFERENCE.md](API_REFERENCE.md) for complete API documentation.
See [ADVANCED_USAGE.md](ADVANCED_USAGE.md) for advanced patterns and examples.
See [STRENGTHS_AND_WEAKNESSES.md](STRENGTHS_AND_WEAKNESSES.md) for detailed analysis.
