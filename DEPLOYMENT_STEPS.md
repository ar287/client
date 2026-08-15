# Session Management System - Deployment Steps

This document provides complete, step-by-step instructions for deploying both the **Server/Admin Environment** and standalone **Client PCs**.

---

## 🖥️ 1. Server PC Setup (Host Machine)

### Requirements on Server PC:
* Windows x64 OS
* .NET 9.0 SDK
* SQL Server (LocalDB or Express / Enterprise)
* Ollama AI (if AI features enabled)

### Setup Steps:
1. Open SQL Server Management Studio (SSMS) or SQL CMD and execute:
   * `DatabaseSetup.sql`
2. Open terminal in workspace root and start Server:
   ```cmd
   cd SessionManagement.Server
   dotnet run
   ```
   *(Server starts listening on `http://localhost:5102` or `http://0.0.0.0:5102`)*.
3. Open a second terminal and start Admin Dashboard:
   ```cmd
   cd SessionManagement.Admin
   dotnet run
   ```

---

## 💻 2. Client PC Deployment (Standalone Ready-to-Run)

### Requirements on Client PC:
* **Zero Software Installations Required!**
* **NO** Visual Studio
* **NO** .NET SDK or .NET Runtime
* **NO** SQL Server / SSMS
* **NO** Ollama
* Network connection to Server PC (LAN / Wi-Fi)

---

### Step-by-Step Client Deployment:

#### Step 1: Copy Deployment Package
Copy the entire `ClientPC/` directory to the target Client PC (e.g. `C:\ClientPC` or Desktop).

#### Step 2: Configure Server IP
1. On the Server PC, open command prompt and run `ipconfig` to find its LAN IPv4 address (e.g. `192.168.1.50`).
2. On the Client PC, navigate to:
   ```text
   ClientPC/Client/appsettings.json
   ```
3. Open `appsettings.json` in Notepad and update the `BaseUrl`:
   ```json
   {
     "Server": {
       "BaseUrl": "http://192.168.1.50:5102"
     }
   }
   ```
4. Save and close `appsettings.json`.

#### Step 3: Launch Client Application
Double-click:
```text
ClientPC/Start-Client.bat
```
The Client application will launch immediately and connect to the Server.

---

## 🛡️ Firewall & Network Troubleshooting

If the Client PC cannot connect to the Server PC:
1. Ensure both PCs are connected to the same local network.
2. Ensure Windows Firewall on the Server PC permits incoming traffic on port **5102**.
3. Test network connectivity from Client PC command prompt:
   ```cmd
   ping 192.168.1.50
   ```
