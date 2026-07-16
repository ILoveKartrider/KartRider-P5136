# Security policy

Report security problems privately to the repository maintainer before opening
a public issue. Include the smallest source-only reproduction possible.

Never attach game clients, PIN/RHO files, packet captures, crash dumps, access
tokens, usernames, profile data or server logs. Redact local paths, IP addresses
and account identifiers.

The connector edits configuration in a user-selected client directory. Review
changes that weaken executable detection, path validation or settings backup as
security-sensitive.

The compatibility server defaults to local use. The legacy protocol does not
provide modern authentication, confidentiality or robust identity binding. Do
not expose it directly to the public Internet; use it only on the local machine
or a trusted isolated network.
