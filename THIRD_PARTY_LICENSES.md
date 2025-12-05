# Third-Party Licenses

This document lists all third-party dependencies used in Universal Analog Input and their respective licenses.

## License Compatibility Summary

Universal Analog Input is licensed under the MIT License. All dependencies listed below have licenses that are compatible with MIT:

- **MIT**: Fully compatible (permissive)
- **Apache-2.0**: Fully compatible (permissive)
- **BSD-3-Clause**: Fully compatible (permissive)
- **MPL-2.0**: Compatible (weak copyleft - only requires source for modifications to MPL files)

## Rust Dependencies

### Core Libraries

| Dependency | Version | License | Source |
|------------|---------|---------|--------|
| serde | 1.0 | MIT OR Apache-2.0 | [crates.io](https://crates.io/crates/serde) |
| serde_json | 1.0 | MIT OR Apache-2.0 | [crates.io](https://crates.io/crates/serde_json) |
| thiserror | 2.0.16 | MIT OR Apache-2.0 | [crates.io](https://crates.io/crates/thiserror) |
| uuid | 1.8 | MIT OR Apache-2.0 | [crates.io](https://crates.io/crates/uuid) |
| tokio | 1.42 | MIT | [crates.io](https://crates.io/crates/tokio) |
| log | 0.4 | MIT OR Apache-2.0 | [crates.io](https://crates.io/crates/log) |
| env_logger | 0.11 | MIT OR Apache-2.0 | [crates.io](https://crates.io/crates/env_logger) |
| dirs | 6.0.0 | MIT OR Apache-2.0 | [crates.io](https://crates.io/crates/dirs) |
| once_cell | 1.18 | MIT OR Apache-2.0 | [crates.io](https://crates.io/crates/once_cell) |
| arc-swap | 1.7 | MIT OR Apache-2.0 | [crates.io](https://crates.io/crates/arc-swap) |
| chrono | 0.4 | MIT OR Apache-2.0 | [crates.io](https://crates.io/crates/chrono) |

### Windows-Specific Dependencies

| Dependency | Version | License | Source |
|------------|---------|---------|--------|
| windows | 0.62.0 | MIT OR Apache-2.0 | [Microsoft windows-rs](https://github.com/microsoft/windows-rs) |
| winapi | 0.3 | MIT OR Apache-2.0 | [crates.io](https://crates.io/crates/winapi) |
| winres | 0.1 | MIT | [crates.io](https://crates.io/crates/winres) |

### Hardware Integration

| Dependency | Version | License | Source |
|------------|---------|---------|--------|
| wooting-analog-wrapper | git | MPL-2.0 | [GitHub](https://github.com/WootingKb/wooting-analog-sdk) |
| vigem-client | 0.1 | MIT | [GitHub](https://github.com/CasualX/vigem-client) |

### Build Dependencies

| Dependency | Version | License | Source |
|------------|---------|---------|--------|
| winres | 0.1 | MIT | [crates.io](https://crates.io/crates/winres) |

## C# / .NET Dependencies

| Dependency | Version | License | Source |
|------------|---------|---------|--------|
| Microsoft.WindowsAppSDK | 1.8.251106002 | Microsoft Software License | [NuGet](https://www.nuget.org/packages/Microsoft.WindowsAppSDK) |
| CommunityToolkit.WinUI.Controls.Sizers | 8.2.250402 | MIT | [NuGet](https://www.nuget.org/packages/CommunityToolkit.WinUI.Controls.Sizers) |
| Microsoft.Extensions.DependencyInjection | 9.0.0 | MIT | [NuGet](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection) |
| System.Text.Json | 9.0.0 | MIT | [NuGet](https://www.nuget.org/packages/System.Text.Json) |

## Required Drivers

| Dependency | Version | License | Source |
|------------|---------|---------|--------|
| ViGEmBus Driver | Latest | BSD-3-Clause | [GitHub](https://github.com/nefarius/ViGEmBus) |
| Wooting Analog SDK | Latest | MPL-2.0 | [GitHub](https://github.com/WootingKb/wooting-analog-sdk) |

## License Texts

### MIT License
The MIT License is a permissive free software license. It permits reuse within proprietary software provided all copies include the license terms and copyright notice.

### Apache License 2.0
The Apache License 2.0 is a permissive free software license that also provides an express grant of patent rights from contributors to users.

### BSD-3-Clause License
The BSD 3-Clause License is a permissive free software license that allows redistribution and use with minimal restrictions.

### Mozilla Public License 2.0 (MPL-2.0)
The MPL-2.0 is a weak copyleft license. When you modify MPL-licensed files, those modifications must be released under MPL-2.0. However, you can combine MPL code with code under other licenses (including proprietary) as long as the MPL files remain separate and their modifications are shared.

**Important for Universal Analog Input**: The Wooting Analog SDK is used as a library dependency without modification, so the code is not required to be released under MPL-2.0. If the Wooting SDK files are ever modified directly, only those modifications would need to be shared under MPL-2.0.

### Microsoft Software License
The Microsoft.WindowsAppSDK uses Microsoft's proprietary license terms. This is a runtime dependency distributed by Microsoft via NuGet and does not affect the licensing of Universal Analog Input itself. Users must agree to Microsoft's terms when using Windows applications.

## Attribution Requirements

When distributing Universal Analog Input, the following notices must be included:

1. **Copyright notice** for all MIT and Apache-2.0 licensed dependencies
2. **License text** for BSD-3-Clause (ViGEmBus)
3. **Source availability** for any modifications to MPL-2.0 code (if applicable)
4. **Microsoft license agreement** acceptance for WindowsAppSDK runtime

## Summary

All dependencies have been verified to be compatible with the MIT License used by Universal Analog Input. No copyleft restrictions prevent the distribution of this software under MIT, as:

- Most dependencies use permissive licenses (MIT, Apache-2.0, BSD-3-Clause)
- The MPL-2.0 dependency (Wooting SDK) is used without modification
- Microsoft proprietary licenses apply only to runtime components

For the complete text of each license, please refer to the respective project repositories linked in the tables above.

---

**Last Updated**: 2025-12-05
