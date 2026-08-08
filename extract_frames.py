import sys
import os
import cv2
from pathlib import Path

os.environ["PYTHONUTF8"] = "1"

base = Path(r"D:\unity\mowang\Assets\new characters\(1) 星豹")

videos = {
    "walk": base / "Video_20260808_084023.mp4",
    "battle": base / "Video_20260808_084026.mp4",
}

for name, vpath in videos.items():
    if not vpath.exists():
        print(f"[ERROR] {name} video not found: {vpath}")
        continue

    out_dir = base / f"{name}_raw_frames"
    out_dir.mkdir(exist_ok=True)

    cap = cv2.VideoCapture(str(vpath))
    fps = cap.get(cv2.CAP_PROP_FPS)
    total = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    print(f"\n[{name}] {vpath.name}: {w}x{h}, {fps:.1f} fps, {total} frames, {total/fps:.1f}s")

    idx = 0
    while True:
        ret, frame = cap.read()
        if not ret:
            break
        out_path = out_dir / f"frame_{idx:04d}.png"
        # Use imencode + file write to handle Chinese paths
        success, encoded = cv2.imencode('.png', frame)
        if success:
            out_path.write_bytes(encoded.tobytes())
        else:
            print(f"  [WARN] Failed to encode frame {idx}")
        idx += 1

    cap.release()
    # Verify
    png_count = len(list(out_dir.glob('*.png')))
    print(f"  -> Extracted {idx} frames, verified {png_count} PNGs in {out_dir}")

print("\nDone!")
