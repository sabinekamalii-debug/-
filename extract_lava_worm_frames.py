import sys
import os
import cv2
import numpy as np
from pathlib import Path
from rembg import remove, new_session

# Force UTF-8 for Chinese paths
os.environ["PYTHONUTF8"] = "1"
sys.stdout.reconfigure(encoding='utf-8')

# === CONFIG - Update video filenames after user provides them ===
BASE = Path(r"D:\unity\mowang\Assets\new characters\(1) 熔岩虫")

# Walk video - UPDATE this filename
WALK_VIDEO = BASE / "walk.mp4"
# Battle video - UPDATE this filename
BATTLE_VIDEO = BASE / "battle.mp4"

# Output to temp dir OUTSIDE Assets to prevent Unity from deleting files without .meta
TEMP_DIR = Path(r"D:\unity\mowang\temp_lava_worm")
WALK_OUT = TEMP_DIR / "走路帧"
BATTLE_OUT = TEMP_DIR / "战斗帧"

# === rembg session ===
print("Initializing rembg session (birefnet-general)...")
session = new_session("birefnet-general")
print("rembg session ready.")


def find_cycle_length(frames_gray, max_check=80):
    """Find walk cycle length by comparing frame 0 to all subsequent frames."""
    if len(frames_gray) < 4:
        return len(frames_gray)
    
    ref = frames_gray[0].astype(np.float32)
    diffs = []
    for i in range(1, min(len(frames_gray), max_check)):
        curr = frames_gray[i].astype(np.float32)
        diff = np.mean(np.abs(ref - curr))
        diffs.append((i, diff))
    
    # Find the first local minimum after frame 3 (skip early frames that are too similar)
    for i in range(3, len(diffs)):
        if diffs[i][1] < diffs[i-1][1] and diffs[i][1] < diffs[i+1][1] if i+1 < len(diffs) else False:
            cycle_len = diffs[i][0]
            print(f"  Cycle detected at frame {cycle_len} (diff={diffs[i][1]:.2f})")
            return cycle_len
    
    # Fallback: find global min
    best = min(diffs, key=lambda x: x[1])
    print(f"  No clear cycle, closest match at frame {best[0]} (diff={best[1]:.2f})")
    return best[0]


def extract_all_frames(video_path):
    """Extract all frames from video as BGR arrays."""
    cap = cv2.VideoCapture(str(video_path))
    if not cap.isOpened():
        print(f"ERROR: Cannot open {video_path}")
        return []
    
    total = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    fps = cap.get(cv2.CAP_PROP_FPS)
    w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    print(f"  Video: {video_path.name}, {w}x{h}, {fps:.1f}fps, {total} frames")
    
    frames = []
    while True:
        ret, frame = cap.read()
        if not ret:
            break
        frames.append(frame)
    cap.release()
    print(f"  Extracted {len(frames)} frames")
    return frames


def remove_bg_and_save(frame_bgr, out_path):
    """Remove background using rembg and save as PNG with alpha."""
    result_bytes = remove(frame_bgr, session=session)
    result_array = np.frombuffer(result_bytes, dtype=np.uint8)
    result_image = cv2.imdecode(result_array, cv2.IMREAD_UNCHANGED)
    
    if result_image.shape[2] == 4:
        result_bgra = cv2.cvtColor(result_image, cv2.COLOR_RGBA2BGRA)
    else:
        result_bgra = result_image
    
    # Use imencode + write_bytes for Chinese path compatibility
    success, encoded = cv2.imencode('.png', result_bgra)
    if success:
        out_path.write_bytes(encoded.tobytes())
        return True
    return False


def process_walk_video(video_path, output_dir):
    """Extract walk frames: find cycle, sample ~11 frames evenly across one cycle."""
    frames = extract_all_frames(video_path)
    if len(frames) < 4:
        print(f"  Too few frames ({len(frames)}), skipping walk")
        return []
    
    # Convert to grayscale for cycle detection
    gray_frames = [cv2.cvtColor(f, cv2.COLOR_BGR2GRAY) for f in frames]
    
    # Find cycle length
    cycle_len = find_cycle_length(gray_frames)
    
    # Sample ~11 frames evenly across the cycle
    target_count = 11
    if cycle_len <= target_count:
        # Use all frames in the cycle
        indices = list(range(cycle_len))
    else:
        # Evenly sample target_count frames from 0 to cycle_len-1
        indices = [int(i * (cycle_len - 1) / (target_count - 1)) for i in range(target_count)]
    
    print(f"  Sampling {len(indices)} frames from cycle (len={cycle_len}): {indices}")
    
    output_dir.mkdir(parents=True, exist_ok=True)
    results = []
    for i, idx in enumerate(indices):
        if idx >= len(frames):
            idx = idx % len(frames)
        print(f"  Frame {i+1}/{len(indices)} (video frame {idx}): removing bg...", end=" ", flush=True)
        out_path = output_dir / f"walk_{i+1:02d}.png"
        if remove_bg_and_save(frames[idx], out_path):
            results.append(out_path)
            print(f"OK -> {out_path.name}")
        else:
            print("FAILED")
    
    # Verify files exist
    actual = list(output_dir.glob("*.png"))
    print(f"  Verified: {len(actual)} PNGs in {output_dir}")
    return results


def process_battle_video(video_path, output_dir):
    """Extract battle/idle frames: use first 30 frames, sample ~11 with most variation."""
    frames = extract_all_frames(video_path)
    if len(frames) < 4:
        print(f"  Too few frames ({len(frames)}), skipping battle")
        return []
    
    # Take first 30 frames (most stable action segment)
    candidate_frames = frames[:min(30, len(frames))]
    
    # Convert to grayscale
    gray_frames = [cv2.cvtColor(f, cv2.COLOR_BGR2GRAY) for f in candidate_frames]
    
    # Calculate frame-to-frame differences to find frames with most variation
    diffs = []
    for i in range(1, len(gray_frames)):
        diff = np.mean(np.abs(gray_frames[i].astype(np.float32) - gray_frames[i-1].astype(np.float32)))
        diffs.append((i, diff))
    
    # Sort by difference (descending) and pick top N frames with most action
    target_count = min(11, len(candidate_frames))
    
    # Evenly sample across the candidate range for smooth animation
    if len(candidate_frames) <= target_count:
        indices = list(range(len(candidate_frames)))
    else:
        indices = [int(i * (len(candidate_frames) - 1) / (target_count - 1)) for i in range(target_count)]
    
    print(f"  Sampling {len(indices)} battle frames: {indices}")
    
    output_dir.mkdir(parents=True, exist_ok=True)
    results = []
    for i, idx in enumerate(indices):
        if idx >= len(frames):
            idx = idx % len(frames)
        print(f"  Frame {i+1}/{len(indices)} (video frame {idx}): removing bg...", end=" ", flush=True)
        out_path = output_dir / f"battle_{i+1:02d}.png"
        if remove_bg_and_save(frames[idx], out_path):
            results.append(out_path)
            print(f"OK -> {out_path.name}")
        else:
            print("FAILED")
    
    actual = list(output_dir.glob("*.png"))
    print(f"  Verified: {len(actual)} PNGs in {output_dir}")
    return results


# === MAIN ===
TEMP_DIR.mkdir(parents=True, exist_ok=True)

print("=" * 60)
print("Processing WALK video")
if WALK_VIDEO.exists():
    walk_frames = process_walk_video(WALK_VIDEO, WALK_OUT)
    print(f"WALK: {len(walk_frames)} frames -> {WALK_OUT}")
else:
    print(f"Walk video not found: {WALK_VIDEO}")
    print("Please update WALK_VIDEO variable with the correct filename.")

print("=" * 60)
print("Processing BATTLE video")
if BATTLE_VIDEO.exists():
    battle_frames = process_battle_video(BATTLE_VIDEO, BATTLE_OUT)
    print(f"BATTLE: {len(battle_frames)} frames -> {BATTLE_OUT}")
else:
    print(f"Battle video not found: {BATTLE_VIDEO}")
    print("Please update BATTLE_VIDEO variable with the correct filename.")

print("=" * 60)
print("DONE! Frames are in temp dir. Next: C# script to copy into Assets.")
