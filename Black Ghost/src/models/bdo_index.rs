use std::fs::File;
use std::ptr;
use memmap2::Mmap;
use rayon::prelude::*;
use safer_ffi::derive_ReprC;
use crate::processing::raw_ice::RawIce;
use super::pad_meta::PadMeta;
use super::paz_file::PazFile;

#[derive_ReprC]
#[repr(C)]
pub struct BdoIndex {
    pub metadata: PadMeta,
    pub paz_files: safer_ffi::Vec<PazFile>,
}

impl BdoIndex {
    pub fn load(path: &str, key: &[u8]) -> Result<Self, Box<dyn std::error::Error>> {
        let file = File::open(path)?;
        let data = unsafe { Mmap::map(&file)? };

        let ice = RawIce::new(0, key);

        let mut cursor: usize = 0;

        // --- Read header ---
        let version = u32::from_le_bytes(data[cursor..cursor + 4].try_into()?);
        let paz_count = u32::from_le_bytes(data[cursor + 4..cursor + 8].try_into()?);
        cursor += 8 + (paz_count as usize * 12);

        // --- Define paz block ---
        let file_count = u32::from_le_bytes(data[cursor..cursor + 4].try_into()?) as usize;
        cursor += 4;

        let raw_files_start = cursor;
        let raw_files_len = file_count * 28;
        let raw_files_slice = &data[raw_files_start..raw_files_start + raw_files_len];
        cursor += raw_files_len;

        // --- Read filenames/foldernames ---
        let folder_len = u32::from_le_bytes(data[cursor..cursor + 4].try_into()?) as usize;
        let folder_raw_slice = &data[cursor + 4..cursor + 4 + folder_len];
        cursor += 4 + folder_len;

        let file_len = u32::from_le_bytes(data[cursor..cursor + 4].try_into()?) as usize;
        let file_raw_slice = &data[cursor + 4..cursor + 4 + file_len];

        // --- Decrypt ---
        let (mut folder_raw, mut file_raw) = rayon::join(
            || folder_raw_slice.to_vec(),
            || file_raw_slice.to_vec()
        );

        rayon::join(
            || ice.decrypt_par(&mut folder_raw),
            || ice.decrypt_par(&mut file_raw),
        );

        // --- Parse paz files ---
        let (metadata, id_map) = PadMeta::parse_sorted(
            version,
            paz_count,
            &folder_raw,
            &file_raw,
            file_count
        );

        // --- Create paz files from raw memory ---
        let mut paz_files: Vec<PazFile> = Vec::with_capacity(file_count);

        unsafe {
            ptr::copy_nonoverlapping(
                raw_files_slice.as_ptr() as *const PazFile,
                paz_files.as_mut_ptr(),
                file_count
            );
            paz_files.set_len(file_count);
        }

        // --- Correct folder ids ---
        paz_files.par_chunks_mut(4096).for_each(|chunk| {
            for file in chunk {
                unsafe {
                    file.folder_id = *id_map.get_unchecked(file.folder_id as usize);
                }
            }
        });

        Ok(BdoIndex {
            metadata,
            paz_files: paz_files.into(),
        })
    }
}