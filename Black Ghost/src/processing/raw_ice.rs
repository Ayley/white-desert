use rayon::prelude::*;
use std::slice;

#[repr(align(64))]
struct AlignedSBoxData([u8; 16384]);
static SBOX_BLOB: AlignedSBoxData =
    AlignedSBoxData(*include_bytes!(concat!(env!("OUT_DIR"), "/ice_sbox.bin")));

#[derive(Clone, Debug, Copy)]
#[repr(C, align(16))]
pub struct RawIceSubkey {
    val: [u32; 3],
}

#[derive(Clone, Debug)]
pub struct RawIceKeyStruct {
    size: usize,
    rounds: usize,
    pub keysched: Vec<RawIceSubkey>,
}

#[derive(Clone, Debug)]
#[repr(align(64))]
pub struct RawIce {
    pub key: RawIceKeyStruct,
    sbox_flat: [u32; 4096],
}

const KEYROT: [i32; 16] = [0, 1, 2, 3, 2, 1, 3, 0, 1, 3, 2, 0, 3, 1, 0, 2];

macro_rules! ice_f_raw {
    ($p:expr, $sk:expr, $sbox_ptr:expr) => {{
        let p = $p;
        let tr = (p & 0x3ff) | ((p << 2) & 0xffc00);
        let tl = ((p >> 16) & 0x3ff) | (p.rotate_left(18) & 0xffc00);

        let salt = $sk.val[2] & (tl ^ tr);
        let al = salt ^ tl ^ $sk.val[0];
        let ar = salt ^ tr ^ $sk.val[1];

        unsafe {
            *$sbox_ptr.add((al >> 10) as usize & 0x3ff)
                ^ *$sbox_ptr.add(1024 + (al as usize & 0x3ff))
                ^ *$sbox_ptr.add(2048 + ((ar >> 10) as usize & 0x3ff))
                ^ *$sbox_ptr.add(3072 + (ar as usize & 0x3ff))
        }
    }};
}

macro_rules! ice_f_opt {
    ($p:expr, $sk:expr, $sb0:expr, $sb1:expr, $sb2:expr, $sb3:expr) => {{
        let p = $p;
        let tr = (p & 0x3ff) | ((p << 2) & 0xffc00);
        let tl = ((p >> 16) & 0x3ff) | (p.rotate_left(18) & 0xffc00);

        let salt = $sk.val[2] & (tl ^ tr);
        let al = salt ^ tl ^ $sk.val[0];
        let ar = salt ^ tr ^ $sk.val[1];

        unsafe {
            *$sb0.add((al >> 10) as usize & 0x3ff)
                ^ *$sb1.add(al as usize & 0x3ff)
                ^ *$sb2.add((ar >> 10) as usize & 0x3ff)
                ^ *$sb3.add(ar as usize & 0x3ff)
        }
    }};
}

macro_rules! process_4_blocks {
    ($l:expr, $r:expr, $offset:expr, $sk:expr, $sb0:expr, $sb1:expr, $sb2:expr, $sb3:expr) => {
        let f0 = ice_f_opt!($r[$offset + 0], $sk, $sb0, $sb1, $sb2, $sb3);
        let f1 = ice_f_opt!($r[$offset + 1], $sk, $sb0, $sb1, $sb2, $sb3);
        let f2 = ice_f_opt!($r[$offset + 2], $sk, $sb0, $sb1, $sb2, $sb3);
        let f3 = ice_f_opt!($r[$offset + 3], $sk, $sb0, $sb1, $sb2, $sb3);

        $l[$offset + 0] ^= f0;
        $l[$offset + 1] ^= f1;
        $l[$offset + 2] ^= f2;
        $l[$offset + 3] ^= f3;
    };
}

#[allow(unused)]
trait UncheckedSplit {
    unsafe fn split_at_mut_unchecked(&mut self, mid: usize) -> (&mut [u8], &mut [u8]);
}

impl UncheckedSplit for [u8] {
    #[inline(always)]
    unsafe fn split_at_mut_unchecked(&mut self, mid: usize) -> (&mut [u8], &mut [u8]) {
        let len = self.len();
        unsafe {
            let ptr = self.as_mut_ptr();
            (
                slice::from_raw_parts_mut(ptr, mid),
                slice::from_raw_parts_mut(ptr.add(mid), len - mid),
            )
        }
    }
}

impl RawIce {
    pub fn new(level: usize, key: &[u8]) -> Self {
        let mut ik = RawIce {
            key: RawIceKeyStruct {
                size: 0,
                rounds: 0,
                keysched: Vec::new(),
            },
            sbox_flat: [0; 4096],
        };

        unsafe {
            let src_ptr = SBOX_BLOB.0.as_ptr() as *const u32;
            std::ptr::copy_nonoverlapping(src_ptr, ik.sbox_flat.as_mut_ptr(), 4096);
        }

        if level < 1 {
            ik.key.size = 1;
            ik.key.rounds = 8;
            assert_eq!(key.len(), 8);
        } else {
            ik.key.size = level;
            ik.key.rounds = level * 16;
            assert_eq!(key.len(), level * 8);
        }

        ik.key.keysched = vec![RawIceSubkey { val: [0; 3] }; ik.key.rounds];
        ik.key_set(key);
        ik
    }

    #[inline(always)]
    fn crypt_64_core<const ENCRYPT: bool>(&self, chunk: &mut [u8], sbox_ptr: *const u32) {
        let mut l = [0u32; 8];
        let mut r = [0u32; 8];

        let (sb0, sb1, sb2, sb3) = unsafe {
            (
                sbox_ptr,
                sbox_ptr.add(1024),
                sbox_ptr.add(2048),
                sbox_ptr.add(3072),
            )
        };

        unsafe {
            let ptr = chunk.as_ptr() as *const u32;
            for i in 0..8 {
                l[i] = u32::from_be(*ptr.add(i * 2));
                r[i] = u32::from_be(*ptr.add(i * 2 + 1));
            }
        }

        if ENCRYPT {
            for sk_pair in self.key.keysched.chunks_exact(2) {
                process_4_blocks!(l, r, 0, &sk_pair[0], sb0, sb1, sb2, sb3);
                process_4_blocks!(l, r, 4, &sk_pair[0], sb0, sb1, sb2, sb3);

                process_4_blocks!(r, l, 0, &sk_pair[1], sb0, sb1, sb2, sb3);
                process_4_blocks!(r, l, 4, &sk_pair[1], sb0, sb1, sb2, sb3);
            }
        } else {
            for sk_pair in self.key.keysched.rchunks_exact(2) {
                process_4_blocks!(l, r, 0, &sk_pair[1], sb0, sb1, sb2, sb3);
                process_4_blocks!(l, r, 4, &sk_pair[1], sb0, sb1, sb2, sb3);

                process_4_blocks!(r, l, 0, &sk_pair[0], sb0, sb1, sb2, sb3);
                process_4_blocks!(r, l, 4, &sk_pair[0], sb0, sb1, sb2, sb3);
            }
        }

        unsafe {
            let ptr = chunk.as_mut_ptr() as *mut u32;
            for i in 0..8 {
                *ptr.add(i * 2) = r[i].to_be();
                *ptr.add(i * 2 + 1) = l[i].to_be();
            }
        }
    }

    #[inline(always)]
    fn crypt_fixed_batch<const N: usize, const ENCRYPT: bool>(&self, chunk: &mut [u8]) {
        debug_assert!(chunk.len() == N);
        let sbox_ptr = self.sbox_flat.as_ptr();
        unsafe {
            let ptr = chunk.as_mut_ptr();
            for i in (0..N).step_by(64) {
                let sub_chunk = slice::from_raw_parts_mut(ptr.add(i), 64);
                self.crypt_64_core::<ENCRYPT>(sub_chunk, sbox_ptr);
            }
        }
    }

    #[inline(always)]
    pub fn decrypt128(&self, chunk: &mut [u8]) {
        self.crypt_fixed_batch::<128, false>(chunk);
    }

    #[inline(always)]
    pub fn encrypt128(&self, chunk: &mut [u8]) {
        self.crypt_fixed_batch::<128, true>(chunk);
    }

    #[inline(always)]
    pub fn decrypt256(&self, chunk: &mut [u8]) {
        self.crypt_fixed_batch::<256, false>(chunk);
    }

    #[inline(always)]
    pub fn encrypt256(&self, chunk: &mut [u8]) {
        self.crypt_fixed_batch::<256, true>(chunk);
    }

    #[inline(always)]
    fn crypt_tail_cascade<const ENCRYPT: bool>(&self, rem: &mut [u8]) {
        let sbox_ptr = self.sbox_flat.as_ptr();
        let mut chunk_iter = rem;

        if chunk_iter.len() >= 128 {
            let mut chunks = chunk_iter.chunks_exact_mut(128);
            for chunk in &mut chunks {
                self.crypt_fixed_batch::<128, ENCRYPT>(chunk);
            }
            chunk_iter = chunks.into_remainder();
        }

        if chunk_iter.len() >= 64 {
            let mut chunks = chunk_iter.chunks_exact_mut(64);
            for chunk in &mut chunks {
                self.crypt_64_core::<ENCRYPT>(chunk, sbox_ptr);
            }
            chunk_iter = chunks.into_remainder();
        }

        if !chunk_iter.is_empty() {
            if ENCRYPT {
                self.encrypt_scalar_fallback(chunk_iter, sbox_ptr);
            } else {
                self.decrypt_scalar_fallback(chunk_iter, sbox_ptr);
            }
        }
    }

    fn crypt_par<const ENCRYPT: bool>(&self, data: &mut [u8]) {
        let len = data.len();
        assert_eq!(len % 8, 0, "Data length must be a multiple of 8");

        let use_huge_batch = len >= 16 * 1024;

        if use_huge_batch {
            const BATCH: usize = 256;
            let split_len = len - (len % BATCH);
            let (main, tail) = data.split_at_mut(split_len);

            main.par_chunks_exact_mut(BATCH).for_each(|chunk| {
                self.crypt_fixed_batch::<BATCH, ENCRYPT>(chunk);
            });
            if !tail.is_empty() {
                self.crypt_tail_cascade::<ENCRYPT>(tail);
            }
        } else {
            const BATCH: usize = 128;
            let split_len = len - (len % BATCH);
            let (main, tail) = data.split_at_mut(split_len);

            main.par_chunks_exact_mut(BATCH).for_each(|chunk| {
                self.crypt_fixed_batch::<BATCH, ENCRYPT>(chunk);
            });
            if !tail.is_empty() {
                self.crypt_tail_cascade::<ENCRYPT>(tail);
            }
        }
    }

    pub fn decrypt_par(&self, data: &mut [u8]) {
        self.crypt_par::<false>(data);
    }

    pub fn encrypt_par(&self, data: &mut [u8]) {
        self.crypt_par::<true>(data);
    }

    pub fn decrypt(&self, data: &mut [u8]) {
        self.crypt_serial::<false>(data);
    }

    pub fn encrypt(&self, data: &mut [u8]) {
        self.crypt_serial::<true>(data);
    }

    fn crypt_serial<const ENCRYPT: bool>(&self, data: &mut [u8]) {
        let mut rem = data;
        unsafe {
            while rem.len() >= 256 {
                let (chunk, rest) = rem.split_at_mut_unchecked(256);
                self.crypt_fixed_batch::<256, ENCRYPT>(chunk);
                rem = rest;
            }
        }
        self.crypt_tail_cascade::<ENCRYPT>(rem);
    }

    #[inline(always)]
    fn decrypt_scalar_fallback(&self, data: &mut [u8], sbox_ptr: *const u32) {
        for chunk in data.chunks_exact_mut(8) {
            let mut l = u32::from_be_bytes(chunk[0..4].try_into().unwrap());
            let mut r = u32::from_be_bytes(chunk[4..8].try_into().unwrap());
            for pair in self.key.keysched.rchunks_exact(2) {
                l ^= ice_f_raw!(r, &pair[1], sbox_ptr);
                r ^= ice_f_raw!(l, &pair[0], sbox_ptr);
            }
            chunk[0..4].copy_from_slice(&r.to_be_bytes());
            chunk[4..8].copy_from_slice(&l.to_be_bytes());
        }
    }

    #[inline(always)]
    fn encrypt_scalar_fallback(&self, data: &mut [u8], sbox_ptr: *const u32) {
        for chunk in data.chunks_exact_mut(8) {
            let mut l = u32::from_be_bytes(chunk[0..4].try_into().unwrap());
            let mut r = u32::from_be_bytes(chunk[4..8].try_into().unwrap());
            for pair in self.key.keysched.chunks_exact(2) {
                l ^= ice_f_raw!(r, &pair[0], sbox_ptr);
                r ^= ice_f_raw!(l, &pair[1], sbox_ptr);
            }
            chunk[0..4].copy_from_slice(&r.to_be_bytes());
            chunk[4..8].copy_from_slice(&l.to_be_bytes());
        }
    }

    fn key_sched_build(&mut self, kb: &mut [u16; 4], n: i32, keyrot: &[i32]) {
        for (i, &kr) in keyrot.iter().enumerate().take(8) {
            let isk = &mut self.key.keysched[n as usize + i];
            isk.val = [0; 3];
            for _ in 0..5 {
                for j in 0..3 {
                    let curr_sk = &mut isk.val[j];
                    for k in 0..4 {
                        let idx = (kr as usize + k) & 3;
                        let bit = kb[idx] & 1;
                        *curr_sk = (*curr_sk << 1) | (bit as u32);
                        kb[idx] = (kb[idx] >> 1) | ((bit ^ 1) << 15);
                    }
                }
            }
        }
    }

    pub fn key_set(&mut self, key: &[u8]) {
        if self.key.rounds == 8 {
            let mut kb = [0u16; 4];
            for i in 0..4 {
                kb[3 - i] = (key[i * 2] as u16) << 8 | key[i * 2 + 1] as u16;
            }
            self.key_sched_build(&mut kb, 0, &KEYROT);
            return;
        }
        for i in 0..self.key.size {
            let mut kb = [0u16; 4];
            for j in 0..4 {
                kb[3 - j] =
                    (key[i * 8 + j * 2] as u16) << 8 | key[i as usize * 8 + j * 2 + 1] as u16;
            }
            self.key_sched_build(&mut kb, (i * 8).try_into().unwrap(), &KEYROT);
            self.key_sched_build(
                &mut kb,
                (self.key.rounds - 8 - i * 8).try_into().unwrap(),
                &KEYROT[8..16],
            );
        }
    }
}
