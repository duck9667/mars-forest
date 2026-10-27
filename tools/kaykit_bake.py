#!/usr/bin/env python3
# KayKit(.gltf + .bin + 팔레트 아틀라스) → 게임 정점 색 모델(.bytes) 굽기. 표준 라이브러리 + PIL 만 쓴다.
#
# KayKit 은 색을 팔레트 아틀라스 텍스처(세로 그라데이션 칸)에 두고 UV 로 가리킨다. 게임은 텍스처 없이 정점 색 셰이더
# (Resources/Shaders/MarsVColor)로 그리므로, 정점마다 UV 자리의 아틀라스 색을 읽어 정점 색으로 굽는다(그라데이션은 정점 사이 보간으로 남는다).
# 좌표는 Unity 로컬로 바꿔 저장한다: glTF(오른손) → Unity(왼손) z 반전 + 감김 뒤집기. 단위는 KayKit 원래 단위(배치할 때 C# 에서 스케일).
#
# 출력(little endian) Resources/Models/kk_<이름>.bytes:
#   "KKM1" | uint32 정점 수 nv | uint32 인덱스 수 ni | float32×6 경계(min xyz, max xyz)
#   | float32×3×nv 위치 | float32×3×nv 법선 | uint8×4×nv 색(sRGB RGBA) | uint32×ni 인덱스
#
# 사용:
#   python3 -I tools/kaykit_bake.py bake  <팩 gltf 폴더> <출력 폴더> 이름 [이름 ...]
#   python3 -I tools/kaykit_bake.py sheet <팩 gltf 폴더> <출력 png> [이름 ...]   (미리보기 접촉 인화: 고를 때 보기용)
import json
import math
import os
import struct
import sys

from PIL import Image, ImageDraw

COMP = {5126: ('f', 4), 5125: ('I', 4), 5123: ('H', 2), 5121: ('B', 1), 5122: ('h', 2), 5120: ('b', 1)}
NCOMP = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


def mat_mul(a, b):
    return [sum(a[r + 4 * k] * b[k + 4 * c] for k in range(4)) for c in range(4) for r in range(4)]  # 열 우선 4×4


def node_matrix(n):
    if 'matrix' in n:
        return list(n['matrix'])
    tx, ty, tz = n.get('translation', [0, 0, 0])
    qx, qy, qz, qw = n.get('rotation', [0, 0, 0, 1])
    sx, sy, sz = n.get('scale', [1, 1, 1])
    r = [1 - 2 * (qy * qy + qz * qz), 2 * (qx * qy + qz * qw), 2 * (qx * qz - qy * qw),
         2 * (qx * qy - qz * qw), 1 - 2 * (qx * qx + qz * qz), 2 * (qy * qz + qx * qw),
         2 * (qx * qz + qy * qw), 2 * (qy * qz - qx * qw), 1 - 2 * (qx * qx + qy * qy)]
    return [r[0] * sx, r[1] * sx, r[2] * sx, 0, r[3] * sy, r[4] * sy, r[5] * sy, 0, r[6] * sz, r[7] * sz, r[8] * sz, 0, tx, ty, tz, 1]


def xf_point(m, p):
    x, y, z = p
    return (m[0] * x + m[4] * y + m[8] * z + m[12], m[1] * x + m[5] * y + m[9] * z + m[13], m[2] * x + m[6] * y + m[10] * z + m[14])


def xf_dir(m, p):
    x, y, z = p
    v = (m[0] * x + m[4] * y + m[8] * z, m[1] * x + m[5] * y + m[9] * z, m[2] * x + m[6] * y + m[10] * z)
    l = math.sqrt(v[0] ** 2 + v[1] ** 2 + v[2] ** 2) or 1.0
    return (v[0] / l, v[1] / l, v[2] / l)


class Gltf:
    def __init__(self, path):
        self.dir = os.path.dirname(path)
        with open(path, 'r', encoding='utf-8') as f:
            self.j = json.load(f)
        self.bufs = []
        for b in self.j.get('buffers', []):
            with open(os.path.join(self.dir, b['uri']), 'rb') as f:
                self.bufs.append(f.read())
        self.imgs = {}

    def accessor(self, ai):
        a = self.j['accessors'][ai]
        fmt, size = COMP[a['componentType']]
        n = NCOMP[a['type']]
        bv = self.j['bufferViews'][a['bufferView']]
        buf = self.bufs[bv['buffer']]
        off = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
        stride = bv.get('byteStride', 0) or n * size
        out = []
        for k in range(a['count']):
            vals = struct.unpack_from('<' + fmt * n, buf, off + k * stride)
            if a.get('normalized'):
                mx = {'B': 255.0, 'H': 65535.0, 'b': 127.0, 'h': 32767.0}.get(fmt, 1.0)
                vals = tuple(v / mx for v in vals)
            out.append(vals if n > 1 else vals[0])
        return out

    def image_of(self, mat_index):
        m = self.j['materials'][mat_index]
        pbr = m.get('pbrMetallicRoughness', {})
        if 'baseColorTexture' in pbr:
            tex = self.j['textures'][pbr['baseColorTexture']['index']]
            uri = self.j['images'][tex['source']]['uri']
            if uri not in self.imgs:
                im = Image.open(os.path.join(self.dir, uri)).convert('RGBA')
                self.imgs[uri] = (im, im.load())
            return self.imgs[uri], None
        f = pbr.get('baseColorFactor', [1, 1, 1, 1])
        lin2s = lambda v: int(round(255 * (v * 12.92 if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055)))
        return None, (lin2s(f[0]), lin2s(f[1]), lin2s(f[2]), 255)


def load_model(path):
    """gltf → (위치, 법선, 색, 인덱스) Unity 좌표. 노드 변환 적용."""
    g = Gltf(path)
    P, N, C, I = [], [], [], []
    nodes = g.j['nodes']
    scene = g.j['scenes'][g.j.get('scene', 0)]

    def walk(ni, parent):
        n = nodes[ni]
        m = mat_mul(parent, node_matrix(n))
        if 'mesh' in n:
            for pr in g.j['meshes'][n['mesh']]['primitives']:
                if pr.get('mode', 4) != 4:
                    continue
                at = pr['attributes']
                pos = g.accessor(at['POSITION'])
                nrm = g.accessor(at['NORMAL']) if 'NORMAL' in at else None
                uv = g.accessor(at['TEXCOORD_0']) if 'TEXCOORD_0' in at else None
                idx = g.accessor(pr['indices']) if 'indices' in pr else list(range(len(pos)))
                img, flat = g.image_of(pr['material']) if 'material' in pr else (None, (255, 255, 255, 255))
                base = len(P)
                for k, p in enumerate(pos):
                    wp = xf_point(m, p)
                    P.append((wp[0], wp[1], -wp[2]))
                    if nrm:
                        wn = xf_dir(m, nrm[k])
                        N.append((wn[0], wn[1], -wn[2]))
                    else:
                        N.append((0.0, 1.0, 0.0))
                    if img and uv:
                        im, px = img
                        u, v = uv[k]
                        x = min(im.width - 1, max(0, int((u % 1.0) * im.width)))
                        y = min(im.height - 1, max(0, int((v % 1.0) * im.height)))
                        C.append(px[x, y])
                    else:
                        C.append(flat)
                for t in range(0, len(idx) - 2, 3):  # z 반전이라 감김을 뒤집는다
                    I.extend((base + idx[t], base + idx[t + 2], base + idx[t + 1]))
        for c in n.get('children', []):
            walk(c, m)

    ident = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]
    for r in scene['nodes']:
        walk(r, ident)
    return P, N, C, I


def write_bytes(out, P, N, C, I):
    mn = [min(p[a] for p in P) for a in range(3)]
    mx = [max(p[a] for p in P) for a in range(3)]
    with open(out, 'wb') as f:
        f.write(b'KKM1')
        f.write(struct.pack('<II', len(P), len(I)))
        f.write(struct.pack('<6f', *mn, *mx))
        f.write(b''.join(struct.pack('<3f', *p) for p in P))
        f.write(b''.join(struct.pack('<3f', *n) for n in N))
        f.write(b''.join(struct.pack('<4B', *c) for c in C))
        f.write(struct.pack('<%dI' % len(I), *I))
    return mn, mx


def render(P, N, C, I, size=240, elev=55.0, yaw=0.0):
    """게임 카메라(남쪽에서 고도 55°)처럼 보는 간이 소프트웨어 렌더(삼각형 깊이 정렬 + 램버트)."""
    ce, se = math.cos(math.radians(elev)), math.sin(math.radians(elev))
    cy, sy = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    L = (-0.35, 0.8, -0.45)
    ll = math.sqrt(sum(v * v for v in L))
    L = tuple(v / ll for v in L)

    def view(p):
        x, y, z = p
        x, z = x * cy - z * sy, x * sy + z * cy
        # 카메라는 −z(남)에서 +z(북)를 내려다본다: 화면 y = 높이·cos − 깊이·sin
        sx, sy2 = x, y * ce + z * se
        depth = z * ce - y * se
        return sx, sy2, depth

    V = [view(p) for p in P]
    xs = [v[0] for v in V]
    ys = [v[1] for v in V]
    w = max(xs) - min(xs)
    h = max(ys) - min(ys)
    sc = (size * 0.86) / max(w, h, 1e-6)
    ox = size / 2 - (max(xs) + min(xs)) / 2 * sc
    oy = size / 2 + (max(ys) + min(ys)) / 2 * sc
    tris = []
    for t in range(0, len(I), 3):
        a, b, c = I[t], I[t + 1], I[t + 2]
        d = (V[a][2] + V[b][2] + V[c][2]) / 3
        tris.append((d, a, b, c))
    tris.sort(key=lambda x: -x[0])
    im = Image.new('RGB', (size, size), (236, 226, 212))
    dr = ImageDraw.Draw(im)
    for d, a, b, c in tris:
        n = [(N[a][k] + N[b][k] + N[c][k]) / 3 for k in range(3)]
        lam = max(0.0, n[0] * L[0] + n[1] * L[1] + n[2] * L[2])
        s = 0.55 + 0.5 * lam
        col = tuple(min(255, int((C[a][k] + C[b][k] + C[c][k]) / 3 * s)) for k in range(3))
        pts = [(ox + V[i][0] * sc, oy - V[i][1] * sc) for i in (a, b, c)]
        dr.polygon(pts, fill=col)
    return im


def main():
    if len(sys.argv) < 4:
        print(__doc__ or '사용법은 파일 머리 주석 참고')
        sys.exit(1)
    cmd, src = sys.argv[1], sys.argv[2]
    if cmd == 'bake':
        out_dir = sys.argv[3]
        os.makedirs(out_dir, exist_ok=True)
        for name in sys.argv[4:]:
            P, N, C, I = load_model(os.path.join(src, name + '.gltf'))
            out = os.path.join(out_dir, 'kk_' + name + '.bytes')
            mn, mx = write_bytes(out, P, N, C, I)
            print('%-34s 정점 %5d 삼각형 %5d  크기 %.2f×%.2f×%.2f  %5.1fKB' % (name, len(P), len(I) // 3, mx[0] - mn[0], mx[1] - mn[1], mx[2] - mn[2], os.path.getsize(out) / 1024))
    elif cmd == 'sheet':
        out = sys.argv[3]
        names = sys.argv[4:] or sorted(f[:-5] for f in os.listdir(src) if f.endswith('.gltf'))
        cell, cols = 240, 8
        rows = (len(names) + cols - 1) // cols
        sheet = Image.new('RGB', (cols * cell, rows * (cell + 18)), (255, 255, 255))
        dr = ImageDraw.Draw(sheet)
        for i, name in enumerate(names):
            P, N, C, I = load_model(os.path.join(src, name + '.gltf'))
            im = render(P, N, C, I, cell)
            x, y = (i % cols) * cell, (i // cols) * (cell + 18)
            sheet.paste(im, (x, y))
            mn = [min(p[a] for p in P) for a in range(3)]
            mx = [max(p[a] for p in P) for a in range(3)]
            dr.text((x + 4, y + cell + 2), '%s %.1fx%.1fx%.1f' % (name, mx[0] - mn[0], mx[1] - mn[1], mx[2] - mn[2]), fill=(0, 0, 0))
        sheet.save(out)
        print('미리보기', out, len(names), '개')
    else:
        print('모르는 명령', cmd)
        sys.exit(1)


if __name__ == '__main__':
    main()
