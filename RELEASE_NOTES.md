# Quiver v2.3.13

Fixes Windows installs for GitHub releases that ship `.tar.gz` assets (for example Project Picori / Minish Cap decomp).

## Install / extraction

- **Windows tar.gz installs** — Game installs now extract `.tar.gz` with the system `tar` tool (same approach as Linux/macOS and the CLI updater), instead of the broken custom Windows tar reader that could fail with “Could not find any recognizable digits.”
- **Managed fallback** — If system `tar` cannot be started, a corrected built-in extractor is used (no double-read of block padding; size fields trim spaces).
