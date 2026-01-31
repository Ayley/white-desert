extern crate core;

use rayon::iter::ParallelIterator;
pub mod models;
pub mod util;
pub mod processing;

use std::fs::File;
use std::io::Cursor;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicUsize, Ordering};
use image_dds::ddsfile::Dds;
use image_dds::image::{DynamicImage, ImageFormat};
use image_dds::image_from_dds;
use luadec::LuaDecompiler;
use memmap2::Mmap;
use mimalloc::MiMalloc;
use rayon::prelude::IntoParallelRefIterator;
use safer_ffi::{ffi_export};
use safer_ffi::prelude::{char_p, repr_c};
use crate::models::bdo_index::BdoIndex;
use crate::models::paz_file::PazFile;
use crate::processing::bdo_decomp::BdoDecomp;
use crate::processing::raw_ice::RawIce;

#[global_allocator]
static GLOBAL: MiMalloc = MiMalloc;

#[repr(i32)]
pub enum ExtractType {
    RawDecrypted = 0,
    Converted = 1,
}

const BDO_ICE_KEY: [u8; 8] = [0x51, 0xF3, 0x0F, 0x11, 0x04, 0x24, 0x6A, 0x00];

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
) -> Option<repr_c::Vec<u8>> {
    let folder = paz_folder_path.to_str();

    let paz_name = format!("pad{:05}.paz", file_info.paz_number);
    let full_path = PathBuf::from(folder).join(paz_name);

    let file = File::open(full_path).ok()?;
    let mmap = unsafe { Mmap::map(&file).ok()? };

    let start = file_info.offset as usize;
    let end = start + file_info.compressed_size as usize;
    if end > mmap.len() { return None; }

    let mut data = mmap[start..end].to_vec();

    let mut needs_decryption = true;

    if data.len() % 8 != 0 {
        needs_decryption = false;
    }
    else if data.len() >= 4 {
        if &data[0..4] == b"PABR" {
            needs_decryption = false;
        }
    }

    if needs_decryption {
        let ice = RawIce::new(0, &BDO_ICE_KEY);
        if data.len() > 8192 {
            ice.decrypt_par(&mut data);
        } else {
            ice.decrypt(&mut data);
        }
    }

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
) -> Option<repr_c::Vec<u8>> {
    match get_file_content(paz_folder_path, file_info) {
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
    extract_type: i32,
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
        let mut full_output_path = base_output.join(relative_path);

        let success = if let Some(data) = get_file_content(paz_folder_path, file_info) {
            let mut final_data = data;

            if extract_type == 1 {
                println!("Parse");
                let ext = file_name.to_lowercase();

                if ext.ends_with(".dds") || ext.ends_with(".dds1") {
                    match convert_dds_to_png_memory(&final_data) {
                        Ok(png_data) => {
                            full_output_path.set_extension("png");
                            final_data = png_data.into();
                        }
                        Err(e) => {
                            println!("-- Decode Error: {:?}", e);
                        }
                    }
                }

                if ext.ends_with(".luac") {
                    let decompiler = LuaDecompiler::new();
                    // Wir dekompilieren und überschreiben final_data mit dem Ergebnis
                    let result_code = match decompiler.decompile(&*final_data) {
                        Ok(code) => {
                            full_output_path.set_extension("lua");
                            code.into_bytes()
                        },
                        Err(e) => {
                            format!("-- Decompile Error: {:?}", e).into_bytes()
                        }
                    };

                    final_data = result_code.into();
                }
            }

            if let Some(parent) = full_output_path.parent() {
                let _ = std::fs::create_dir_all(parent);
            }

            std::fs::write(&full_output_path, &final_data[..]).is_ok()
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

fn convert_dds_to_png_memory(dds_buffer: &[u8]) -> Result<Vec<u8>, Box<dyn std::error::Error>> {
    let mut reader = Cursor::new(dds_buffer);
    let dds = Dds::read(&mut reader)?;

    let img = image_from_dds(&dds, 0)?;

    let mut png_buffer = Vec::new();
    let mut cursor = Cursor::new(&mut png_buffer);

    let dynamic_img = DynamicImage::ImageRgba8(img);
    dynamic_img.write_to(&mut cursor, ImageFormat::Png)?;

    Ok(png_buffer)
}