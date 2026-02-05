# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-02-05

### Added
- HTTP traffic interception via `DelegatingHandler`
- In-memory ring buffer storage with configurable capacity
- Request list UI with method, URL, status, duration display
- Request detail view with headers and body tabs
- JSON pretty-printing for request/response bodies
- Sensitive header masking (Authorization, API keys, cookies)
- Search and filter by method, status code, or text
- Export to cURL format
- Export to PDF format with professional styling
- SQLite persistent storage option
- Notifications on Android and iOS for quick access
- Dark mode support
- Shell navigation integration (`WiretapPage` route)
- Localized timestamp display

### Supported Platforms
- iOS 15.0+
- Mac Catalyst 15.0+
- Android API 24+ (Android 7.0)
