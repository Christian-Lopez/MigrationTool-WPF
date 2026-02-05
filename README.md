# SQL Migration Tool - WPF Edition

## 🎯 Windows Server 2016 Compatible

This is a WPF migration of the original WinUI 3 SQL Migration Tool, specifically designed to work on **Windows Server 2016** and later versions.

## ✨ Features

- **Database Connection Management**: Connect to SQL Server, LocalDB, and remote databases
- **LocalDB Support**: Automatically resolves LocalDB named pipes for reliable connections
- **Data Migration**: Migrate tables between databases with optional filtering and row limits
- **Windows Authentication**: Full support for both Windows and SQL authentication
- **Server 2016 Compatibility**: Works on Windows Server 2016 and all newer versions

## 🔧 Requirements

- **.NET 8.0 Runtime** (or build as self-contained - see below)
- **Windows Server 2016** or later / Windows 10 or later
- **SQL Server** or **LocalDB** (optional)

## 📦 Installation

### Option 1: Run with .NET 8.0 Runtime

1. Install [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Extract the published folder
3. Run `MigrationTool.exe`

### Option 2: Self-Contained (No Runtime Required)

The self-contained version includes all dependencies:

1. Extract the `publish` folder
2. Run `MigrationTool.exe`
3. No additional installations needed!

## 🚀 Building from Source

### Prerequisites

- Visual Studio 2022 or later
- .NET 8.0 SDK

### Build Steps

```bash
# Clone or download the repository
git clone <your-repo-url>
cd MigrationTool-WPF

# Restore dependencies
dotnet restore

# Build
dotnet build -c Release

# Run
dotnet run
```

## 📤 Publishing

### For Windows Server 2016

```bash
# Self-contained (recommended for Server 2016)
dotnet publish -c Release -r win-x64 --self-contained true

# Framework-dependent (smaller, requires .NET 8.0 runtime)
dotnet publish -c Release -r win-x64 --self-contained false
```

Output will be in: `bin\Release\net8.0-windows\win-x64\publish\`

### Quick Publish Scripts

We've included helper scripts:

**Windows:**
```cmd
publish.bat
```

**PowerShell:**
```powershell
.\publish.ps1
```

## 🎨 Key Differences from WinUI Version

| Feature | WinUI 3 | WPF (This Version) |
|---------|---------|-------------------|
| **Min Windows** | Windows 10 1809 | Windows 7+ / Server 2016+ |
| **Deployment** | Complex (MSIX/Runtime) | Simple (copy folder) |
| **File Size** | ~5-10 MB | ~80-100 MB (self-contained) |
| **Settings Storage** | Windows Storage APIs | Simple .ini file |
| **Modern UI** | Yes | Styled to look modern |

## 💾 Settings Storage

Settings are stored in:
```
%APPDATA%\MigrationTool\connection_settings.ini
```

**Note:** Passwords are NOT saved for security. You'll need to re-enter them each session.

## 🔐 LocalDB Connection Fix

This version includes automatic LocalDB named pipe resolution to fix connection issues:

- Automatically detects `(localdb)\MSSQLLocalDB` connections
- Resolves to actual pipe name (e.g., `np:\\.\pipe\LOCALDB#...`)
- Works reliably on second and subsequent connections
- Includes connection pool management

## 🐛 Troubleshooting

### "Connection failed" errors

1. Verify SQL Server is running
2. Check firewall settings
3. For LocalDB, ensure it's started: `sqllocaldb start MSSQLLocalDB`
4. Try using the actual server name instead of localhost

### Application won't start on Server 2016

1. Ensure you're using the **self-contained** publish
2. Check Windows Update is current
3. Install [Visual C++ Redistributables](https://aka.ms/vs/17/release/vc_redist.x64.exe)

### "Table not found" errors

1. Ensure destination database has matching schema
2. Check table names match exactly (case-sensitive in some configurations)
3. Verify both connections are to the correct databases

## 📝 Usage

### 1. Configure Connections

1. Navigate to **Connections** tab
2. Enter source database details
3. Click **Verify Connection**
4. Repeat for destination database

### 2. Migrate Data

1. Navigate to **Data Migration** tab
2. Click **Refresh Tables** to load source tables
3. Select tables to migrate
4. (Optional) Set row limit or WHERE condition
5. Click **Start Migration**

## 🔄 Migration from WinUI

If you were using the original WinUI version:

- All connection logic is preserved
- LocalDB fixes are included
- Business logic is identical
- UI is similar but uses WPF controls

## 📜 License

MIT License - See LICENSE file for details

## 🤝 Contributing

Contributions are welcome! Please feel free to submit pull requests.

## 📧 Support

For issues or questions, please open an issue on GitHub.

---

**Built with ❤️ for Windows Server 2016 compatibility**
