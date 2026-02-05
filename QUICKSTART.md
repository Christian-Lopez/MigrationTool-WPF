# Quick Start Guide

## For Developers

### Option 1: Open in Visual Studio 2022

1. Open `MigrationTool.sln`
2. Build the solution (F6)
3. Run (F5)

### Option 2: Command Line

```bash
# Build
dotnet build

# Run
dotnet run

# Publish for distribution
dotnet publish -c Release -r win-x64 --self-contained true
```

### Option 3: Use Publish Scripts

**Windows:**
```cmd
publish.bat
```

**PowerShell:**
```powershell
.\publish.ps1
```

## For End Users (Windows Server 2016+)

1. Extract the publish folder
2. Run `MigrationTool.exe`
3. No installation required!

## First Time Setup

1. **Configure Source Database**
   - Click "Connections" tab
   - Enter source server details
   - Click "Verify Connection"

2. **Configure Destination Database**
   - Enter destination server details  
   - Click "Verify Connection"

3. **Migrate Data**
   - Click "Data Migration" tab
   - Click "Refresh Tables"
   - Select tables to migrate
   - Click "Start Migration"

## LocalDB Users

For LocalDB connections, use:
```
Server: (localdb)\MSSQLLocalDB
```

The app will automatically resolve the named pipe for reliable connections.

## Troubleshooting

**Can't connect to LocalDB?**
```cmd
sqllocaldb start MSSQLLocalDB
```

**App won't start on Server 2016?**
- Make sure you're using the self-contained publish
- Install Visual C++ Redistributables if needed

**Connection timeout?**
- Increase the timeout in code (currently 30 seconds)
- Check firewall settings
- Verify SQL Server is running

## Support

For issues, check:
1. README.md - Full documentation
2. MIGRATION_GUIDE.md - Technical details
3. GitHub issues

---

**Happy migrating! 🚀**
