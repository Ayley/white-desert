# White Desert 🏜️

**White Desert** is a high-performance archive explorer and extractor designed for Black Desert Online (BDO) `.paz` files. It combines a modern Windows 11 UI experience with a powerful, low-level Rust engine to handle massive game archives with ease.

---

## ✨ Features

* **Blazing Fast Indexing**: Powered by **Black Ghost** (custom Rust backend). It utilizes high-performance memory mapping to provide near-instant access to thousands of archive entries directly via pointer arithmetic and unsafe memory access.
* **Modern UI**: Built with **Avalonia UI** and **FluentAvalonia**, offering a native Windows 11 Look & Feel including Dark Mode support.
* **Deep Inspection Tools**:
    * **Hex Editor**: Real-time binary inspection of file contents via a custom Hex-Control.
    * **Text & Script Preview**: Syntax highlighting for Lua, XML, and JSON using AvaloniaEdit.
    * **Image Viewer**: High-performance preview for game textures and UI assets.
* **Smart Search**: Parallelized search logic to find files across the entire archive structure in milliseconds.
* **Safe Extraction**: Batch extraction with real-time progress tracking and automated temporary file management.

---

## 🚀 Getting Started

1. **Download**: Grab the latest version from the [Releases](#) page.
2. **First Start**: Launch `White Desert.exe`.
3. **Setup Path**: 
   * Use the **auto-search** function to let the app find your game automatically.
   * **OR** use the directory picker to select your **Black Desert Online installation folder** manually.
4. **Explore**: Once the path is set, the tool will initialize the index, and you can start browsing, searching, and extracting files.

---

## ⚠️ Known Issues (Work in Progress)

* **Hex Editor Search**: The search functionality within the Hex Editor is currently not operational.
* **File Info Accuracy**: Metadata displayed in the File Info panel may be incorrect in certain edge cases.
* **Image View Persistence**: Deselecting an image does not automatically switch the view tab, leaving the Image View active.

---

## 🗺️ Roadmap (Future Features)

The following features are planned for future updates (optional & non-binding):
* **Advanced Extraction**: Option to extract Lua scripts directly in their decompiled state.
* **Image Conversion**: Export `.dds` files directly as `.png` during extraction.
* **Media Support**: Integrated WebM video playback.
* **Audio Engine**: Ability to play back game sounds and music directly within the app.
* **Localization Tools**: Improved display and handling of language/string files.

---

## ⚖️ License

Distributed under the **MIT License**. See `LICENSE` for more information.

---

## 🤝 Acknowledgments
* [AvaloniaUI](https://github.com/AvaloniaUI/Avalonia) for the UI framework.
* [FluentAvalonia](https://github.com/amwx/FluentAvalonia) for the beautiful WinUI 3 styles.
* [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) for the powerful text editing capabilities.
