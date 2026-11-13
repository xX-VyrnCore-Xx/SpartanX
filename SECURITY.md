# Security Policy

## Supported versions

Only the latest release receives security fixes.

## Reporting a vulnerability

Please **do not open a public issue**. Use GitHub's private reporting:
[Report a vulnerability](https://github.com/xX-VyrnCore-Xx/SpartanX/security/advisories/new).

Include the version, Windows build, impact and steps to reproduce. You'll get an acknowledgement within a few days.

## Scope notes

SpartanX runs unelevated and per-user. Areas of particular interest:

- The in-app updater (downloads ZIPs from GitHub Releases over HTTPS and extracts them over the install folder).
- Global hotkeys and window enumeration.
- Settings parsing (`%LOCALAPPDATA%\SpartanX\settings.json`).
