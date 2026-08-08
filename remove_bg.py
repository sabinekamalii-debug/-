import sys
import os
import cv2
from pathlib import Path
from rembg import remove, new_session

os.environ["PYTHONUTF8"] = "1"

# Use birefnet-general model (hyphen, not underscore)
session = new_session("birefnet-general")

base = Path(r"D:\unity\mowang\Assets\new characters\(1) 星豹")

# Select 16 evenly spaced frames from each video (121 frames total)
frame_indices = [int(i * 120 / 16) for i in range(16)]  # 0,7,15,22,30,37,45,52,60,67,75,82,90,97,105,112
print(f"Selected frame indices: {frame_indices}")

for name in ["walk", "battle"]:
    src_dir = base / f"{name}_raw_frames"
    out_dir = base / f"{name}_frames_clean"
    out_dir.mkdir(exist_ok=True)

    # Clear old files
    for old in out_dir.glob("*.png"):
        old.unlink()

    print(f"\nProcessing {name} frames...")
    for i, frame_idx in enumerate(frame_indices):
        src_path = src_dir / f"frame_{frame_idx:04d}.png"
        if not src_path.exists():
            print(f"  [WARN] Frame {frame_idx} not found")
            continue

        # Read image
        with open(src_path, 'rb') as f:
            input_data = f.read()

        # Remove background
        output_data = remove(input_data, session=session)

        # Save with transparent background
        out_path = out_dir / f"frame_{i:04d}.png"
        with open(out_path, 'wb') as f:
            f.write(output_data)

        print(f"  [{i+1}/16] frame_{frame_idx:04d}.png -> {out_path.name} ({len(output_data)} bytes)")

    count = len(list(out_dir.glob('*.png')))
    print(f"  -> Total: {count} clean frames in {out_dir}")

print("\nAll done!")
