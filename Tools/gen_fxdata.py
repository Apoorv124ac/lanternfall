"""Writes Assets/Lanternfall/Scripts/FxData.cs from the pixelfx sheet JSON in Tools/fx-meta."""
import glob, json, os, sys
root = sys.argv[1]
rows = []
for f in sorted(glob.glob(os.path.join(root, "Tools/fx-meta/*.json"))):
    n = os.path.basename(f)[:-5]
    m = json.load(open(f))["meta"]["pixelfx"]
    a = m.get("act")
    contact = a["hits"][0]["t"] if a and a.get("hits") else 0
    rows.append(f'            {{ "{n}", new FxInfo({m["fps"]}, {str(m["loop"]).lower()}, {m["frames"]}, {m["columns"]}, '
                f'{m["frameSize"][0]}, {m["frameSize"][1]}, {m["pivot"][0]}, {m["pivot"][1]}, {contact}f) }},')
src = open(os.path.join(root, "Assets/Lanternfall/Scripts/FxData.cs")).read()
head, rest = src.split("        {\n            {", 1)
tail = rest[rest.index("        };"):]
open(os.path.join(root, "Assets/Lanternfall/Scripts/FxData.cs"), "w").write(head + "        {\n" + "\n".join(rows) + "\n" + tail)
