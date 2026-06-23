# Homer - Smart Home Dashboard

Homer is a comprehensive smart home dashboard solution that provides both web-based and mobile interfaces for controlling home automation systems via Home Assistant and NetDaemon.

## Projects

### Homer.NetDaemon
A Blazor-based web application that provides a kiosk-style dashboard for controlling home automation devices. This is the original web interface.

**Technology Stack:**
- **Blazor Server/WebAssembly** - Interactive web UI
- **BootstrapBlazor** - UI component library
- **NetDaemon** - Home Assistant integration framework
- **.NET 10.0** - Latest .NET framework

**Features:**
- Real-time device control (lights, air conditioners, water heater)
- Weather information display
- Public transit (bus) timing information
- Interactive dashboard with Chinese language support
- Responsive design for various screen sizes

**Location:** `/Homer.NetDaemon/`

### Homer.Kiosk (NEW)
A cross-platform mobile application built with Uno Platform, providing native mobile access to home automation controls.

**Technology Stack:**
- **Uno Platform 6.5** - Cross-platform UI framework
- **C# Markup** - Declarative UI using C# instead of XAML
- **MVUX Pattern** - Model-View-Update eXtended for reactive state management
- **Material Design** - Google's Material Design theme
- **NetDaemon** - Home Assistant integration
- **.NET 10.0** - Latest .NET framework

**Supported Platforms:**
- Android (phones and tablets)
- iOS (iPhone and iPad)

**Features:**
- Native mobile interface for home controls
- Power button controls for lights and air conditioners
- Weather and transit information display
- Material Design theming
- C# markup for type-safe UI development

**Location:** `/Homer.Kiosk/`

### Homer.NetDaemon.Client
Client-side Blazor WebAssembly components used by Homer.NetDaemon.

**Location:** `/Homer.NetDaemon.Client/`

### Homer.ServiceDefaults
Shared service configuration and defaults used across projects.

**Location:** `/Homer.ServiceDefaults/`

## Getting Started

### Prerequisites
- **.NET 10.0 SDK** or later
- **Home Assistant** instance with configured devices
- For mobile development:
  - Android SDK (for Android)
  - macOS with Xcode (for iOS)

### Configuration

Both projects use Home Assistant for device integration. You'll need to configure:

1. **Home Assistant URL and Token**
   - Set in user secrets for Homer.NetDaemon
   - Configure in appsettings.json for Homer.Kiosk

2. **NetDaemon Code Generation**
   ```bash
   just codegen
   ```
   This generates C# entities from your Home Assistant instance.

### Building Homer.NetDaemon (Web)

```bash
cd Homer.NetDaemon
dotnet restore
dotnet build
dotnet run
```

Then navigate to the URL shown in the console (typically `http://localhost:5000`).

### Building Homer.Kiosk (Mobile)

```bash
cd Homer.Kiosk
dotnet restore
dotnet build

# For Android
dotnet build -f net10.0-android -t:Run

# For iOS (macOS only)
dotnet build -f net10.0-ios -t:Run
```

See the [Homer.Kiosk README](Homer.Kiosk/Homer.Kiosk/ReadMe.md) for detailed setup instructions.

## Architecture

### Homer.NetDaemon Architecture
```
Homer.NetDaemon/
├── Components/
│   ├── Pages/          # Blazor pages (Home, Blinds, etc.)
│   ├── Components/     # Reusable UI components
│   └── Layout/         # Layout components
├── Entities/           # Generated Home Assistant entities
├── Services/           # Application services
└── Options/            # Configuration options
```

### Homer.Kiosk Architecture
```
Homer.Kiosk/
└── Homer.Kiosk/
    ├── Presentation/   # Pages and models (C# markup)
    ├── Services/       # Application services
    ├── Models/         # Data models
    ├── Platforms/      # Platform-specific code
    │   ├── Android/
    │   └── iOS/
    └── Assets/         # Images, fonts, resources
```

## Key Features Comparison

| Feature | Homer.NetDaemon (Web) | Homer.Kiosk (Mobile) |
|---------|----------------------|---------------------|
| Platform | Web browser | Android, iOS |
| UI Framework | Blazor | Uno Platform |
| Markup | Razor (.razor) | C# Markup (.cs) |
| Design System | Bootstrap | Material Design |
| State Management | Blazor State | MVUX |
| Device Controls | ✅ | ✅ (initial) |
| Weather Info | ✅ | ✅ (placeholder) |
| Bus Timings | ✅ | ✅ (placeholder) |
| Responsive | ✅ | ✅ (native) |

## Development

### Homer.NetDaemon Development
1. Install .NET 10.0 SDK
2. Configure Home Assistant credentials in user secrets
3. Run `just codegen` to generate entity classes
4. Build and run with `dotnet run`

### Homer.Kiosk Development
1. Install .NET 10.0 SDK
2. Install required workloads: `dotnet workload restore`
3. Configure Home Assistant connection in appsettings.json
4. Build for your target platform
5. Debug on emulator or physical device

### Code Generation
The `Justfile` includes commands for generating NetDaemon entities:

```bash
just codegen
```

This connects to your Home Assistant instance and generates strongly-typed C# classes for all your devices.

## C# Markup Examples

Homer.Kiosk uses C# Markup for UI definition. Here's a comparison:

**Traditional XAML:**
```xml
<Button Content="Toggle Light" 
        Command="{Binding ToggleLight}" 
        Background="{ThemeResource PrimaryBrush}" />
```

**C# Markup:**
```csharp
new Button()
    .Content("Toggle Light")
    .Command(() => vm.ToggleLight)
    .Background(Theme.Brushes.Primary.Default)
```

## Migration Notes

Homer.Kiosk is a migration of the Homer.NetDaemon Blazor frontend to a native mobile app using Uno Platform. Key changes:

1. **UI Framework**: Blazor → Uno Platform
2. **Markup Style**: Razor → C# Markup
3. **State Management**: Blazor → MVUX
4. **Design System**: Bootstrap → Material Design
5. **Target Platforms**: Web → Android/iOS

The core functionality remains the same:
- Home automation device control
- Real-time state updates
- Weather and transit information
- NetDaemon integration

## Contributing

When contributing to this project:
1. Follow the existing code style
2. Test on multiple platforms for Homer.Kiosk
3. Ensure NetDaemon entities are up-to-date
4. Update documentation as needed

## Technology Stack

- **.NET 10.0** - Latest .NET framework
- **Home Assistant** - Home automation platform
- **NetDaemon** - .NET integration for Home Assistant
- **Blazor** - Web UI (Homer.NetDaemon)
- **Uno Platform** - Cross-platform UI (Homer.Kiosk)
- **C# Markup** - Type-safe UI definition
- **MVUX** - Reactive state management pattern
- **Material Design** - Design system
- **Serilog** - Logging framework
- **Refit** - REST API client

## License

[Add your license information here]

## Resources

- [Uno Platform Documentation](https://platform.uno)
- [NetDaemon Documentation](https://netdaemon.xyz)
- [Home Assistant](https://www.home-assistant.io)
- [Blazor Documentation](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
- [C# Markup Guide](https://aka.platform.uno/csharp-markup)
