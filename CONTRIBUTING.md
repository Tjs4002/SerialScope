# Contributing to SerialScope

Thanks for helping out. Bug reports, ideas and pull requests are all welcome.

## Reporting a bug

[Open an issue](https://github.com/Tjs4002/SerialScope/issues/new/choose) and include:

- Your Windows version.
- The device and USB chip (shown next to the port name, e.g. *CP210x* or *CH340*).
- The baud rate.
- What you expected and what happened instead. A saved log (**Save log** button) helps a lot.

## Making changes

1. Fork the repository and create a branch for your change.
2. Edit the files in `src/`.
3. Run `build.bat` and test `bin\SerialScope.exe` with a real device if you can.
4. Open a pull request describing what changed and why.

### Guidelines

- **Keep it dependency-free.** The project builds with the `csc.exe` that ships with Windows, which supports C# 5. Please avoid newer syntax (string interpolation `$"..."`, `?.`, `nameof`, expression-bodied members) and NuGet packages.
- **Keep it small and fast.** SerialScope is meant to open instantly and stay out of the way.
- **Match the existing style:** four-space indents, braces on new lines, short comments only where the code isn't obvious.
- Add a line to [CHANGELOG.md](CHANGELOG.md) under an *Unreleased* heading.

## Releasing (maintainers)

1. Update the version in `src/AppInfo.cs` (both the attributes and `AppInfo.Version`) and in `CHANGELOG.md`.
2. Commit, then tag and push:
   ```bat
   git tag v1.0.1
   git push origin v1.0.1
   ```
3. GitHub Actions builds `SerialScope.exe` and attaches it to a new release automatically, together with `SHA256SUMS.txt` and the Scoop manifest `serialscope.json`.
4. Optional, for winget: once the release is published, generate the manifests and submit them to [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs):
   ```bat
   powershell -ExecutionPolicy Bypass -File tools\make-winget-manifest.ps1 -Version 1.0.1
   wingetcreate submit packaging\winget\1.0.1
   ```
