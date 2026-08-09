import sys
sys.stdout.reconfigure(encoding='utf-8')
from pathlib import Path
from rembg import remove, new_session

session = new_session("birefnet-general")

char_dir = Path(r"D:\unity\mowang\Assets\new characters\人族\奶妈")
out_dir = Path(r"D:\unity\mowang\temp_healer_img")
out_dir.mkdir(exist_ok=True)

for f in sorted(char_dir.iterdir()):
    if f.suffix.lower() in ('.jpeg', '.jpg', '.png') and not f.name.endswith('.meta'):
        with open(f, 'rb') as inp:
            data = inp.read()
        out_data = remove(data, session=session)
        out_path = out_dir / (f.stem + ".png")
        with open(out_path, 'wb') as out:
            out.write(out_data)
        print(f"IMG: {f.name} -> {out_path.name}")

print("ALL DONE")
