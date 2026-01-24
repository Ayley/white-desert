use std::fs::File;

use memmap2::Mmap;
use rayon::iter::ParallelIterator;
use rayon::prelude::ParallelSlice;
use safer_ffi::derive_ReprC;
use safer_ffi::prelude::repr_c;

use super::pad_meta::PadMeta;
use super::paz_file::PazFile;
use crate::processing::ice_cipher::IceCipher;

#[derive_ReprC]
#[repr(C)]
pub struct BdoIndex {
    pub metadata: PadMeta,
    pub paz_files: repr_c::Vec<PazFile>,
}

impl BdoIndex {
    pub fn load(path: &str, key: &[u8]) -> Result<Self, Box<dyn std::error::Error>> {
        let file = File::open(path)?;
        let data = unsafe { Mmap::map(&file)? };
        let ice = IceCipher::new(key);
        let mut cursor: usize = 0;

        // 1. Header & Paz Count
        let version = u32::from_le_bytes(data[cursor..cursor + 4].try_into()?);
        let paz_count = u32::from_le_bytes(data[cursor + 4..cursor + 8].try_into()?);
        cursor += 8 + (paz_count as usize * 12);

        // 2. Read raw PAZ file data (initial read without index correction)
        let file_count = u32::from_le_bytes(data[cursor..cursor + 4].try_into()?) as usize;
        cursor += 4;
        let raw_file_data = &data[cursor..cursor + (file_count * 28)];
        cursor += file_count * 28;

        // 3. String blocks
        let folder_len = u32::from_le_bytes(data[cursor..cursor + 4].try_into()?) as usize;
        let folder_raw_slice = &data[cursor + 4..cursor + 4 + folder_len];
        cursor += 4 + folder_len;

        let file_len = u32::from_le_bytes(data[cursor..cursor + 4].try_into()?) as usize;
        let file_raw_slice = &data[cursor + 4..cursor + 4 + file_len];

        // Decrypt string blocks
        let mut folder_raw = folder_raw_slice.to_vec();
        let mut file_raw = file_raw_slice.to_vec();

        rayon::join(
            || ice.decrypt(&mut folder_raw),
            || ice.decrypt(&mut file_raw),
        );

        let (metadata, id_map) = PadMeta::parse_sorted(version, paz_count, &folder_raw, &file_raw, file_count);

        // 5. Parse PAZ files and correct folder IDs (Parallel)
        // Remap folder IDs to match the alphabetically sorted folder list
        let paz_files: Vec<PazFile> = raw_file_data
            .par_chunks_exact(28)
            .map(|chunk| {
                let mut file = PazFile::from_binary(chunk);

                file.folder_id = id_map[file.folder_id as usize];

                file
            })
            .collect();

        Ok(BdoIndex {
            metadata,
            paz_files: paz_files.into(),
        })
    }
}
