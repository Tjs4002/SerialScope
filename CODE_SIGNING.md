# Code signing and privacy policy

## Code signing

SerialScope releases are **not code-signed yet**, so Windows SmartScreen may show a warning the first time you run a downloaded copy. Click **More info → Run anyway**, or install with [Scoop](README.md#download), which usually avoids the warning.

Until releases are signed, you can check that a download is genuine:

- Every release is built from this repository's source code by [GitHub Actions](https://github.com/Tjs4002/SerialScope/actions), in public, and runs the automated tests before it is published.
- Each release includes `SHA256SUMS.txt`. Compare it with `Get-FileHash SerialScope.exe` in PowerShell.
- You can [build it yourself](README.md#build-from-source) in a few seconds with the C# compiler that ships with Windows.

The release workflow is already prepared for signing, so signing can be switched on without changing how releases are made. When it is, this page will say who provides the certificate, and only binaries built from this repository will be signed.

### Team roles

| Role | Members |
|---|---|
| Committers and reviewers | [Tjs4002](https://github.com/Tjs4002) |
| Release approvers | [Tjs4002](https://github.com/Tjs4002) |

Contributions from other people come in through pull requests, which are reviewed by a committer before they are merged.

## Privacy policy

SerialScope does not collect, store or send any personal data, serial data or usage statistics.

It connects to one networked system:

- **Update check:** at most twice a day it asks GitHub's public API (`api.github.com`) for the latest SerialScope release version. The request contains no information about you or your devices beyond what any web request carries (such as your IP address, which GitHub sees). It can be turned off under ⚙ **Settings → Check for updates automatically**.

Apart from that, SerialScope will not transfer any information to other networked systems unless you specifically request it, for example by clicking **Report on GitHub** in the crash screen, which opens a pre-filled bug report in your browser that you choose whether to submit.

Session logs and settings stay on your computer.
