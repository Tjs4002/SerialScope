# Code signing policy

Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org).

Release builds of `SerialScope.exe` are built from this repository's source code by [GitHub Actions](https://github.com/Tjs4002/SerialScope/actions) and submitted to SignPath for signing. Each release is approved by hand before it is signed. Only binaries built from this repository are signed.

## Team roles

| Role | Members |
|---|---|
| Committers and reviewers | [Tjs4002](https://github.com/Tjs4002) |
| Approvers | [Tjs4002](https://github.com/Tjs4002) |

Contributions from other people come in through pull requests, which are reviewed by a committer before they are merged.

## Privacy policy

SerialScope does not collect, store or send any personal data, serial data or usage statistics.

It connects to one networked system:

- **Update check:** at most twice a day it asks GitHub's public API (`api.github.com`) for the latest SerialScope release version. The request contains no information about you or your devices beyond what any web request carries (such as your IP address, which GitHub sees). It can be turned off under ⚙ **Settings → Check for updates automatically**.

Apart from that, SerialScope will not transfer any information to other networked systems unless you specifically request it, for example by clicking **Report on GitHub** in the crash screen, which opens a pre-filled bug report in your browser that you choose whether to submit.

Session logs and settings stay on your computer.
