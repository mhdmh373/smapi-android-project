#!/usr/bin/env python3
"""قارئ metadata مصغر لملفات .NET (بدون أي اعتماد خارجي).

يجيب عن سؤالين فقط من StardewValley.dll (قراءة محلية):
  1) ما توقيع Game1.warpFarmer (عدد المعاملات) في 1.6.15.24354؟
  2) ما أعضاء Farmer المطابقة للحقيبة/السعة (inventory/Items/MaxItems)؟

الاستعمال:
  python3 tools/mdparse.py /path/StardewValley.dll --methods StardewValley.Game1 warpFarmer
  python3 tools/mdparse.py /path/StardewValley.dll --members StardewValley.Farmer
"""
import struct
import sys

ET = {0x01: "void", 0x02: "bool", 0x08: "int", 0x0E: "string", 0x1C: "object"}


def u32(b, o):
    return struct.unpack_from("<I", b, o)[0]


def read_cuint(blob, o):
    b0 = blob[o]
    if b0 & 0x80 == 0:
        return b0, o + 1
    if b0 & 0x40 == 0:
        return ((b0 & 0x3F) << 8) | blob[o + 1], o + 2
    return ((b0 & 0x1F) << 24) | (blob[o + 1] << 16) | (blob[o + 2] << 8) | blob[o + 3], o + 4


class MD:
    def __init__(self, path):
        self.data = open(path, "rb").read()
        d = self.data
        pe = u32(d, 0x3C)
        coff = pe + 4
        nsec = struct.unpack_from("<H", d, coff + 2)[0]
        opt = coff + 20
        magic = struct.unpack_from("<H", d, opt)[0]
        dd = opt + (96 if magic == 0x10B else 112)
        cli_rva, cli_size = struct.unpack_from("<II", d, dd + 14 * 8)
        self.sections = []
        sh = opt + (224 if magic == 0x10B else 240)
        for i in range(nsec):
            vsize, vaddr, rawsize, rawptr = struct.unpack_from("<IIII", d, sh + i * 40 + 8)
            self.sections.append((vaddr, rawptr, max(vsize, rawsize)))
        cli = self.rva(cli_rva)
        md_rva, md_size = struct.unpack_from("<II", d, cli + 8)
        base = self.rva(md_rva)
        assert d[base:base + 4] == b"BSJB", "not a CLI metadata root"
        ver_len = u32(d, base + 12)
        p = base + 16 + ((ver_len + 3) & ~3)
        p += 2  # flags
        nstreams = struct.unpack_from("<H", d, p)[0]
        p += 2
        self.streams = {}
        for _ in range(nstreams):
            off, size = struct.unpack_from("<II", d, p)
            p += 8
            name = b""
            while d[p] != 0:
                name += bytes([d[p]])
                p += 1
            p = (p + 4) & ~3
            self.streams[name.decode()] = (base + off, size)
        tilde_off, _ = self.streams["#~"]
        p = tilde_off
        p += 4  # reserved
        p += 2  # major/minor version
        heap = d[p]  # heap sizes
        p += 1
        p += 1  # reserved
        valid = struct.unpack_from("<Q", d, p)[0]
        p += 8
        # sorted skipped
        p += 8
        order = [0, 1, 2, 4, 6, 8, 10]
        self.tables = {}
        counts = []
        for t in range(64):
            if valid & (1 << t):
                n = u32(d, p)
                p += 4
                counts.append((t, n))
        # heap index sizes
        str_big = heap & 1
        blob_big = heap & 4
        # table row counts for coded index sizing
        def rows(t):
            for tt, nn in counts:
                if tt == t:
                    return nn
            return 0
        # compute row sizes for needed tables
        self.str_big = str_big
        self.blob_big = blob_big
        S = lambda: 4 if str_big else 2
        B = lambda: 4 if blob_big else 2
        # coded-index widths + full row sizes (ECMA-335 II.22)
        def W(t):
            return 4 if rows(t) >= (1 << 16) else 2

        def coded(pairs):
            for t, bits in pairs:
                if rows(t) >= (1 << (16 - bits)):
                    return 4
            return 2

        FL, ML, PL, TD = W(4), W(6), W(8), W(2)
        self.FL, self.ML = FL, ML
        TDR = coded([(2, 2), (1, 2), (26, 2)])
        RS = coded([(0, 2), (25, 2), (32, 2), (1, 2)])
        MRP = coded([(2, 3), (1, 3), (25, 3), (6, 3), (26, 3)])
        HCA = coded([(2, 5), (6, 5), (20, 5), (28, 5), (8, 5), (23, 5), (12, 5), (22, 5), (4, 5), (26, 5), (10, 5), (25, 5), (32, 5), (1, 5), (35, 5), (44, 5), (42, 5), (41, 5)])
        CAT = coded([(6, 3), (10, 3)])
        MDOR = coded([(6, 1), (10, 1)])
        HS = coded([(20, 1), (22, 1)])
        IMP = coded([(35, 2), (32, 2), (41, 2)])
        TOMD = coded([(2, 1), (6, 1)])
        G = 4 if (heap & 2) else 2
        self.TDR, self.TD = TDR, TD
        self.sizes = {
            0: 2 + S() + G + 2 + 2,
            1: RS + S() + S(),
            2: 4 + S() + S() + TDR + FL + ML,
            4: 2 + S() + B(),
            6: 4 + 2 + 2 + S() + B() + PL,
            8: 2 + 2 + S(),
            9: TD + TDR,
            10: MRP + S() + B(),
            11: 2 + 2 + coded([(4, 2), (8, 2), (22, 2)]) + B(),
            12: HCA + CAT + B(),
            13: coded([(4, 1), (8, 1)]) + B(),
            14: 2 + coded([(2, 2), (6, 2), (28, 2)]) + B(),
            15: 2 + 4 + TD,
            16: 4 + FL,
            17: B(),
            18: TD + W(20),
            20: 2 + S() + TDR,
            21: TD + W(22),
            23: 2 + ML + HS,
            24: TD + MDOR + MDOR,
            25: S(),
            26: B(),
            27: 2 + coded([(4, 1), (6, 1)]) + S() + IMP,
            28: 4 + 2 + 2 + 2 + 2 + 4 + B() + S() + S(),
            29: 4,
            32: 2 + 2 + 2 + 2 + 4 + B() + S() + S() + B(),
            35: 2 + S() + B(),
            41: 4 + 4 + S() + S() + IMP,
            42: 4 + 4 + S() + IMP,
            43: TD + TD,
            44: 2 + 2 + TOMD + S(),
        }
        # row data starts after all counts; assign offsets sequentially
        for t, n in counts:
            sz = self.sizes.get(t, 0)
            self.tables[t] = (p, n, sz)
            p += n * sz
        s_off = self.streams["#Strings"][0]
        self.strings = (s_off, d)
        b_off = self.streams["#Blob"][0]
        self.blobs = (b_off, d)

    def rva(self, rva):
        for va, raw, size in self.sections:
            if va <= rva < va + size:
                return raw + (rva - va)
        raise ValueError("bad RVA")

    def get_str(self, idx):
        if idx == 0:
            return ""
        off, d = self.strings
        end = d.index(b"\x00", off + idx)
        return d[off + idx:end].decode("utf-8", "replace")

    def get_blob(self, idx):
        off, d = self.blobs
        o = off + idx
        ln, p = read_cuint(d, o)
        return d[p:p + ln]

    def rows(self, t):
        off, n, sz = self.tables[t]
        d = self.data
        out = []
        for i in range(n):
            out.append(d[off + i * sz:off + (i + 1) * sz])
        return out

    def type_name(self, idx):
        # TypeDef row
        off, n, sz = self.tables[2]
        r = self.data[off + (idx - 1) * sz:off + idx * sz]
        S = 4 if self.str_big else 2
        name = self.get_str(struct.unpack_from("<H" if S == 2 else "<I", r, 4)[0])
        ns = self.get_str(struct.unpack_from("<H" if S == 2 else "<I", r, 4 + S)[0])
        return (ns + "." + name) if ns else name

    def find_type(self, full):
        for i, r in enumerate(self.rows(2)):
            if self.type_name(i + 1) == full:
                return i + 1, r
        return None, None

    def decode_type(self, blob, o):
        code = blob[o]
        o += 1
        if code in ET:
            return ET[code], o
        if code in (0x11, 0x12):  # VALUETYPE/CLASS + TypeDefOrRef coded
            idx, o = read_cuint(blob, o)
            tag, row = idx & 3, idx >> 2
            if tag == 0:
                return self.type_name(row), o
            if tag == 1:
                return "typeref:" + self.typeref_name(row), o
            return "typespec", o
        if code == 0x1D:  # SZARRAY
            inner, o = self.decode_type(blob, o)
            return inner + "[]", o
        if code == 0x0F:  # PTR
            inner, o = self.decode_type(blob, o)
            return inner + "*", o
        if code == 0x10:  # BYREF
            inner, o = self.decode_type(blob, o)
            return inner + "&", o
        if code == 0x15:  # GENERICINST
            gen, o = self.decode_type(blob, o)
            n, o = read_cuint(blob, o)
            args = []
            for _ in range(n):
                a, o = self.decode_type(blob, o)
                args.append(a)
            return gen + "<" + ",".join(args) + ">", o
        if code == 0x13:  # VAR/MVAR
            n, o = read_cuint(blob, o)
            return "T" + str(n), o
        return "t0x%02x" % code, o

    def typeref_name(self, idx):
        for i, r in enumerate(self.rows(1)):
            if i + 1 == idx:
                S = 4 if self.str_big else 2
                fmt = "<H" if S == 2 else "<I"
                name = self.get_str(struct.unpack_from(fmt, r, 2)[0])
                ns = self.get_str(struct.unpack_from(fmt, r, 2 + S)[0])
                return (ns + "." + name) if ns else name
        return "?"

    def method_sig(self, blob_idx):
        blob = self.get_blob(blob_idx)
        o = 1  # skip calling convention
        nparams, o = read_cuint(blob, o)
        ret, o = self.decode_type(blob, o)
        params = []
        for _ in range(nparams):
            t, o = self.decode_type(blob, o)
            params.append(t)
        return ret, params


def td_range(md, idx):
    # يعيد (fieldStart, fieldEnd, methodStart, methodEnd) لنوع TypeDef برقم idx
    S = 4 if md.str_big else 2
    off, n, sz = md.tables[2]
    r = md.data[off + (idx - 1) * sz:off + idx * sz]
    base = 4 + S + S + md.TDR
    fmt = "<I" if md.FL == 4 else "<H"
    fstart = struct.unpack_from(fmt, r, base)[0]
    fmt = "<I" if md.ML == 4 else "<H"
    mstart = struct.unpack_from(fmt, r, base + md.FL)[0]
    fend = len(md.rows(4)) + 1
    mend = len(md.rows(6)) + 1
    if idx < n:
        rr = md.data[off + idx * sz:off + (idx + 1) * sz]
        fmt = "<I" if md.FL == 4 else "<H"
        fend = struct.unpack_from(fmt, rr, base)[0]
        fmt = "<I" if md.ML == 4 else "<H"
        mend = struct.unpack_from(fmt, rr, base + md.FL)[0]
    return fstart, fend, mstart, mend


def cmd_methods(md, full, filt):
    idx, _ = md.find_type(full)
    if idx is None:
        print("type not found: " + full)
        return
    S = 4 if md.str_big else 2
    B = 4 if md.blob_big else 2
    mrows = md.rows(6)
    _, _, mstart, mend = td_range(md, idx)
    for i in range(mstart, mend):
        mr = mrows[i - 1]
        name = md.get_str(struct.unpack_from("<H" if S == 2 else "<I", mr, 8)[0])
        if filt and filt not in name:
            continue
        sig = struct.unpack_from("<H" if B == 2 else "<I", mr, 8 + S)[0]
        ret, params = md.method_sig(sig)
        print(f"{full}.{name}({', '.join(params)}) -> {ret}")


def cmd_members(md, full):
    idx, _ = md.find_type(full)
    if idx is None:
        print("type not found: " + full)
        return
    S = 4 if md.str_big else 2
    fstart, fend, mstart, mend = td_range(md, idx)
    frows = md.rows(4)
    keys = ("item", "inventory", "max", "size", "capacity", "backpack")
    for i in range(fstart, fend):
        fr = frows[i - 1]
        name = md.get_str(struct.unpack_from("<H" if S == 2 else "<I", fr, 2)[0])
        if any(k in name.lower() for k in keys):
            print(f"FIELD {name}")
    mrows = md.rows(6)
    for i in range(mstart, mend):
        mr = mrows[i - 1]
        name = md.get_str(struct.unpack_from("<H" if S == 2 else "<I", mr, 8)[0])
        if name.startswith("get_") or name.startswith("set_"):
            prop = name[4:]
            if any(k in prop.lower() for k in keys):
                print(f"PROPERTY {prop} ({name})")
        elif any(k in name.lower() for k in keys):
            print(f"METHOD {name}")


if __name__ == "__main__":
    path = sys.argv[1]
    md = MD(path)
    if sys.argv[2] == "--methods":
        cmd_methods(md, sys.argv[3], sys.argv[4] if len(sys.argv) > 4 else None)
    elif sys.argv[2] == "--members":
        cmd_members(md, sys.argv[3])
