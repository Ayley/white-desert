pub struct IceCipher {
    key_rounds: usize,
    key_size: usize,
    keysched: Vec<[u32; 3]>,
    sbox: [[u32; 1024]; 4],
}

impl IceCipher {
    pub fn new(key: &[u8]) -> Self {
        let key_len = key.len();
        let mut key_size = 1;

        let key_rounds = if key_len == 8 {
            8
        } else if key_len % 16 == 0 {
            key_size = key_len / 16;
            key_len
        } else {
            panic!("Invalid key length for ICE cipher: {} bytes", key_len);
        };

        let mut ice = Self {
            key_rounds,
            key_size,
            keysched: vec![[0u32; 3]; key_rounds],
            sbox: [[0u32; 1024]; 4],
        };

        ice.ice_sbox_init();
        ice.ice_key_set(key);
        ice
    }

    fn gf_mult(mut a: u32, mut b: u32, m: u32) -> u32 {
        let mut res = 0;
        while b > 0 {
            if b & 1 > 0 {
                res ^= a;
            }
            a <<= 1;
            b >>= 1;
            if a >= 256 {
                a ^= m;
            }
        }
        res
    }

    fn gf_exp7(b: u32, m: u32) -> u32 {
        if b == 0 {
            return 0;
        }

        let b2 = Self::gf_mult(b, b, m);
        let b4 = Self::gf_mult(b2, b2, m);
        let b6 = Self::gf_mult(b2, b4, m);

        Self::gf_mult(b, b6, m)
    }

    fn ice_perm32(mut x: u32) -> u32 {
        const ICE_BOX: [u32; 32] = [
            0x00000001, 0x00000080, 0x00000400, 0x00002000, 0x00080000, 0x00200000, 0x01000000,
            0x40000000, 0x00000008, 0x00000020, 0x00000100, 0x00004000, 0x00010000, 0x00800000,
            0x04000000, 0x20000000, 0x00000004, 0x00000010, 0x00000200, 0x00008000, 0x00020000,
            0x00400000, 0x08000000, 0x10000000, 0x00000002, 0x00000040, 0x00000800, 0x00001000,
            0x00040000, 0x00100000, 0x02000000, 0x80000000,
        ];
        let mut res = 0;
        for &p in &ICE_BOX {
            if x & 1 > 0 {
                res |= p;
            }
            x >>= 1;
        }
        res
    }

    fn ice_sbox_init(&mut self) {
        const ICE_SMOD: [[u32; 4]; 4] = [
            [333, 313, 505, 369],
            [379, 375, 319, 391],
            [361, 445, 451, 397],
            [397, 425, 395, 505],
        ];
        const ICE_SXOR: [[u32; 4]; 4] = [
            [0x83, 0x85, 0x9b, 0xcd],
            [0xcc, 0xa7, 0xad, 0x41],
            [0x4b, 0x2e, 0xd4, 0x33],
            [0xea, 0xcb, 0x2e, 0x04],
        ];

        for i in 0..1024 {
            let col = (i >> 1) & 0xff;
            let row = (i & 0x1) | ((i & 0x200) >> 8);

            for s in 0..4 {
                let val = col ^ ICE_SXOR[s][row as usize];
                let m = ICE_SMOD[s][row as usize];

                let x = Self::gf_exp7(val, m) << (24 - 8 * s);
                self.sbox[s][i as usize] = Self::ice_perm32(x);
            }
        }
    }

    fn ice_key_sched_build(&mut self, kb: &mut [u16; 4], n: usize, keyrot: &[u32]) {
        for i in 0..8 {
            let kr = keyrot[i] as usize;
            let isk = &mut self.keysched[n + i];

            for j in 0..15 {
                let sk_idx = j % 3;

                for k in 0..4 {
                    let kb_idx = (kr + k) & 3;
                    let key_block = &mut kb[kb_idx];

                    let bit = *key_block & 1;
                    isk[sk_idx] = (isk[sk_idx] << 1) | bit as u32;
                    *key_block = (*key_block >> 1) | ((bit ^ 1) << 15);
                }
            }
        }
    }

    fn ice_key_set(&mut self, key: &[u8]) {
        let ice_keyrot = [0, 1, 2, 3, 2, 1, 3, 0, 1, 3, 2, 0, 3, 1, 0, 2];
        let mut kb = [0u16; 4];
        if self.key_rounds == 8 {
            for i in 0..4 {
                kb[3 - i] = u16::from_be_bytes([key[i * 2], key[i * 2 + 1]]);
            }
            self.ice_key_sched_build(&mut kb, 0, &ice_keyrot);
        } else {
            for i in 0..self.key_size {
                for j in 0..4 {
                    let base = i * 8 + j * 2;
                    kb[3 - j] = u16::from_be_bytes([key[base], key[base + 1]]);
                }
                self.ice_key_sched_build(&mut kb, i * 8, &ice_keyrot);
                self.ice_key_sched_build(&mut kb, self.key_rounds - 8 - i * 8, &ice_keyrot[8..]);
            }
        }
    }

    fn ice_f(&self, p: u32, sk: &[u32; 3]) -> u32 {
        let tl = ((p >> 16) & 0x3ff) | (((p >> 14) | (p << 18)) & 0xffc00);
        let tr = (p & 0x3ff) | ((p << 2) & 0xffc00);
        let al = sk[2] & (tl ^ tr);
        let ar = al ^ tr ^ sk[1];
        let al_final = al ^ tl ^ sk[0];
        self.sbox[0][(al_final >> 10) as usize]
            | self.sbox[1][(al_final & 0x3ff) as usize]
            | self.sbox[2][(ar >> 10) as usize]
            | self.sbox[3][(ar & 0x3ff) as usize]
    }

    pub fn encrypt_parallel(&self, data: &mut [u8]) {
        use rayon::prelude::*;

        if data.len() > 8192 {
            data.par_chunks_mut(8192).for_each(|chunk| {
                self.encrypt(chunk);
            });
        } else {
            self.encrypt(data);
        }
    }

    pub fn encrypt(&self, data: &mut [u8]) {
        for chunk in data.chunks_exact_mut(8) {
            let mut l = u32::from_be_bytes([chunk[0], chunk[1], chunk[2], chunk[3]]);
            let mut r = u32::from_be_bytes([chunk[4], chunk[5], chunk[6], chunk[7]]);

            for i in (0..self.key_rounds).step_by(2) {
                l ^= self.ice_f(r, &self.keysched[i]);
                r ^= self.ice_f(l, &self.keysched[i + 1]);
            }

            chunk[0..4].copy_from_slice(&r.to_be_bytes());
            chunk[4..8].copy_from_slice(&l.to_be_bytes());
        }
    }

    pub fn decrypt_parallel(&self, data: &mut [u8]) {
        use rayon::prelude::*;

        if data.len() > 8192 {
            data.par_chunks_mut(8192).for_each(|chunk| {
                self.decrypt(chunk);
            });
        } else {
            self.decrypt(data);
        }
    }

    pub fn decrypt(&self, data: &mut [u8]) {
        for chunk in data.chunks_exact_mut(8) {
            let mut l = u32::from_be_bytes([chunk[0], chunk[1], chunk[2], chunk[3]]);
            let mut r = u32::from_be_bytes([chunk[4], chunk[5], chunk[6], chunk[7]]);

            for i in (1..self.key_rounds).step_by(2).rev() {
                l ^= self.ice_f(r, &self.keysched[i]);
                r ^= self.ice_f(l, &self.keysched[i - 1]);
            }

            chunk[0..4].copy_from_slice(&r.to_be_bytes());
            chunk[4..8].copy_from_slice(&l.to_be_bytes());
        }
    }
}
