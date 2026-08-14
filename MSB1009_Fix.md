# Issue Resolution: MSBUILD error MSB1009 (Project file does not exist)

## Issue Description
When attempting to run the .NET Web API server project using the command:
```powershell
dotnet run --project SessionManagement.Server
```
inside the `SessionManagement.Server` directory, the build fails with:
```text
MSBUILD : error MSB1009: Project file does not exist.
```

---

## Root Cause Analysis
1. You navigated inside the project folder:
   ```powershell
   cd SessionManagement.Server
   ```
2. Once inside `SessionManagement.Server/`, the project file [`SessionManagement.Server.csproj`](file:///c:/Users/dell/OneDrive/Desktop/pju/code/SessionManagement/SessionManagement.Server/SessionManagement.Server.csproj) is located in the current directory (`./`).
3. Running `--project SessionManagement.Server` forced .NET to search for a nested subfolder or project named `SessionManagement.Server` inside the current directory (`SessionManagement.Server/SessionManagement.Server`). Because no such path exists, MSBuild threw error `MSB1009`.

---

## How to Run the Project Correctly

### Method 1: Running inside `SessionManagement.Server` folder (Recommended)
If your terminal is already inside `SessionManagement.Server`:
```powershell
dotnet run
```
*`dotnet run` will automatically detect `SessionManagement.Server.csproj` in the current folder.*

Or explicitly specify the `.csproj` file name:
```powershell
dotnet run --project SessionManagement.Server.csproj
```

### Method 2: Running from the Root Workspace (`SessionManagement`)
If your terminal is at the workspace root (`SessionManagement`):
```powershell
dotnet run --project SessionManagement.Server
```
