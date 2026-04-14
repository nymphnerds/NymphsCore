# Nymphs3D Blender Addon User Guide

This guide is a placeholder for the main addon user guide.

It will be expanded to cover:

- installing the addon in Blender
- connecting the addon to the `Nymphs3D2` WSL backend
- backend startup behavior
- model download behavior when models were not prefetched
- choosing backend modes
- troubleshooting common connection and startup problems

## Current Short Notes

- The recommended backend target is the dedicated `NymphsCore` WSL distro created by the manager.
- If models were not prefetched during install, the manager or addon can trigger large model downloads later.
- Those model downloads can take a long time on normal home internet connections.

## Planned Sections

1. Install the addon in Blender
2. Point the addon at the correct backend distro
3. Start the backend from the addon or manager-supported runtime tools
4. Understand model downloads
5. Use the texture workflow
6. Fix common startup problems
