"""Live Blender addon entrypoint for Nymphs."""

import importlib


_module_name = f"{__package__}.Nymphs" if __package__ else "Nymphs"
_module = importlib.import_module(_module_name)
_module = importlib.reload(_module)

_public_names = getattr(_module, "__all__", None)
if _public_names is None:
    _public_names = [name for name in dir(_module) if not name.startswith("_")]

globals().update({name: getattr(_module, name) for name in _public_names})
