import sys
import os
import cv2
import numpy as np
from pathlib import Path
from rembg import remove, new_session

# Force UTF-8
sys.stdout.reconfigure(encoding='utf-8')

# Paths
base = Path(r"D:\unity\mowang\Assets\new characters\(1) 星豹")

walk_video = base / "Video_20260808_084023.mp4"   # walk video
battle_video = base / "Video_20260808_084026.mp4"  # battle video

walk_dir = base / "走路帧"
battle_dir = base / "战斗帧"

walk_dir.mkdir(exist_ok=True)
battle_dir.mkdir(exist_ok=True)

# Initialize rembg session
print("Initializing rembg session (birefnet-general)...")
session = new_session("birefnet-general")
print("rembg session ready.")

def extract_and_process(video_path, output_dir, step=10, max_frames=15):
    """Extract frames from video at every `step` frames, remove background, save as PNG."""
    cap = cv2.VideoCapture(str(video_path))
    if not cap.isOpened():
        print(f"ERROR: Cannot open {video_path}")
        return []

    total = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    print(f"\nVideo: {video_path.name}, total frames: {total}")

    # Extract frames at every `step` intervals
    selected_indices = list(range(0, total, step))[:max_frames]
    print(f"Selected {len(selected_indices)} frames: {selected_indices}")

    results = []
    for idx in selected_indices:
        cap.set(cv2.CAP_PROP_POS_FRAMES, idx)
        ret, frame = cap.read()
        if not ret:
            print(f"  Frame {idx}: read failed, skipping")
            continue

        # Remove background using rembg
        print(f"  Frame {idx}: removing background...", end=" ", flush=True)
        result_bytes = remove(frame, session=session)
        result_array = np.frombuffer(result_bytes, dtype=np.uint8)
        result_image = cv2.imdecode(result_array, cv2.IMREAD_UNCHANGED)
        # rembg returns RGBA, convert to BGRA for cv2.imwrite
        if result_image.shape[2] == 4:
            result_bgra = cv2.cvtColor(result_image, cv2.COLOR_RGBA2BGRA)
        else:
            result_bgra = result_image

        frame_num = len(results) + 1
        out_path = output_dir / f"frame_{frame_num:04d}.png"
        cv2.imwrite(str(out_path), result_bgra)
        results.append(out_path)
        print(f"saved -> {out_path.name}")

    cap.release()
    return results

# Process walk video
print("=" * 60)
print("Processing WALK video")
walk_frames = extract_and_process(walk_video, walk_dir, step=10, max_frames=12)

# Process battle video
print("=" * 60)
print("Processing BATTLE video")
battle_frames = extract_and_process(battle_video, battle_dir, step=10, max_frames=12)

print("=" * 60)
print(f"WALK frames: {len(walk_frames)} -> {walk_dir}")
print(f"BATTLE frames: {len(battle_frames)} -> {battle_dir}")
print("DONE!")
