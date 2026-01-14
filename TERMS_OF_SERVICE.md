# Terms of Service

**Last Updated**: January 14, 2026

## 1. Acceptance of Terms

By downloading, installing, or using Universal Analog Input ("Application"), you agree to be bound by these Terms of Service. If you do not agree to these terms, do not install or use the Application.

## 2. License and Usage

The Application is provided under the **MIT License**. You have the right to:
- Use the software freely for personal or commercial purposes
- Modify the source code
- Distribute copies
- Include it in your own projects

See [LICENSE](LICENSE) for full details.

## 3. Error Monitoring and Crash Reporting

### 3.1 Sentry Integration (Enabled by Default)

This Application includes **automatic crash reporting via Sentry** to help me improve stability and fix bugs.

**What this means**:
- When the application crashes, a report is automatically sent to my Sentry servers
- The report includes crash details, stack traces, and system information
- This data helps me identify and fix bugs faster

### 3.2 What Data is Sent

**Automatically sent**:
- Application crash information and error messages
- Stack traces (code location and function names)
- Application version and operating system version
- Timestamp of the crash
- Session information (start/end times)

**NOT sent** (protected):
- Personal information (names, emails, etc.)
- Keyboard input or keystroke data
- Game names, file paths, or personal files
- URLs you visit or personal settings

### 3.3 Disabling Sentry

While crash reporting is enabled by default, you can disable it:

**Option 1: Edit .env file**
```bash
# Open %LOCALAPPDATA%\UniversalAnalogInput\.env
# Remove or comment out the DSN lines:
#UI_SENTRY_DSN=...
#NATIVE_SENTRY_DSN=...
```

**Option 2: Delete the .env file**
The application will run without Sentry monitoring.

### 3.4 Your Data Rights

You have the right to:
- Disable Sentry at any time
- Request deletion of your crash data
- Access your crash reports
- See exactly what data was sent

For details, see [PRIVACY_POLICY.md](PRIVACY_POLICY.md).

### 3.5 Data Processing Agreement (DPA)

For organizations requiring formal data processing agreements under GDPR or other regulations:
- Sentry's complete **Data Processing Agreement** is available at: https://sentry.io/legal/dpa/
- This document defines Sentry's obligations as a Data Processor
- Automatically effective for all Sentry customers (no additional signature needed)
- See [PRIVACY_POLICY.md](PRIVACY_POLICY.md) for more information about data processing

## 4. Disclaimer

THE APPLICATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.

IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## 5. Limitation of Liability

To the maximum extent permitted by law:
- I am not responsible for any data loss or damage caused by this software
- I am not responsible for any harm caused by your use of this software
- I am not liable for any indirect, incidental, special, or consequential damages

## 6. Modification of Terms

I reserve the right to modify these terms at any time. Changes will be reflected in the "Last Updated" date. Continued use of the Application constitutes acceptance of modified terms.

## 7. Termination

Your right to use this Application is automatically granted upon acceptance of these terms and continues indefinitely, unless terminated by:
- Violation of these terms
- Uninstalling the application

## 8. Third-Party Services

### 8.1 Sentry
- **Service**: Error Monitoring and Crash Reporting
- **Privacy Policy**: https://sentry.io/privacy/
- **Terms**: https://sentry.io/terms/

### 8.2 Wooting Analog SDK
- **Purpose**: Capture analog keyboard input
- **Privacy**: https://github.com/WootingKb/wooting-analog-sdk

### 8.3 ViGEm
- **Purpose**: Virtual gamepad emulation
- **Privacy**: https://github.com/nefarius/ViGEmBus

## 9. Open Source

This is an open-source project. You can:
- **View the source code**: https://github.com/Ritonton/UniversalAnalogInput
- **Verify what data is sent**: Read the source code yourself
- **Fork and modify**: Create your own version
- **Contribute**: Submit improvements

## 10. Support and Reporting Issues

- **Report Bugs**: https://github.com/Ritonton/UniversalAnalogInput/issues
- **Ask Questions**: Create an issue with the "question" label
- **Contact**: Create an issue with the "question" label

## 11. Governing Law

These terms shall be governed by the laws applicable in France, without regard to its conflict of law provisions.

## 12. Entire Agreement

These Terms of Service, together with the Privacy Policy and License, constitute the entire agreement between you and me regarding the use of the Application.

## 13. Severability

If any provision of these terms is found to be unenforceable, the remaining provisions shall continue in full force and effect.

---

## Quick Summary

| Item | Details |
|------|---------|
| **License** | MIT - Free to use, modify, and distribute |
| **Warranty** | NONE - Provided "as-is" |
| **Crash Reports** | Enabled by default, can be disabled |
| **Data Privacy** | No PII collected by default |
| **User Rights** | Can disable, access, or delete data |
| **Open Source** | Yes - View source code anytime |
| **Modifications** | I can update terms (will notify via "Last Updated") |

---

**By installing this application, you acknowledge that you have read and agree to these Terms of Service.**

For questions, see [PRIVACY_POLICY.md](PRIVACY_POLICY.md) or open a GitHub issue.

**Make sure to also read**: [PRIVACY_POLICY.md](PRIVACY_POLICY.md)
