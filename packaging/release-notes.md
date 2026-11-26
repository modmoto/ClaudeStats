Menu bar indicator for your Claude usage limits: two rings for the 5-hour and the weekly limit.
Uses your Claude Code login; no further setup needed.

## Which package?
- `osx-arm64`: Macs with Apple Silicon (M1 and newer)
- `osx-x64`: Intel Macs

## Install
1. Unzip and drag `ClaudeStats.app` to `/Applications`.
2. The app is not notarized by Apple. Allow it once:
   ```bash
   xattr -dr com.apple.quarantine /Applications/ClaudeStats.app
   ```
3. Make sure you are logged in to Claude Code (`claude`, then `/login`).
4. Open the app. When macOS asks for access to `Claude Code-credentials`, choose **Always Allow**.

The icon lives in the menu bar only. If you don't see it, your menu bar may be full (see the
[README](https://github.com/modmoto/ClaudeStats#icon-not-visible)).

Checksums of the files are in `SHA256SUMS.txt`.
