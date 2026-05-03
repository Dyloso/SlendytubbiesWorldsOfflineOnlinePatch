# Slendytubbies Worlds — Setup Guide

This mod restores offline singleplayer and self-hosted online multiplayer to Slendytubbies Worlds. Two separate mods are provided depending on how you want to play.

---

## Which Mod Do I Need?

| Mod | File | Use Case |
|-----|------|----------|
| **Offline Singleplayer** | `OfflineSingleplayer.dll` | Solo play, no internet required |
| **Online Multiplayer** | `OnlineMultiplayer.dll` | Play with friends over Hamachi |

You only need **one** of these at a time. Do not use both simultaneously.

---

## Requirements

- A copy of **Slendytubbies Worlds**
- [MelonLoader 0.6.6](https://github.com/LavaGang/MelonLoader/releases/tag/v0.6.6) — other versions are not compatible
- The mod file(s) from this repository

**For Online Multiplayer only:**
- [Light Reflective Mirror (LRM) Node v12](https://github.com/Derek-R-S/Light-Reflective-Mirror/releases) — required to run the relay server (host only)
- [LogMeIn Hamachi](https://vpn.net) — for connecting with friends over the internet
- .NET 5.0 Runtime — required to run the LRM server

---

## One-Time Setup

### 1. Install MelonLoader

1. Download **MelonLoader 0.6.6** from the link above
2. Run the installer and point it at your `SlendytubbiesWorlds.exe`
3. Launch the game once — MelonLoader will set itself up, then close automatically
4. You should now see `MelonLoader/` and `Mods/` folders in your game directory

### 2. Install The Mod

1. Copy either `OfflineSingleplayer.dll` or `OnlineMultiplayer.dll` into the `Mods/` folder
2. Only use **one mod at a time**

**For Online Multiplayer only:**

3. Launch the game once to generate the config file, then close it
4. Open `UserData/OfflineOnlinePatch.cfg` — it should contain:
   ```
   RelayIP=127.0.0.1
   ```

---

## Offline Singleplayer Setup

No additional setup needed. Simply:

1. Copy `OfflineSingleplayer.dll` into the `Mods/` folder
2. Launch the game and play

Both **Play Online** and **Join An Open World** work in singleplayer mode, including warping and fast travel between maps.

---

## Online Multiplayer Setup

### Set Up The LRM Relay Server (Host Only)

The host is the person who runs the relay server. Only one person needs to do this.

1. Download **LRM-Node.zip** from the releases page
2. Extract it to a folder, e.g. `C:\LRM\`
3. Right click `RunNode.bat` and select **Run as administrator** — it will generate a `config.json` file, then close
4. Open `config.json` and replace its contents with the following:
   ```json
   {
     "TransportDLL": "MultiCompiled.dll",
     "TransportClass": "Mirror.SimpleWebTransport",
     "AuthenticationKey": "Lemonade",
     "TransportPort": 7778,
     "UpdateLoopTime": 10,
     "UpdateHeartbeatInterval": 100,
     "UseEndpoint": true,
     "EndpointPort": 8080,
     "EndpointServerList": true,
     "EnableNATPunchtroughServer": true,
     "NATPunchtroughPort": 7776,
     "UseLoadBalancer": false,
     "LoadBalancerAuthKey": "AuthKey",
     "LoadBalancerAddress": "127.0.0.1",
     "LoadBalancerPort": 7070,
     "LoadBalancerRegion": 1
   }
   ```
5. Save and close the file

### Open Firewall Ports (Host Only)

Run the following commands in **PowerShell as Administrator**:

```powershell
netsh advfirewall firewall add rule name="LRM Relay TCP" dir=in action=allow protocol=TCP localport=7778
netsh advfirewall firewall add rule name="LRM Relay UDP" dir=in action=allow protocol=UDP localport=7778
netsh advfirewall firewall add rule name="LRM Endpoint" dir=in action=allow protocol=TCP localport=8080
```

### Set Up Hamachi

Both the host and all players need to complete these steps.

1. Download and install **Hamachi** from `vpn.net`
2. Create a free LogMeIn account and sign in to Hamachi

**Host:**
1. Click **Network → Create a new network**
2. Enter a network name and password
3. Share the network name and password with your friends
4. Note your **Hamachi IP** — shown under your name in the Hamachi window (e.g. `25.x.x.x`)

**Players:**
1. Click **Network → Join an existing network**
2. Enter the network name and password the host provided

### Fix Hamachi Firewall Issues

If players can't connect, run this in **PowerShell as Administrator** on **all machines**:

```powershell
netsh advfirewall firewall add rule name="Hamachi" dir=in action=allow program="C:\Program Files (x86)\LogMeIn Hamachi\hamachi-2.exe" enable=yes
netsh advfirewall firewall add rule name="Hamachi Out" dir=out action=allow program="C:\Program Files (x86)\LogMeIn Hamachi\hamachi-2.exe" enable=yes
netsh advfirewall firewall add rule name="Hamachi Network" dir=in action=allow remoteip=25.0.0.0/8
netsh advfirewall firewall add rule name="Hamachi Network Out" dir=out action=allow remoteip=25.0.0.0/8
```

To verify Hamachi is working, right click a friend in Hamachi and click **Diagnose** — both Local and Peer results should show **OK**.

---

## Every Session (Online Multiplayer)

### Host

1. Open Hamachi and confirm friends show as connected (green dot)
2. Right click `RunNode.bat` and select **Run as administrator** — keep this window open while playing
3. Open `UserData/OfflineOnlinePatch.cfg` and set your Hamachi IP:
   ```
   RelayIP=25.x.x.x
   ```
4. Launch the game

### Players

1. Open Hamachi and confirm you are connected to the host's network (green dot)
2. Open `UserData/OfflineOnlinePatch.cfg` and set the **host's** Hamachi IP:
   ```
   RelayIP=25.x.x.x
   ```
3. Launch the game

---

## Playing

### Play Online (Multiplayer)

1. Click **Play Online** from the main menu
2. **Host:** Click **Create a Room**, configure your room, and start
3. **Players:** Click **Refresh** and join the host's room from the list

### Join An Open World (Multiplayer)

1. Click **Join An Open World** from the main menu
2. **First player to join a world** becomes the host for that world automatically
3. **Other players** click **Join** on the same world to join the existing session

---

## Checking Your Online Status

The bottom right corner of the title screen shows the current player count. If it says **"0 Players Online"** you are in offline mode and multiplayer will not be available. If it shows **"1 Player Online"** or more, you are connected to the relay and multiplayer is ready to use.

If you launched the game before starting the LRM server, you can get online without restarting — simply join any game mode and return to the title screen. This will trigger a fresh connection check and the player count should update to reflect your online status.

---

## Solo Play

The **Offline Singleplayer** mod works without Hamachi or the LRM server. Simply launch the game and play normally.

If using the **Online Multiplayer** mod without the LRM server running, the game will wait 5 seconds then let you in anyway in offline mode.

---

## Known Limitations

- **Usernames are non-functional** — player display names will not show correctly in-game. This is due to bypassing the original authentication server.
- **In-game chat is non-functional** — for the same reason as above.

A fix for both may be added in a future update based on community feedback.

---

## Troubleshooting

**Stuck on connecting screen at startup:**
- The relay server is not running or unreachable
- Wait 5 seconds — it will time out and let you in
- For solo play this is normal

**Room list is empty:**
- Make sure the host has `RunNode.bat` running as administrator
- Make sure both players have the correct Hamachi IP in `OnlineMultiplayer.cfg`
- Try clicking the **Refresh** button in the room list

**Can't connect to relay:**
- Check Windows Firewall — make sure ports 7778 and 8080 are open on the host's machine
- Make sure Hamachi shows both players as connected (green dot)
- Run the Hamachi Diagnose check — both sides should show OK

**Open World players not seeing each other:**
- Make sure both players join the **same world slot** (e.g. both click World 1)
- The first player to join creates the session — others must join after

**Game crashes on startup:**
- Make sure you are using **MelonLoader 0.6.6** — this is the version known to work

**Hamachi service stopped:**
- Open `services.msc`, find **Hamachi Tunneling Engine**, and start it
- Or open Hamachi's **Self-Diagnosis** tool and click **Start**

---

## File Reference

| File | Location | Purpose |
|------|----------|---------|
| `OfflineSingleplayer.dll` | `Mods/` | Offline singleplayer mod |
| `OnlineMultiplayer.dll` | `Mods/` | Online multiplayer mod |
| `OfflineOnlinePatch.cfg` | `UserData/` | Relay IP configuration (multiplayer only) |
| `RunNode.bat` | LRM folder | Starts the relay server (run as administrator) |
| `config.json` | LRM folder | LRM server configuration |

---

## Credits

Reverse engineered and developed using MelonLoader, Harmony, Unity Explorer, ILSpy, dotPeek, and Wireshark. The game uses Mirror networking with Light Reflective Mirror as the relay transport.