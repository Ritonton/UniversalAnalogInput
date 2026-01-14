# Privacy Policy

**Last Updated**: January 14, 2026

## 1. Overview

Universal Analog Input ("Application") is a free, open-source software project developed by Henri DELEMAZURE. This Privacy Policy explains how I handle data, particularly in relation to optional error monitoring.

**Important**: Universal Analog Input is distributed under the MIT License and is provided "as-is" without warranty. This is an open-source project, not a commercial product.

## 2. Data Collection

### 2.1 Local Data

The Application stores the following data **locally on your computer**:
- Game profiles and key mappings (JSON files)
- Configuration settings
- Crash logs (in `%LOCALAPPDATA%\UniversalAnalogInput\`)

**I do NOT access or collect this local data.** It remains entirely on your computer.

### 2.2 Optional Crash Reporting (Sentry)

The Application **optionally** includes error monitoring via [Sentry](https://sentry.io) to help identify and fix bugs.

**Sentry Status Depends on How You Got the Application**:

**Built from Source (Open-Source Development)**:
- Sentry is **DISABLED by default** - no crash data is sent
- To enable monitoring, you must explicitly set environment variables:
  ```bash
  UI_SENTRY_DSN=https://your-key@sentry.io/project-id
  NATIVE_SENTRY_DSN=https://your-key@sentry.io/project-id
  ```

**Official Release (Distributed Binary)**:
- Sentry is **ENABLED by default** - crash reports are sent automatically when crashes occur
- You can **disable Sentry at any time** by editing or deleting the `.env` file:
  - Edit `%LOCALAPPDATA%\UniversalAnalogInput\.env` and comment out the DSN lines
  - Or delete the `.env` file entirely
- If no DSN variables are set, **no crash data is sent anywhere**

### 2.3 What Sentry Collects (When Enabled)

When enabled, Sentry collects:

**Collected**:
- Application crash details and stack traces
- Application version and release information
- Operating system version
- Timestamps of crashes
- Session information (start/end times)

**NOT Collected** (by default):
- Personal information (names, emails, IP addresses)
- User input or keystroke data
- Keyboard mappings or custom configurations
- Game names or file paths

Configuration ensures `send_default_pii: false`, preventing automatic collection of personally identifiable information.

## 3. Sentry Data Handling

### 3.1 Data Transmission

When Sentry is enabled:
- Crash reports are sent to Sentry Inc.'s servers
- Data is transmitted over HTTPS (encrypted)
- Sentry acts as a "Data Processor" under the terms of their [Privacy Policy](https://sentry.io/privacy/)

### 3.2 Data Location

- **Sentry EU**: Data stored in the European Union (depending on your plan)

### 3.3 Data Retention

Sentry retains crash data for:
- **Default**: 30 days

After the retention period, data is automatically deleted.

### 3.4 Your Rights

You can:
- **Disable Sentry** (Official Release): Edit or delete the `.env` file in `%LOCALAPPDATA%\UniversalAnalogInput\`
- **Disable Sentry** (Built from Source): Simply don't set the DSN environment variables
- **Delete Crash Data**: Request deletion via your Sentry project dashboard
- **Export Your Data**: Download crash reports from Sentry's web interface
- **Uninstall Completely**: Remove the application entirely - no monitoring occurs

## 4. Third-Party Services

### 4.1 Sentry

**Service**: Error Monitoring and Crash Reporting
**URL**: https://sentry.io
**Privacy Policy**: https://sentry.io/privacy/
**Terms of Service**: https://sentry.io/terms/
**Data Controller**: Sentry Inc.

When Sentry is enabled, you implicitly agree to Sentry's terms and privacy policy. Sentry is a reputable, GDPR-compliant service used by thousands of open-source projects.

### 4.2 Wooting Analog SDK

The Application integrates with the Wooting Analog SDK to capture keyboard input. The SDK **operates locally** and does not transmit data to external servers.

**SDK Privacy**: https://github.com/WootingKb/wooting-analog-sdk

### 4.3 ViGEm Virtual Gamepad

The Application uses ViGEm for virtual gamepad emulation. ViGEm **operates locally** and does not transmit data.

**ViGEm Privacy**: https://github.com/nefarius/ViGEmBus

## 5. Legal Basis for Processing

Under GDPR, my legal basis for processing crash data (when Sentry is enabled) is:

**Legitimate Interests** (Article 6(1)(f) GDPR)
- Improving application stability and reliability
- Identifying and fixing bugs
- Monitoring release health

## 6. User Rights (GDPR)

If you are in the European Union, you have the right to:

1. **Access** your personal data (request via Sentry dashboard)
2. **Rectification** of inaccurate data
3. **Erasure** ("right to be forgotten") - delete your crash reports
4. **Restrict** processing
5. **Object** to processing
6. **Data Portability** - export your data

**To exercise these rights**:
- Contact Sentry directly at https://sentry.io/privacy/
- Or disable Sentry completely by not setting DSN variables

## 7. Children's Privacy

This Application is not intended for children under 13 years old. I do not knowingly collect data from minors. If you believe a child has used this application, please disable Sentry monitoring.

## 8. California Privacy Rights (CCPA)

California residents have the right to:
- Know what data is collected
- Delete personal data
- Opt-out of "sale" of personal data (I don't sell data)

Since Sentry is optional and disabled by default, you have full control over whether any data is processed.

## 9. Data Security

The Application:
- Does not store sensitive data without encryption
- Transmits data to Sentry via HTTPS only
- Does not transmit data anywhere if Sentry is disabled

**Important**: This is open-source software. I recommend reviewing the source code at https://github.com/Ritonton/UniversalAnalogInput to verify security practices.

## 10. Changes to This Policy

I may update this Privacy Policy occasionally. Any changes will be reflected in the "Last Updated" date at the top of this document.

## 11. Contact

For questions about this Privacy Policy or data handling:
- **Open an Issue**: https://github.com/Ritonton/UniversalAnalogInput/issues

## 12. Summary

| Question | Answer |
|----------|--------|
| Is Sentry enabled by default? | **Built from Source**: No / **Official Release**: Yes |
| Can I disable Sentry in Official Release? | **Yes** - Edit or delete `%LOCALAPPDATA%\UniversalAnalogInput\.env` |
| What data does Sentry collect? | Crash reports, stack traces, version info, OS version |
| Is personal data collected? | **No** - `send_default_pii: false` (default configuration) |
| Can I delete my crash data? | **Yes** - Delete via Sentry dashboard or request erasure |
| Where is my data stored? | Sentry servers EU |
| How long is data retained? | 30 days (default) |
| Is my keyboard input logged? | **No** - Only application crashes are reported |

## 13. Data Processing Agreement (DPA)

For organizations and users requiring formal data processing agreements under GDPR or other data protection regulations:

**Sentry's Data Processing Agreement** is available at:
- **Full DPA**: https://sentry.io/legal/dpa/
- **Sentry Privacy Policy**: https://sentry.io/privacy/
- **Sentry Terms of Service**: https://sentry.io/terms/

### What This Means

When Sentry is enabled (in Official Release builds):
- Sentry acts as a **Data Processor** under GDPR Article 28
- The DPA automatically applies to all Sentry customers
- **No additional signature or agreement is required** - using the service implies acceptance
- The DPA defines how Sentry handles personal data on your behalf
- You retain full control over data deletion and retention periods

### For EU Users (GDPR)

Your **legal basis** for processing under Sentry is:
- **Legitimate Interests** (GDPR Article 6(1)(f))
  - Improving application stability and reliability
  - Identifying and fixing bugs
  - Monitoring release health and user adoption

All data processing complies with:
- GDPR principles (lawfulness, fairness, transparency, data minimization)
- Sentry's standard contractual clauses for data transfers
- Your rights under GDPR (access, rectification, erasure, portability)

---

**This Application is open-source and provided "as-is" under the MIT License.**

For more information, see:
- **Code**: https://github.com/Ritonton/UniversalAnalogInput
- **License**: [LICENSE](LICENSE)
- **Dependencies**: [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md)
