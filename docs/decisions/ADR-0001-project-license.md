# ADR-0001: License Fadrio under GPL-3.0-or-later

- Status: Accepted
- Date: 2026-09-04

## Context

Fadrio is intended to be a community-developed Linux desktop utility. The technical specification recommends GPL-3.0-or-later while leaving the final choice open before accepting external contributions. The repository now needs an explicit contribution and distribution license.

## Decision

License original Fadrio source and bundled original assets under GNU General Public License version 3 or, at the recipient's option, any later version. Contributions are accepted under the same terms. Third-party components and runtime-resolved assets retain their own licenses.

## Consequences

Distributions and derivative works must comply with GPL source and notice obligations. Package metadata uses the SPDX expression `GPL-3.0-or-later`, the complete license text is stored at the repository root, and third-party dependencies are documented separately.
