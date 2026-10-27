# Security Policy

## Supported Versions

This is an early-stage repository. Security fixes are handled on the active code
line only.

| Version | Supported |
| --- | --- |
| `main` (latest) | ✅ |
| Older commits/releases | ❌ |

## Reporting a Vulnerability

If you discover a security issue, please report it privately to the repository
maintainers through GitHub security reporting mechanisms (for example, GitHub
Private Vulnerability Reporting, if enabled).

If private reporting is not available in the repository UI, open a minimal
public issue **without exploit details** and request a secure coordination
channel with maintainers.

Do **not** publish proof-of-concept exploit details, attack steps, or sensitive
payloads before maintainers have had a chance to investigate and coordinate a
fix.

## What to Include in a Report

Please include:

- A clear description of the vulnerability and impacted component(s)
- Reproduction steps (safe/minimal)
- Potential impact
- Affected commit/branch/version, if known
- Any suggested mitigations

Do not include real secrets, personal data, or production-only credentials in
the report.

## Disclosure and Coordination

- Maintainers will triage reports and coordinate remediation.
- Public disclosure should be coordinated after a fix or mitigation is available.
- There is currently no dedicated private security email address published for
  this repository.

## If Secrets Were Exposed

If a vulnerability report indicates leaked credentials, tokens, or keys:

1. Rotate affected credentials immediately.
2. Revoke/disable exposed tokens or keys.
3. Replace compromised secrets in deployment environments.
4. Review logs and dependent systems for misuse.
5. Coordinate follow-up remediation (including history cleanup if needed).
