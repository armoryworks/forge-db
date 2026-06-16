# history/ — apply receipts (OUTPUT — never edit, never replay)

On every `apply`, the harness captures pg-schema-diff's own plan SQL here **before** applying
(docs/DESIGN.md §4.1). These are **receipts, not recipes**: the `schema/` tree + pg-schema-diff are
the only things that determine state. The harness never reads from `history/`; replaying one of these files
is not a supported operation. Do not hand-edit them.
