import os
import sys
from importlib.util import find_spec
from pathlib import Path

os.environ.setdefault("OPENCV_IO_ENABLE_OPENEXR", "1")
os.environ.setdefault("PYTORCH_CUDA_ALLOC_CONF", "expandable_segments:True")
os.environ.setdefault("HF_HUB_DISABLE_XET", "1")
os.environ.setdefault("HF_HUB_ENABLE_HF_TRANSFER", "0")


def _module_available(module_name: str) -> bool:
    return find_spec(module_name) is not None


def _preferred_attention_backend() -> str:
    if _module_available("flash_attn"):
        return "flash_attn"
    if _module_available("flash_attn_interface"):
        return "flash_attn_3"
    return "sdpa"


if "ATTN_BACKEND" not in os.environ:
    os.environ["ATTN_BACKEND"] = _preferred_attention_backend()
if "SPARSE_ATTN_BACKEND" not in os.environ:
    os.environ["SPARSE_ATTN_BACKEND"] = os.environ["ATTN_BACKEND"]

REPO_ROOT = Path(__file__).resolve().parents[1]
TRELLIS_MODEL_REPO_ID = "microsoft/TRELLIS.2-4B"
LEGACY_LOCAL_MODEL_ROOT = REPO_ROOT / "models" / "trellis2"


def resolve_model_reference() -> str:
    configured = (os.environ.get("TRELLIS_MODEL_ROOT") or "").strip()
    if configured:
        configured_path = Path(configured).expanduser()
        if configured_path.exists():
            return str(configured_path)
        return configured
    return TRELLIS_MODEL_REPO_ID


def resolve_local_model_root(*, local_files_only: bool = True) -> Path:
    configured = (os.environ.get("TRELLIS_MODEL_ROOT") or "").strip()
    if configured:
        configured_path = Path(configured).expanduser()
        if configured_path.exists():
            return configured_path

        try:
            from huggingface_hub import snapshot_download

            return Path(snapshot_download(repo_id=configured, local_files_only=local_files_only))
        except Exception:
            if LEGACY_LOCAL_MODEL_ROOT.exists():
                return LEGACY_LOCAL_MODEL_ROOT
            raise

    if LEGACY_LOCAL_MODEL_ROOT.exists():
        return LEGACY_LOCAL_MODEL_ROOT

    from huggingface_hub import snapshot_download

    return Path(snapshot_download(repo_id=TRELLIS_MODEL_REPO_ID, local_files_only=local_files_only))


def resolve_runtime_model_reference() -> str:
    configured = (os.environ.get("TRELLIS_MODEL_ROOT") or "").strip()
    if configured:
        configured_path = Path(configured).expanduser()
        if configured_path.exists():
            return str(configured_path)
        return configured
    if LEGACY_LOCAL_MODEL_ROOT.exists():
        return str(LEGACY_LOCAL_MODEL_ROOT)
    return TRELLIS_MODEL_REPO_ID


def resolve_output_dir() -> Path:
    configured = (os.environ.get("TRELLIS_OUTPUT_DIR") or "").strip()
    if configured:
        return Path(configured).expanduser()
    return REPO_ROOT / "output"


MODEL_REFERENCE = resolve_runtime_model_reference()
DEFAULT_IMAGE = REPO_ROOT / "assets" / "example_image" / "T.png"
OUTPUT_DIR = resolve_output_dir()

sys.path.insert(0, str(REPO_ROOT))


def patch_public_model_ids() -> None:
    import torch
    from PIL import Image

    from trellis2.modules import image_feature_extractor
    from trellis2.pipelines import rembg as rembg_module

    orig_dino_init = image_feature_extractor.DinoV3FeatureExtractor.__init__
    orig_biref_init = rembg_module.BiRefNet.__init__

    def patched_dino_init(self, model_name: str, image_size=512):
        if model_name == "facebook/dinov3-vitl16-pretrain-lvd1689m":
            model_name = "PIA-SPACE-LAB/dinov3-vitl-pretrain-lvd1689m"
        return orig_dino_init(self, model_name, image_size=image_size)

    def patched_dino_extract_features(self, image: torch.Tensor) -> torch.Tensor:
        from torch.nn import functional as F

        image = image.to(self.model.embeddings.patch_embeddings.weight.dtype)
        hidden_states = self.model.embeddings(image, bool_masked_pos=None)
        position_embeddings = self.model.rope_embeddings(image)

        if hasattr(self.model, "layer"):
            layers = self.model.layer
        else:
            layers = self.model.model.layer

        for layer_module in layers:
            hidden_states = layer_module(
                hidden_states,
                position_embeddings=position_embeddings,
            )

        return F.layer_norm(hidden_states, hidden_states.shape[-1:])

    def patched_biref_init(self, model_name: str = "ZhengPeng7/BiRefNet"):
        if model_name == "briaai/RMBG-2.0":
            model_name = "ZhengPeng7/BiRefNet"
        return orig_biref_init(self, model_name=model_name)

    def patched_biref_call(self, image: Image.Image) -> Image.Image:
        from torchvision import transforms

        image_size = image.size
        input_images = self.transform_image(image).unsqueeze(0).to("cuda")
        param = next(self.model.parameters(), None)
        if param is not None:
            input_images = input_images.to(dtype=param.dtype)
        with torch.no_grad():
            preds = self.model(input_images)[-1].sigmoid().cpu()
        pred = preds[0].squeeze()
        pred_pil = transforms.ToPILImage()(pred)
        mask = pred_pil.resize(image_size)
        image.putalpha(mask)
        return image

    image_feature_extractor.DinoV3FeatureExtractor.__init__ = patched_dino_init
    image_feature_extractor.DinoV3FeatureExtractor.extract_features = patched_dino_extract_features
    rembg_module.BiRefNet.__init__ = patched_biref_init
    rembg_module.BiRefNet.__call__ = patched_biref_call
