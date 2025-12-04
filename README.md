<p align="center">
  <img src="Assets/Square192x192Logo.png" alt="Grex Logo" width="192">
</p>

<h1 align="center">Grex</h1>

<p align="center">
  <strong>The modern, tabbed, Windows grep experience.</strong><br>
  Search native drives, UNC shares, Docker containers, and entire WSL distributions with instant previews, Regex visualizations, and safe bulk replace—all powered by WinUI 3 and .NET 8.
</p>

<p align="center">
  <a href="#quick-start">Quick Start</a> •
  <a href="docs/features.md">Features</a> •
  <a href="docs/usage.md">Usage Guide</a> •
  <a href="docs/architecture.md">Architecture</a> •
  <a href="docs/build-and-test.md">Build & Test</a>
</p>

---

## ✨ Highlights

- **Lightning-fast scanning** – Streamed IO, smart caching, and optional Windows Search acceleration keep huge repos responsive
- **Windows + WSL, one workflow** – Switch between `C:\`, `\\server`, and `\\wsl$\Ubuntu` without touching a terminal
- **Docker container search** – Search directly inside running containers using the Docker API or local mirroring
- **Visual Regex Builder** – Design patterns with presets and live breakdowns before unleashing them on your codebase
- **Safe replace** – Confirmation dialogs, per-file summaries, and culture-aware matching make global edits reliable
- **Multilingual UI** – 100 languages supported with instant interface updates
- **Modern Windows design** – Tabs, command bar, column controls, and a Mica-coated shell feel native on Windows 11

## Quick Start

```powershell
git clone https://github.com/BornSupercharged/Grex.git
cd Grex
dotnet build grex.sln -p:Platform=x64
# After building, run the executable directly:
.\bin\x64\Debug\net8.0-windows10.0.19041.0\Grex.exe
```

> **Requirements:** Windows 10 1809+ (or Windows 11), .NET 8 SDK, and Windows App Runtime 1.8  
> Install the runtime with `winget install Microsoft.WindowsAppRuntime.1.8`

## 🚀 Usage in 60 Seconds

1. **Pick a path** – Browse or paste `C:\repo`, `\\server\logs`, or `\\wsl$\Ubuntu\home\user`
2. **Choose search mode** – Plain text or Regex; Content (per-line) or Files (per-file) results
3. **Refine filters** – Toggle `.gitignore`, hidden/system/bin/link flags, filename patterns, size limits, and Windows Search seeding
4. **Run / replace** – Press Enter to search, fill "Replace with" for bulk edits, and confirm the safety dialog
5. **Inspect results** – Sort, resize, hide columns, or double-click to open files. Tabs keep parallel searches isolated
6. **Target Docker containers** – Enable in Settings, select a container from the dropdown, and search container paths directly

Need the full walkthrough? See the **[Usage Guide](docs/usage.md)**.

## 📚 Documentation

| Document | Description |
|----------|-------------|
| **[Features](docs/features.md)** | Complete feature reference with advanced options |
| **[Usage Guide](docs/usage.md)** | Step-by-step workflows and examples |
| **[Architecture](docs/architecture.md)** | Technical design, components, and search flow |
| **[Build & Test](docs/build-and-test.md)** | Build commands, test suite, and asset generation |
| **[Technical Reference](docs/reference.md)** | Keyboard shortcuts, settings schema, encoding details |
| **[Translations](docs/translations.md)** | Localization system and adding new languages |

## 🛠️ Build & Test

```powershell
# Restore & build
dotnet restore
dotnet build grex.sln -p:Platform=x64

# Run all tests
dotnet test grex.sln -p:Platform=x64
```

For detailed build configurations, CI pipelines, and asset generation, see **[Build & Test Reference](docs/build-and-test.md)**.

## 🤝 Contributing

Issues and pull requests are welcome! Please:

- Keep new UI strings in the `.resw` files so localization stays in sync
- Run `dotnet test grex.sln -p:Platform=x64` before submitting
- Add or update docs under `docs/` when you ship new features

## 📄 License

Released under the **GNU General Public License v3.0**. See [LICENSE](LICENSE) for details.

---

<p align="center">
  Created by <strong>BornSupercharged</strong>
</p>

