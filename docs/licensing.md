# Licensing & attribution notes

## tosu — LGPL-3.0

Verified: tosuapp/tosu `LICENSE` = GNU Lesser General Public License v3.

- **This MVP does not bundle or link tosu.** It only *connects to* a separately user-installed tosu
  instance over its local WebSocket. That creates **no LGPL distribution obligation**.
- If a future phase ships/bundles a tosu binary (managed sidecar), LGPL-3.0 requires: a prominent
  notice that tosu (LGPL-3.0) is included and how it is used, inclusion of the GPL + LGPL license
  texts, and a way for users to obtain tosu's source and to replace/relink it. Any *modification* of
  tosu must be published under LGPL.

## MagicChatbox — proprietary source-available (reference only)

Verified: BoiHanny/vrcosc-magicchatbox `License.md` is a custom, source-available proprietary SLA
(not OSI-approved). It is used **only** as a behavioral reference. **No** source, class structure,
UI, assets, text, or branding from MagicChatbox is copied. This project is a clean-room
implementation built from the official VRChat OSC documentation and standard libraries.

## Third-party dependencies

Keep additions to permissive licenses (MIT / BSD / Apache-2.0) and verify each before adding. The
Core library currently uses only the .NET BCL.
