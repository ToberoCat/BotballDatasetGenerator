from __future__ import annotations

import argparse
import json
import random
import shutil
from pathlib import Path
from typing import Dict, List, Tuple
import uuid
import PIL.Image

IMAGE_EXTS = {".png", ".jpg", ".jpeg"}

try:
    from tqdm import tqdm  # type: ignore
except ImportError:  # pragma: no cover
    def tqdm(x, *args, **kwargs):  # noqa: D401
        return x

def unique_stem(path: Path, root: Path) -> str:
    rel = path.relative_to(root).as_posix()
    return uuid.uuid5(uuid.NAMESPACE_URL, rel).hex 

def guess_json_for_image(img_path: Path) -> Path | None:
    stem = img_path.stem 
    candidates = []
    prev_str = ""
    for el in stem.split('.'):
        prev_str += el
        candidates.append(img_path.with_name(prev_str + ".frame_data.json"))
        prev_str += "."
    return next((p for p in candidates if p.exists()), None)


def find_pairs(root: Path) -> List[Tuple[Path, Path]]:
    pairs: List[Tuple[Path, Path]] = []
    
    for img_path in root.rglob("*"):

        if img_path.suffix.lower() not in IMAGE_EXTS:
            continue
        json_path = guess_json_for_image(img_path)
        if json_path:
            pairs.append((img_path, json_path))
    if not pairs:
        raise RuntimeError(f"No image/JSON pairs found in {root}. Unsupported folder layout?")
    return sorted(pairs)


def load_class_map(root: Path) -> Dict[int, str]:
    ann_file = root / "annotation_definitions.json"
    if not ann_file.exists():
        raise FileNotFoundError("annotation_definitions.json missing at dataset root!")
    with ann_file.open() as f:
        data = json.load(f)
    for ann in data.get("annotationDefinitions", []):
        if ann.get("id") == "bounding box" or ann.get("format") == "bbox":
            return {lab["label_id"]: lab["label_name"] for lab in ann.get("spec", {})}
    raise RuntimeError("Bounding‑box definition not found in annotation_definitions.json")


def convert_bbox(x: float, y: float, w: float, h: float, img_w: int, img_h: int) -> Tuple[float, float, float, float]:
    cx = (x + w / 2) / img_w
    cy = (y + h / 2) / img_h
    return cx, cy, w / img_w, h / img_h


def _xywh_from_box(box: dict) -> tuple[float, float, float, float]:
    if {"x", "y", "width", "height"} <= box.keys():
        return box["x"], box["y"], box["width"], box["height"]
    if {"origin", "dimension"} <= box.keys():
        x, y = box["origin"]   
        w, h = box["dimension"]
        return x, y, w, h
    raise KeyError("No bbox fields I recognise.")

def yolo_lines_for_frame(json_path: Path,
                         class_map: dict[int, str],
                         img_w: int, img_h: int) -> list[str]:
    with json_path.open() as f:
        data = json.load(f)

    ann_blocks = (
        data.get("annotations") or
        (data.get("captures", [{}])[0].get("annotations")) or
        []
    )

    lines: list[str] = []
    for ann in ann_blocks:
        if (ann.get("id") or ann.get("name")) != "bounding box":
            continue
        for box in ann.get("values") or ann.get("data", []):
            lid = int(box.get("label_id") or box.get("labelId"))
            if lid not in class_map:
                continue
            x, y, w, h = _xywh_from_box(box)
            cx = (x + w / 2) / img_w
            cy = (y + h / 2) / img_h
            lines.append(f"{lid} {cx:.6f} {cy:.6f} {w/img_w:.6f} {h/img_h:.6f}\n")
    return lines

def convert_dataset(root: Path, out: Path, train: float, val: float, test: float, seed: int) -> None:
    pairs = find_pairs(root)
    class_map = load_class_map(root)

    for split in ("train", "val", "test"):
        (out / "images" / split).mkdir(parents=True, exist_ok=True)
        (out / "labels" / split).mkdir(parents=True, exist_ok=True)

    random.seed(seed)
    random.shuffle(pairs)
    n = len(pairs)
    n_train, n_val = int(n * train), int(n * val)
    splits = {
        "train": pairs[:n_train],
        "val": pairs[n_train : n_train + n_val],
        "test": pairs[n_train + n_val :],
    }

    for split, items in splits.items():
        for img_path, json_path in tqdm(items, desc=f"{split:>5}"):
            img_w, img_h = PIL.Image.open(img_path).size
            lines = yolo_lines_for_frame(json_path, class_map, img_w, img_h)
            if not lines:
                continue

            stem = unique_stem(img_path, root)
            dst_img = out / "images" / split / f"{stem}{img_path.suffix}"
            dst_lbl = out / "labels" / split / f"{stem}.txt"

            shutil.copy2(img_path, dst_img)
            dst_lbl.write_text("".join(lines))
            
    names = [name for _id, name in sorted(class_map.items())]
    (out / "dataset.yaml").write_text(
        f"path: {out}\ntrain: images/train\nval: images/val\ntest: images/test\n"
        f"nc: {len(names)}\nnames: {names}\n"
    )
    print(f"\n✅ Converted {n} images → {out}")

def parse() -> argparse.Namespace:  # noqa: D401
    p = argparse.ArgumentParser(description="SOLO → YOLOv11 converter")
    p.add_argument("--input", "-i", type=Path, required=True, help="Path to SOLO dataset root")
    p.add_argument("--output", "-o", type=Path, required=True, help="Destination folder for YOLO dataset")
    p.add_argument("--train", type=float, default=0.8)
    p.add_argument("--val", type=float, default=0.1)
    p.add_argument("--test", type=float, default=0.1)
    p.add_argument("--seed", type=int, default=42)
    args = p.parse_args()
    if abs(args.train + args.val + args.test - 1) > 1e-6:
        p.error("train+val+test must sum to 1.0")
    return args


def main() -> None:
    a = parse()
    convert_dataset(a.input, a.output, a.train, a.val, a.test, a.seed)


if __name__ == "__main__":
    main()

