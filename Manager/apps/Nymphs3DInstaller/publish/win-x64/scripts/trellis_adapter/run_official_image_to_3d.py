import argparse

import o_voxel
import torch
from PIL import Image

from trellis_official_common import (
    DEFAULT_IMAGE,
    MODEL_REFERENCE,
    OUTPUT_DIR,
    patch_public_model_ids,
)
from trellis2.pipelines.trellis2_image_to_3d import Trellis2ImageTo3DPipeline


class OfficialImageTo3DPipeline(Trellis2ImageTo3DPipeline):
    model_names_to_load = []

    @classmethod
    def configure_for(cls, pipeline_type: str) -> None:
        if pipeline_type == "512":
            cls.model_names_to_load = [
                "sparse_structure_flow_model",
                "sparse_structure_decoder",
                "shape_slat_flow_model_512",
                "shape_slat_decoder",
                "tex_slat_flow_model_512",
                "tex_slat_decoder",
            ]
        elif pipeline_type == "1024":
            cls.model_names_to_load = [
                "sparse_structure_flow_model",
                "sparse_structure_decoder",
                "shape_slat_flow_model_1024",
                "shape_slat_decoder",
                "tex_slat_flow_model_1024",
                "tex_slat_decoder",
            ]
        elif pipeline_type == "1024_cascade":
            cls.model_names_to_load = [
                "sparse_structure_flow_model",
                "sparse_structure_decoder",
                "shape_slat_flow_model_512",
                "shape_slat_flow_model_1024",
                "shape_slat_decoder",
                "tex_slat_flow_model_1024",
                "tex_slat_decoder",
            ]
        elif pipeline_type == "1536_cascade":
            cls.model_names_to_load = [
                "sparse_structure_flow_model",
                "sparse_structure_decoder",
                "shape_slat_flow_model_512",
                "shape_slat_flow_model_1024",
                "shape_slat_decoder",
                "tex_slat_flow_model_1024",
                "tex_slat_decoder",
            ]
        else:
            raise ValueError(f"Unsupported pipeline_type: {pipeline_type}")


def export_glb(mesh, output_path, texture_size: int, decimation_target: int) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    mesh.simplify(16_777_216)
    glb = o_voxel.postprocess.to_glb(
        vertices=mesh.vertices,
        faces=mesh.faces,
        attr_volume=mesh.attrs,
        coords=mesh.coords,
        attr_layout=mesh.layout,
        voxel_size=mesh.voxel_size,
        aabb=[[-0.5, -0.5, -0.5], [0.5, 0.5, 0.5]],
        decimation_target=decimation_target,
        texture_size=texture_size,
        remesh=True,
        remesh_band=1,
        remesh_project=0,
        verbose=True,
    )
    glb.export(str(output_path), extension_webp=True)


def main() -> None:
    parser = argparse.ArgumentParser(description="Run official TRELLIS.2 image-to-3D smoke test")
    parser.add_argument("--image", type=str, default=str(DEFAULT_IMAGE))
    parser.add_argument("--pipeline-type", choices=["512", "1024", "1024_cascade", "1536_cascade"], default="1024_cascade")
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--prefix", default="official_trellis")
    parser.add_argument("--max-tokens", type=int, default=49152)
    parser.add_argument("--texture-size", type=int, default=2048)
    parser.add_argument("--decimation-target", type=int, default=1000000)
    args = parser.parse_args()

    patch_public_model_ids()
    OfficialImageTo3DPipeline.configure_for(args.pipeline_type)

    pipeline = OfficialImageTo3DPipeline.from_pretrained(MODEL_REFERENCE)
    pipeline.cuda()

    image = Image.open(args.image)
    preprocessed = pipeline.preprocess_image(image)
    meshes = pipeline.run(
        image=image,
        num_samples=1,
        seed=args.seed,
        sparse_structure_sampler_params={},
        shape_slat_sampler_params={},
        tex_slat_sampler_params={},
        preprocess_image=True,
        return_latent=False,
        pipeline_type=args.pipeline_type,
        max_num_tokens=args.max_tokens,
    )

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    preprocessed_path = OUTPUT_DIR / f"{args.prefix}_{args.pipeline_type}_preprocessed.png"
    glb_path = OUTPUT_DIR / f"{args.prefix}_{args.pipeline_type}_textured.glb"
    preprocessed.save(preprocessed_path)
    export_glb(meshes[0], glb_path, texture_size=args.texture_size, decimation_target=args.decimation_target)

    print(f"[official-trellis] preprocessed={preprocessed_path}")
    print(f"[official-trellis] glb={glb_path}")
    print(f"[official-trellis] resolution={args.pipeline_type} meshes={len(meshes)}")


if __name__ == "__main__":
    main()
