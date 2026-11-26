"""Render the original linework through a continuous local deformation, including the shoulder."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

assets = Path(__file__).resolve().parents[3] / "frontend/src/assets/fehler"
source = Image.open(assets / "biber-fehler.png").convert("RGBa")
pixels = np.asarray(source, dtype=float)
y, x = np.indices(pixels.shape[:2], dtype=float)

def ramp(a, b, value):
    t = np.clip((value-a)/(b-a), 0, 1)
    return t*t*(3-2*t)

def beat(t, start, end):
    return np.sin(np.pi*np.clip((t-start)/(end-start), 0, 1))**2

# The wrench and gripping fingers travel together. Motion fades through the
# forearm towards the shoulder and through the neck towards the seated body.
# Keep the entire face rigid. The transition runs through the gap between
# the wrench and muzzle, then blends into the neck below the tooth.
face = 1 - ramp(68, 78, y) * (1 - ramp(0, 7, x - (31 + .42*(y-78))))
head = face * (1-ramp(101, 132, y)) * (1-ramp(144, 154, x))
# Isolate the original wrench and grip. The boundary crosses the arm only
# at the wrist, where it pivots. The rest of the outline borders transparency.
grip_mask = Image.new("L", source.size)
ImageDraw.Draw(grip_mask).polygon(
    [(0,72),(31,72),(39,91),(41,102),(53,110),(55,115),
     (55,134),(59,141),(0,146)], fill=255)
grip = np.asarray(grip_mask, dtype=float) / 255
grip_pixels = pixels * grip[...,None]
body_pixels = pixels * (1-grip[...,None])

def sample(layer, u, v):
    u = np.clip(u, 0, source.width-1.001)
    v = np.clip(v, 0, source.height-1.001)
    i, j = u.astype(int), v.astype(int)
    a, b = (u-i)[...,None], (v-j)[...,None]
    return (1-b)*((1-a)*layer[j,i]+a*layer[j,i+1])+b*((1-a)*layer[j+1,i]+a*layer[j+1,i+1])

tail = ramp(145, 178, x) * (1-ramp(130, 161, y))

def render(t, lift, tilt):
    swing = lift*(beat(t,.16,1.05)-.36*beat(t,1.05,1.6)+beat(t,2.22,3.14)-.36*beat(t,3.14,3.72))
    nod = tilt*(beat(t,.18,2.12)-.55*beat(t,2.2,4.55))
    angle = head * nod
    cos_delta, sin_angle = np.cos(angle)-1, np.sin(angle)
    # Rotate about the wrist. Keep the adjoining forearm on that same
    # transform at the seam, then ease the motion out towards the shoulder.
    c, sn = np.cos(swing), np.sin(swing)
    gx = (c-1)*(x-55) + sn*(y-122)
    gy = -sn*(x-55) + (c-1)*(y-122)
    wrist = (1-ramp(60,88,x))*ramp(105,113,y)*(1-ramp(133,146,y))
    dx = wrist*gx + (1-wrist)*(cos_delta*(x-113) + sin_angle*(y-99))
    dy = wrist*gy + (1-wrist)*(-sin_angle*(x-113) + cos_delta*(y-99)) + tail*2.3*beat(t,.6,3.9)
    body = sample(body_pixels,x+dx,y+dy)
    tool = sample(grip_pixels,x+gx,y+gy)
    # At the wrist the two samples partition the same original pixels.
    rgba = np.clip(body + tool,0,255)
    return Image.frombytes("RGBa", source.size, rgba.round().astype("uint8").tobytes()).convert("RGBA")

for name, lift, tilt in [("reparieren",.145,.039),("suchen",.036,.064),("warten",.018,.029)]:
    frames = [render(i/25,lift,tilt) for i in range(120)]
    frames[0] = frames[-1] = source.convert("RGBA")
    frames[0].save(assets / (name+".webp"),save_all=True,append_images=frames[1:],duration=40,loop=0,lossless=True,method=4)
