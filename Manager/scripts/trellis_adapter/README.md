# TRELLIS.2 Adapter Scripts

This folder is the source of truth for the Nymphs TRELLIS.2 adapter scripts.

The installer copies these files into the local TRELLIS.2 runtime checkout at:

```text
~/TRELLIS.2/scripts/
```

Those installed runtime copies are intentionally not tracked by the upstream
`microsoft/TRELLIS.2` git repository. Edit and commit adapter changes here in
`NymphsCore`, then reinstall or copy them into the runtime checkout for testing.

Tracked adapter files:

```text
api_server_trellis.py
api_server_trellis_gguf.py
run_official_image_to_3d.py
run_official_shape_only.py
trellis_gguf_common.py
trellis_official_common.py
```
