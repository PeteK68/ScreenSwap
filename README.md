# ScreenSwap

**Instantly move entire workspaces between monitors using a single shortcut.**

ScreenSwap lets you bring a full set of windows from one display to another—preserving their layout—so you can stay focused on your primary screen.

Created by Peter Khouri. ScreenSwap began as an experimental window-management project in 2015/2016 and was rebuilt into a working v1 in 2026.

---

## ✨ Why ScreenSwap exists

If you use multiple monitors, you’ve probably done this:

* You organize apps on a side screen
* Then you want to actually *work* on them
* So you drag, resize, and rearrange everything again

ScreenSwap removes that friction.

With one shortcut, your entire workspace moves to your main monitor—exactly how you arranged it.

---

## 🚀 What it does

* 🔁 **Move entire screen layouts**
  Bring all windows from a secondary display to your main display in one action

* 🧠 **Preserve window positions**
  Layouts are remembered and restored when moved

* 🎯 **Focus on a primary monitor**
  Always work on your preferred screen without losing organization

* 🔀 **App Swap (single window focus)**
  Move a specific window into focus while maintaining layout relationships

* ⌨️ **Global shortcuts**
  Trigger everything instantly from anywhere

---

## 🖥️ How it works

ScreenSwap runs as a lightweight background agent:

* Listens for your configured global shortcuts
* Tracks window positions on each display
* Moves and restores layouts between monitors

The app is split into two parts:

* **ScreenSwap.Agent** — background tray process, global hotkey listener, and swap runtime
* **ScreenSwap** — Windows App SDK settings UI

Everything runs locally on your machine.

---

## 🔐 Privacy & safety

ScreenSwap needs access to keyboard input to detect your shortcut—but:

* Keystrokes are **never recorded**
* No data is collected or transmitted
* Everything runs locally

You can verify this yourself—the full source is available.

See: `PRIVACY.md`

---

## ⚙️ Example workflow

1. Arrange apps on a secondary monitor
2. Press your ScreenSwap shortcut
3. Instantly bring that layout to your main screen
4. Work comfortably
5. Swap again whenever needed

---

## 🧰 Configuration

* Select your **main display**
* Configure shortcuts for:

  * Move all windows
  * Move top window only
* Adjust animation duration
* Choose theme

---

## 🪟 Get ScreenSwap

Available on the Microsoft Store.

*(Coming soon)*

---

## 🧱 Architecture (for developers)

* `ScreenSwap.Agent` — tray process, hotkey registration, IPC server, swap runtime
* `ScreenSwap` — settings UI
* `ScreenSwap.Configuration` — shared settings + IPC contracts
* `ScreenSwap.Core` — swap orchestration logic
* `ScreenSwap.Windows` — Windows-specific APIs (hotkeys, monitors, window management)

The settings UI communicates with the agent over a local named pipe.
Settings are stored locally and applied dynamically.

---

## ⌨️ Shortcut behavior

* Global shortcuts work even when other apps are focused
* Shortcut capture is isolated to the settings dialog
* Conflicts and invalid combinations are detected before saving

---

## 🛠️ Development

### Requirements

* Windows 10 (19041+)
* .NET 10 SDK
* Visual Studio or equivalent

### Build

```powershell
dotnet restore .\ScreenSwap.slnx
dotnet build .\ScreenSwap.slnx -c Debug
```

### Run

```powershell
dotnet run --project .\ScreenSwap.Agent\ScreenSwap.Agent.csproj -c Debug
```

---

## 🧠 Design philosophy

> Your workspace should move with you—not the other way around.

---

## 🧾 License

MIT License

---

## ™️ Trademark

“ScreenSwap” is the name of this project. The name and branding may not be used
for derived works without permission.

---

## 🙌 Attribution

ScreenSwap was originally created by Peter Khouri (2026).
