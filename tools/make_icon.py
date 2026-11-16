"""Create the original Noel app icon. Pillow is only needed to rebuild this asset."""
from pathlib import Path
from math import sin, cos, pi
from PIL import Image, ImageDraw

size = 512
image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((8, 8, 504, 504), radius=112, fill="#10272f")
draw.rounded_rectangle((25, 25, 487, 487), radius=96, outline="#4d5b50", width=3)
for top, bottom, radius in ((158, 267, 66), (212, 335, 101), (275, 405, 140)):
    draw.polygon([(256, top), (256 + radius, bottom), (256 - radius, bottom)], fill="#245246")
    draw.line([(256 - radius + 13, bottom - 10), (256, top + 27), (256 + radius - 13, bottom - 10)], fill="#567453", width=3)
for y, w in ((232, 42), (296, 72), (363, 109)):
    draw.arc((256-w, y-17, 256+w, y+20), 0, 180, fill="#e8cd95", width=6)
    for j in range(7):
        x = 256-w + j*2*w/6
        yy = y+16*sin(j*pi/6)
        draw.ellipse((x-5, yy-5, x+5, yy+5), fill="#fff0c4")
points = []
for j in range(10):
    angle = -pi/2+j*pi/5
    r = 39 if j%2 == 0 else 16
    points.append((256+cos(angle)*r, 126+sin(angle)*r))
draw.polygon(points, fill="#edcf8e")
for x,y,r in ((100,140,4),(407,156,4),(132,235,3),(390,299,3),(91,346,4),(413,405,3)):
    draw.ellipse((x-r,y-r,x+r,y+r), fill="#b4c4c4")
out = Path(__file__).resolve().parents[1]/"assets"
out.mkdir(exist_ok=True)
image.save(out/"noel.ico", sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
image.save(out/"noel.png")
