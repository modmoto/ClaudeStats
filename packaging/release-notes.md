Menu bar indicator for your Claude usage limits: two rings for the 5-hour and the weekly limit.

## Which package?
- `osx-arm64`: Macs with Apple Silicon (M1 and newer)
- `osx-x64`: Intel Macs

## Install
1. Unzip and drag `ClaudeStats.app` to `/Applications`.
2. The app is not notarized by Apple. Allow it once:
   ```bash
   xattr -dr com.apple.quarantine /Applications/ClaudeStats.app
   ```
3. Set up the status line (see the [README](https://github.com/modmoto/ClaudeStats#setup)), start Claude Code, open the app.

Alternatively, build it yourself with `scripts/package-macos.sh`; then step 2 is not needed.

Checksums of the files are in `SHA256SUMS.txt`.
