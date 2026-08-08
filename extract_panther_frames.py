import sys
import os
import cv2
from rembg import remove, new_session
from pathlib import Path

# UTF-8 setup for Windows
sys.stdout.reconfigure(encoding='utf-8')

# Paths
base = Path(r"D:\unity\mowang\Assets\new characters\(1) 星豹")

videos = {
    "走路帧": (base / "Video_20260808_084023.mp4", base / "走路帧"),
    "战斗帧": (base / "Video_20260808_084026.mp4", base / "战斗帧"),
}

# Init rembg session with birefnet-general model (best for general subjects)
print("Initializing rembg with birefnet-general model...")
session = new_session("birefnet-general")
print("rembg session ready.")

for name, (video_path, out_dir) in videos.items():
    print(f"\n=== Processing {name} ===")
    print(f"Video: {video_path}")

    # Clean old frames
    if out_dir.exists():
        for f in out_dir.glob("frame_*.png"):
            f.unlink()
        for f in out_dir.glob("*.anim"):
            f.unlink()
        for f in out_dir.glob("*.meta"):
            f.unlink()
    else:
        out_dir.mkdir(parents=True, exist_ok=True)

    cap = cv2.VideoCapture(str(video_path))
    fps = cap.get(cv2.CAP_PROP_FPS)
    total = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    print(f"FPS: {fps}, Total frames: {total}")

    idx = 0
    saved = 0
    while True:
        ret, frame = cap.read()
        if not ret:
            break
        idx += 1

        # Remove background with rembg
        result = remove(frame, session=session)

        out_path = out_dir / f"frame_{saved+1:04d}.png"
        cv2.imwrite(str(out_path), result)
        saved += 1

        if saved % 5 == 0:
            print(f"  Processed {saved}/{total} frames...")

    cap.release()
    print(f"Done: {saved} frames saved to {out_dir}")

print("\n=== All done! ===")
