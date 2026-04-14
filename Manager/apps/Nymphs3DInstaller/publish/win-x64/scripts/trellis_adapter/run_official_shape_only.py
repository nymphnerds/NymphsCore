import argparse
from pathlib import Path

import torch
import trimesh
from PIL import Image

from trellis_official_common import (
    DEFAULT_IMAGE,
    MODEL_REFERENCE,
    OUTPUT_DIR,
    patch_public_model_ids,
)
from trellis2.pipelines.trellis2_image_to_3d import Trellis2ImageTo3DPipeline


class OfficialShapeOnlyPipeline(Trellis2ImageTo3DPipeline):
    model_names_to_load = []

    @classmethod
    def configure_for(cls, pipeline_type: str) -> None:
        if pipeline_type == "512":
            cls.model_names_to_load = [
                "sparse_structure_flow_model",
                "sparse_structure_decoder",
                "shape_slat_flow_model_512",
                "shape_slat_decoder",
            ]
        elif pipeline_type == "1024":
            cls.model_names_to_load = [
                "sparse_structure_flow_model",
                "sparse_structure_decoder",
                "shape_slat_flow_model_1024",
                "shape_slat_decoder",
            ]
        elif pipeline_type == "1024_cascade":
            cls.model_names_to_load = [
                "sparse_structure_flow_model",
                "sparse_structure_decoder",
                "shape_slat_flow_model_512",
                "shape_slat_flow_model_1024",
                "shape_slat_decoder",
            ]
        elif pipeline_type == "1536_cascade":
            cls.model_names_to_load = [
                "sparse_structure_flow_model",
                "sparse_structure_decoder",
                "shape_slat_flow_model_512",
                "shape_slat_flow_model_1024",
                "shape_slat_decoder",
            ]
        else:
            raise ValueError(f"Unsupported pipeline_type: {pipeline_type}")

    @torch.no_grad()
    def run_shape_only(
        self,
        image: Image.Image,
        pipeline_type: str = "512",
        seed: int = 42,
        sparse_structure_sampler_params: dict | None = None,
        shape_slat_sampler_params: dict | None = None,
        max_num_tokens: int = 49152,
        preprocess_image: bool = True,
    ):
        sparse_structure_sampler_params = sparse_structure_sampler_params or {}
        shape_slat_sampler_params = shape_slat_sampler_params or {}

        if preprocess_image:
            image = self.preprocess_image(image)

        torch.manual_seed(seed)
        cond_512 = self.get_cond([image], 512)

        if pipeline_type == "512":
            coords = self.sample_sparse_structure(cond_512, 32, 1, sparse_structure_sampler_params)
            shape_slat = self.sample_shape_slat(
                cond_512,
                self.models["shape_slat_flow_model_512"],
                coords,
                shape_slat_sampler_params,
            )
            resolution = 512
        elif pipeline_type == "1024":
            cond_1024 = self.get_cond([image], 1024)
            coords = self.sample_sparse_structure(cond_512, 64, 1, sparse_structure_sampler_params)
            shape_slat = self.sample_shape_slat(
                cond_1024,
                self.models["shape_slat_flow_model_1024"],
                coords,
                shape_slat_sampler_params,
            )
            resolution = 1024
        elif pipeline_type == "1024_cascade":
            cond_1024 = self.get_cond([image], 1024)
            coords = self.sample_sparse_structure(cond_512, 32, 1, sparse_structure_sampler_params)
            shape_slat, resolution = self.sample_shape_slat_cascade(
                cond_512,
                cond_1024,
                self.models["shape_slat_flow_model_512"],
                self.models["shape_slat_flow_model_1024"],
                512,
                1024,
                coords,
                shape_slat_sampler_params,
                max_num_tokens,
            )
        elif pipeline_type == "1536_cascade":
            cond_1024 = self.get_cond([image], 1024)
            coords = self.sample_sparse_structure(cond_512, 32, 1, sparse_structure_sampler_params)
            shape_slat, resolution = self.sample_shape_slat_cascade(
                cond_512,
                cond_1024,
                self.models["shape_slat_flow_model_512"],
                self.models["shape_slat_flow_model_1024"],
                512,
                1536,
                coords,
                shape_slat_sampler_params,
                max_num_tokens,
            )
        else:
            raise ValueError(f"Unsupported pipeline_type: {pipeline_type}")

        meshes, subs = self.decode_shape_slat(shape_slat, resolution)
        return image, meshes, subs, resolution


def export_mesh(mesh, output_path: Path) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    tri = trimesh.Trimesh(
        vertices=mesh.vertices.detach().cpu().numpy(),
        faces=mesh.faces.detach().cpu().numpy(),
        process=False,
    )
    tri.export(str(output_path), file_type=output_path.suffix.lstrip("."))


def main() -> None:
    parser = argparse.ArgumentParser(description="Run official TRELLIS.2 shape-only smoke test")
    parser.add_argument("--image", type=Path, default=DEFAULT_IMAGE)
    parser.add_argument("--pipeline-type", choices=["512", "1024", "1024_cascade", "1536_cascade"], default="1024_cascade")
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--prefix", default="official_trellis")
    parser.add_argument("--max-tokens", type=int, default=49152)
    args = parser.parse_args()

    patch_public_model_ids()
    OfficialShapeOnlyPipeline.configure_for(args.pipeline_type)

    pipeline = OfficialShapeOnlyPipeline.from_pretrained(MODEL_REFERENCE)
    pipeline.cuda()

    image = Image.open(args.image)
    preprocessed, meshes, _subs, resolution = pipeline.run_shape_only(
        image=image,
        pipeline_type=args.pipeline_type,
        seed=args.seed,
        sparse_structure_sampler_params={},
        shape_slat_sampler_params={},
        max_num_tokens=args.max_tokens,
        preprocess_image=True,
    )

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    preprocessed_path = OUTPUT_DIR / f"{args.prefix}_{args.pipeline_type}_preprocessed.png"
    mesh_path = OUTPUT_DIR / f"{args.prefix}_{args.pipeline_type}_shape.glb"
    preprocessed.save(preprocessed_path)
    export_mesh(meshes[0], mesh_path)

    print(f"[official-trellis] preprocessed={preprocessed_path}")
    print(f"[official-trellis] mesh={mesh_path}")
    print(f"[official-trellis] resolution={resolution} meshes={len(meshes)}")


if __name__ == "__main__":
    main()
