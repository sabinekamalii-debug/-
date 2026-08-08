import os
from pathlib import Path
import shutil

os.environ["PYTHONUTF8"] = "1"

base = Path(r"D:\unity\mowang\Assets\new characters\(1) 星豹")

# Clear old broken frames
for name in ["走路帧", "战斗帧"]:
    d = base / name
    if d.exists():
        for f in d.glob("*.png"):
            f.unlink()
        for f in d.glob("*.anim"):
            f.unlink()
        for f in d.glob("*.meta"):
            f.unlink()

# Copy walk frames
walk_src = base / "walk_frames_clean"
walk_dst = base / "走路帧"
walk_dst.mkdir(exist_ok=True)
for i, f in enumerate(sorted(walk_src.glob("*.png"))):
    shutil.copy2(f, walk_dst / f"frame_{i:04d}.png")
walk_count = len(list(walk_dst.glob("*.png")))
print(f"Walk: copied {walk_count} frames")

# Copy battle frames
battle_src = base / "battle_frames_clean"
battle_dst = base / "战斗帧"
battle_dst.mkdir(exist_ok=True)
for i, f in enumerate(sorted(battle_src.glob("*.png"))):
    shutil.copy2(f, battle_dst / f"frame_{i:04d}.png")
battle_count = len(list(battle_dst.glob("*.png")))
print(f"Battle: copied {battle_count} frames")
