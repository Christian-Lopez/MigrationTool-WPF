# Migration Guide: WinUI to WPF

## Overview

This document explains the changes made when migrating from WinUI 3 to WPF for Windows Server 2016 compatibility.

## Key Changes

### 1. Framework

**WinUI 3:**
```xml
<TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
<UseWinUI>true</UseWinUI>
```

**WPF:**
```xml
<TargetFramework>net8.0-windows</TargetFramework>
<UseWPF>true</UseWPF>
```

### 2. XAML Namespace Changes

**WinUI 3:**
```xml
xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
xmlns:local="using:MigrationTool"
```

**WPF:**
```xml
xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
xmlns:local="clr-namespace:MigrationTool"
```

### 3. UI Controls

| WinUI Control | WPF Equivalent |
|---------------|----------------|
| `InfoBar` | Custom `Border` with `TextBlock` |
| `NavigationView` | Custom navigation buttons |
| `Page` | `Page` (same) |
| `PasswordBox` | `PasswordBox` (same) |
| `TextBox.PlaceholderText` | Use watermark or custom template |
| `NumberBox` | `TextBox` with validation |
| `AutoSuggestBox` | `TextBox` with `TextChanged` |

### 4. Code-Behind Changes

**WinUI 3:**
```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;

// Dispatcher
DispatcherQueue.TryEnqueue(() => { ... });

// InfoBar
StatusInfoBar.Severity = InfoBarSeverity.Success;
```

**WPF:**
```csharp
using System.Windows;
using System.Windows.Controls;
// No Windows.Storage

// Dispatcher
Dispatcher.Invoke(() => { ... });

// Custom status
ShowStatus("Message", true);
```

### 5. Settings Storage

**WinUI 3:**
```csharp
var localSettings = ApplicationData.Current.LocalSettings;
localSettings.Values["key"] = value;

var vault = new PasswordVault();
vault.Add(credential);
```

**WPF (Simple .ini file):**
```csharp
File.WriteAllLines(SettingsFilePath, lines);
// Note: Passwords NOT saved for security
```

### 6. Preserved Features

✅ All database connection logic (identical)
✅ LocalDB pipe name resolution
✅ SqlConnection and SqlBulkCopy code
✅ Business logic in `ConnectionsService`
✅ `TableMetadata` model
✅ Error handling patterns

### 7. Deployment Differences

**WinUI 3:**
- Requires Windows App SDK Runtime
- MSIX packaging recommended
- Complex deployment

**WPF:**
- Simple folder copy
- Optional self-contained
- Works on Server 2016+

## File-by-File Changes

### App.xaml
- Changed namespace
- Added WPF styles
- Removed WinUI-specific resources

### MainWindow.xaml
- Changed from NavigationView to simple buttons
- Uses Frame for page navigation
- Custom header with navigation buttons

### ConnectionsPage.xaml
- Replaced `InfoBar` with custom `Border` + `TextBlock`
- Removed `PlaceholderText` (or used Label instead)
- Changed `CaptionTextBlockStyle` to regular `Label`
- Simplified binding syntax

### ConnectionsPage.xaml.cs
- Changed `using Microsoft.UI.Xaml` → `using System.Windows`
- Changed `InfoBarSeverity` → `bool isSuccess`
- Changed settings from `ApplicationData` → File I/O
- Changed `DispatcherQueue` → `Dispatcher`

### DataMigrationPage.xaml
- Replaced WinUI `DataGrid` with WPF `DataGrid`
- Removed `NumberBox` → used `TextBox`
- Simplified `AutoSuggestBox` → `TextBox`

### DataMigrationPage.xaml.cs
- Updated Dispatcher calls
- Changed `int.Parse(NumberBox.Value)` → `int.TryParse(TextBox.Text)`
- All SqlClient code unchanged

## What Stayed the Same

- ✅ All SQL Server connection logic
- ✅ LocalDB named pipe resolution
- ✅ Bulk copy implementation
- ✅ Error handling
- ✅ Data models
- ✅ Business logic

## Testing Checklist

After migration, test:

- [ ] Source connection (Windows Auth)
- [ ] Source connection (SQL Auth)
- [ ] Destination connection
- [ ] LocalDB connection resolution
- [ ] Table listing
- [ ] Table filtering/search
- [ ] Single table migration
- [ ] Multiple table migration
- [ ] Progress bar updates
- [ ] Error handling
- [ ] Settings persistence
- [ ] Run on Windows Server 2016

## Performance

No performance differences expected. All database operations use the same `Microsoft.Data.SqlClient` library.

## Known Limitations

1. **Password storage**: Not saved (security decision)
2. **UI modernization**: Less modern than WinUI but acceptable
3. **Navigation**: Simpler than NavigationView
4. **Progress notifications**: Less fancy than WinUI

## Benefits

1. ✅ Works on Windows Server 2016
2. ✅ Simple deployment (copy folder)
3. ✅ No Windows App SDK needed
4. ✅ Smaller runtime footprint
5. ✅ Better compatibility across Windows versions

## Conclusion

The migration preserves all critical functionality while ensuring compatibility with Windows Server 2016. The UI is slightly simplified but remains functional and professional.
