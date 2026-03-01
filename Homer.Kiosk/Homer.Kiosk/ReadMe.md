# Homer.Kiosk - Uno Platform Mobile App

Welcome to Homer.Kiosk, a mobile home automation dashboard built with Uno Platform!

## Overview

Homer.Kiosk is a mobile application that provides a kiosk-style interface for controlling home automation devices. It is built using:

- **Uno Platform 6.5** - Cross-platform UI framework
- **C# Markup** - Declarative UI using C# instead of XAML
- **MVUX Pattern** - Model-View-Update eXtended for reactive state management
- **Material Design** - Google's Material Design theme
- **.NET 10.0** - Latest .NET framework

## Migration from Blazor

This project was migrated from the Homer.NetDaemon Blazor frontend. Key components include:

### Features
- **Power Controls**: Toggle switches for balcony lights and air conditioners
- **Weather Display**: Shows current weather information
- **Bus Timings**: Displays public transit information
- **Water Heater Control**: Monitor and control water heater
- **Toilet Status**: Check toilet occupancy status

### Architecture
- **MainPage.cs**: Main dashboard page using C# markup
- **MainModel.cs**: MVUX model containing state and commands
- **NetDaemon Integration**: Connected to Home Assistant via NetDaemon

## Platform Support

This app targets:
- **Android** - Mobile and tablet devices
- **iOS** - iPhone and iPad devices

## Building and Running

### Prerequisites
- .NET 10.0 SDK or later
- Uno Platform templates installed
- For Android: Android SDK
- For iOS: macOS with Xcode

### Build Commands

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run on Android (requires Android device/emulator)
dotnet build -f net10.0-android -t:Run

# Run on iOS (requires macOS with Xcode)
dotnet build -f net10.0-ios -t:Run
```

## Project Structure

```
Homer.Kiosk/
├── Presentation/          # UI pages and views (C# markup)
│   ├── MainPage.cs       # Main dashboard page
│   ├── MainModel.cs      # MVUX model for main page
│   └── Shell.cs          # Application shell
├── Services/             # Application services
├── Models/               # Data models
├── Assets/               # Images, fonts, and resources
└── Platforms/            # Platform-specific code
    ├── Android/
    └── iOS/
```

## Configuration

The app uses `appsettings.json` for configuration:
- API endpoints
- Home Assistant connection settings
- Feature flags

## C# Markup Examples

C# Markup allows you to define UI declaratively in C#:

```csharp
new Button()
    .Content("Toggle Light")
    .Command(() => vm.ToggleLight)
    .Background(Theme.Brushes.Primary.Default)
```

## Resources

- [Uno Platform Documentation](https://aka.platform.uno/get-started)
- [Uno.Sdk Information](https://aka.platform.uno/using-uno-sdk)
- [C# Markup Guide](https://aka.platform.uno/csharp-markup)
- [MVUX Documentation](https://aka.platform.uno/mvux)

## Development

To add new features or components:
1. Create a new model class in `Models/` or `Presentation/`
2. Create corresponding page using C# markup in `Presentation/`
3. Register services in `App.xaml.cs` or via dependency injection
4. Add navigation routes if needed

## Notes

- This is the initial migration from Blazor to Uno Platform
- NetDaemon integration is set up but requires connection configuration
- The UI matches the original Blazor dashboard layout and functionality
- Additional components can be migrated as needed