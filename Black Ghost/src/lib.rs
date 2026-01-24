use std::ffi::CString;
use rayon::iter::ParallelIterator;
pub mod models;
pub mod util;
pub mod processing;

use std::fs::File;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicUsize, Ordering};
use luadec::LuaDecompiler;
use memmap2::Mmap;
use rayon::prelude::IntoParallelRefIterator;
use safer_ffi::{ffi_export};
use safer_ffi::prelude::{char_p, repr_c};
use crate::models::bdo_index::BdoIndex;
use crate::models::paz_file::PazFile;
use crate::processing::bdo_decomp::BdoDecomp;
use crate::processing::ice_cipher::IceCipher;

const BDO_ICE_KEY: [u8; 8] = [0x51, 0xF3, 0x0F, 0x11, 0x04, 0x24, 0x6A, 0x00];
const BDO_TABLE_KEY: [u8; 8] = [0x6A, 0xD5, 0x8D, 0x21, 0x02, 0x8F, 0x9C, 0x00];

#[ffi_export]
pub fn load_bdo_index(
    path: char_p::Ref<'_>,
) -> Option<repr_c::Box<BdoIndex>> {
    let path_str = path.to_str();
    match BdoIndex::load(path_str, &BDO_ICE_KEY) {
        Ok(index) => Some(Box::new(index).into()),
        Err(err) => {
            eprintln!("Failed to load BDO index: {}", err);
            None
        }
    }
}

#[ffi_export]
pub fn free_bdo_index(
    _index: repr_c::Box<BdoIndex>,
) {
    drop(_index);
}

#[ffi_export]
pub fn get_file_content(
    paz_folder_path: char_p::Ref<'_>,
    file_info: PazFile,
    file_name: char_p::Ref<'_>,
) -> Option<repr_c::Vec<u8>> {
    let folder = paz_folder_path.to_str();
    let name_str = file_name.to_str();

    let paz_name = format!("pad{:05}.paz", file_info.paz_number);
    let full_path = PathBuf::from(folder).join(paz_name);

    let file = File::open(full_path).ok()?;
    let mmap = unsafe { Mmap::map(&file).ok()? };

    let start = file_info.offset as usize;
    let end = start + file_info.compressed_size as usize;
    if end > mmap.len() { return None; }

    let mut data = mmap[start..end].to_vec();

    // --- HEURISTIK: ENTSCHLÜSSELUNG ERFORDERLICH? ---
    let mut needs_decryption = true;

    // 1. Mathematischer Check: ICE benötigt zwingend 8-Byte Blöcke.
    // Wenn die Größe nicht durch 8 teilbar ist, ist die Datei garantiert unverschlüsselt.
    if data.len() % 8 != 0 {
        needs_decryption = false;
    }
    // 2. Struktur-Check: Falls sie durch 8 teilbar ist, schauen wir auf bekannte Header.
    else if data.len() >= 4 {
        // Wenn bereits "PABR" (BSS) am Anfang steht, nicht mehr entschlüsseln.
        if &data[0..4] == b"PABR" {
            needs_decryption = false;
        }
    }

    // Nur entschlüsseln, wenn beide Checks fehlschlagen.
    if needs_decryption {
        let ice = IceCipher::new(&BDO_ICE_KEY);
        if data.len() > 8192 {
            ice.decrypt_parallel(&mut data);
        } else {
            ice.decrypt(&mut data);
        }
    }

    // --- LAYER 2: DEKOMPRESSION ---
    // (Prüfung auf 0x6E / 0x6F Header nach der möglichen Entschlüsselung)
    let is_compressed_container = if data.len() > 9 && (data[0] == 0x6E || data[0] == 0x6F) {
        let header_original_size = u32::from_le_bytes([data[5], data[6], data[7], data[8]]);
        header_original_size == file_info.original_size
    } else {
        false
    };

    let final_data = if is_compressed_container {
        let mut decompressed_buffer = vec![0u8; file_info.original_size as usize];
        match BdoDecomp::decompress(&data, &mut decompressed_buffer) {
            Ok(actual_size) => {
                decompressed_buffer.truncate(actual_size as usize);
                decompressed_buffer
            }
            Err(_) => return None
        }
    } else {
        // Falls nicht komprimiert, auf die im Index angegebene Originalgröße stutzen.
        let limit = file_info.original_size as usize;
        if data.len() > limit {
            data.truncate(limit);
        }
        data
    };

    Some(repr_c::Vec::from(final_data))
}

#[ffi_export]
pub fn free_file_content(vec: repr_c::Vec<u8>) {
    drop(vec);
}

#[ffi_export]
pub fn decompile_lua(
    paz_folder_path: char_p::Ref<'_>,
    file_info: PazFile,
    file_name: char_p::Ref<'_>,
) -> Option<repr_c::Vec<u8>> {
    match get_file_content(paz_folder_path, file_info, file_name) {
        Some(raw_data) => {
            let decompiler = LuaDecompiler::new();
            match decompiler.decompile(&raw_data) {
                Ok(code) => Some(repr_c::Vec::from(code.into_bytes())),
                Err(e) => {
                    let err_msg = format!("-- Decompile Error: {:?}", e);
                    Some(repr_c::Vec::from(err_msg.into_bytes()))
                }
            }
        }
        None => {
            let err_msg = "-- IO Error: Could not read or decrypt PAZ file".to_string();
            Some(repr_c::Vec::from(err_msg.into_bytes()))
        }
    }
}

#[ffi_export]
pub fn extract_files_batch(
    save_folder: char_p::Ref<'_>,
    paz_folder_path: char_p::Ref<'_>,
    file_indices: repr_c::Vec<u32>,
    index: &BdoIndex,
    progress_callback: extern "C" fn(i32, i32),
) -> usize {
    let base_output = Path::new(save_folder.to_str());

    let total_files = file_indices.len();
    let counter = AtomicUsize::new(0);

    let count = file_indices.par_iter().filter(|&&idx| {
        let i = idx as usize;

        if i >= index.paz_files.len() { return false; }
        let file_info = index.paz_files[i];


        let folder_path = &index.metadata.folder_paths[file_info.folder_id as usize];
        let file_name = &index.metadata.file_names[file_info.file_id as usize];

        let relative_path = Path::new(folder_path.folder_name.trim_start_matches('/'))
            .join(file_name.trim_start_matches('/'));

        let full_output_path = base_output.join(relative_path);

        if let Some(parent) = full_output_path.parent() {
            let _ = std::fs::create_dir_all(parent);
        }

        let c_file_name = CString::new(file_name.to_string()).unwrap_or_default();

        let c_file_name_ref = char_p::Ref::from(c_file_name.as_c_str());

        let success = if let Some(data) = get_file_content(paz_folder_path, file_info, c_file_name_ref) {
            std::fs::write(full_output_path, &data[..]).is_ok()
        } else {
            false
        };

        let current = counter.fetch_add(1, Ordering::SeqCst) + 1;
        progress_callback(current as i32, total_files as i32);

        success
    }).count();

    std::mem::forget(file_indices);

    count
}