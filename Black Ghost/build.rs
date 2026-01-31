use std::env;
use std::fs::File;
use std::io::{BufWriter, Write};
use std::path::Path;

const ICE_SMOD: [[u32; 4]; 4] = [
    [333, 313, 505, 369], [379, 375, 319, 391],
    [361, 445, 451, 397], [397, 425, 395, 505],
];
const ICE_SXOR: [[u32; 4]; 4] = [
    [0x83, 0x85, 0x9b, 0xcd], [0xcc, 0xa7, 0xad, 0x41],
    [0x4b, 0x2e, 0xd4, 0x33], [0xea, 0xcb, 0x2e, 0x04],
];
const ICE_PBOX: [u32; 32] = [
    0x00000001, 0x00000080, 0x00000400, 0x00002000, 0x00080000, 0x00200000, 0x01000000, 0x40000000,
    0x00000008, 0x00000020, 0x00000100, 0x00004000, 0x00010000, 0x00800000, 0x04000000, 0x20000000,
    0x00000004, 0x00000010, 0x00000200, 0x00008000, 0x00020000, 0x00400000, 0x08000000, 0x10000000,
    0x00000002, 0x00000040, 0x00000800, 0x00001000, 0x00040000, 0x00100000, 0x02000000, 0x80000000,
];

fn gf_mult(mut a: u32, mut b: u32, m: u32) -> u32 {
    let mut res: u32 = 0;
    while b != 0 {
        if b & 1 != 0 { res ^= a; }
        a <<= 1;
        b >>= 1;
        if a >= 256 { a ^= m; }
    }
    res
}
fn gf_exp7(b: u32, m: u32) -> u32 {
    if b == 0 { return 0; }
    let mut x = gf_mult(b, b, m);
    x = gf_mult(b, x, m);
    x = gf_mult(x, x, m);
    gf_mult(b, x, m)
}
fn ice_perm32(mut x: u32) -> u32 {
    let mut res: u32 = 0;
    for pb in ICE_PBOX.iter() {
        if x & 1 != 0 { res |= pb; }
        x >>= 1;
    }
    res
}

fn main() {
    let out_dir = env::var("OUT_DIR").unwrap();

    let sbox_path = Path::new(&out_dir).join("ice_sbox.bin");
    let mut f_sbox = BufWriter::new(File::create(&sbox_path).unwrap());
    
    let mut sbox_data = vec![0u32; 4096];

    for i in 0..1024 {
        let col = ((i >> 1) & 0xff) as u32;
        let row = (i & 0x1) | ((i & 0x200) >> 8);

        sbox_data[i]        = ice_perm32(gf_exp7(col ^ ICE_SXOR[0][row], ICE_SMOD[0][row]) << 24);
        sbox_data[1024 + i] = ice_perm32(gf_exp7(col ^ ICE_SXOR[1][row], ICE_SMOD[1][row]) << 16);
        sbox_data[2048 + i] = ice_perm32(gf_exp7(col ^ ICE_SXOR[2][row], ICE_SMOD[2][row]) << 8);
        sbox_data[3072 + i] = ice_perm32(gf_exp7(col ^ ICE_SXOR[3][row], ICE_SMOD[3][row]));
    }

    for val in sbox_data {
        f_sbox.write_all(&val.to_le_bytes()).unwrap();
    }

    println!("cargo:rerun-if-changed=build.rs");
}