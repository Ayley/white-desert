use crate::util::folder_name_tuple::FolderNameTuple;
use rayon::prelude::*;
use safer_ffi::__::repr_c;
use safer_ffi::derive_ReprC;

#[derive_ReprC]
#[repr(C)]
pub struct PadMeta {
    pub version: u32,
    pub paz_file_count: u32,
    pub file_names: repr_c::Vec<safer_ffi::String>,
    pub folder_paths: repr_c::Vec<FolderNameTuple>,
}

impl PadMeta {
    pub fn parse_sorted(
        version: u32,
        paz_count: u32,
        folder_raw: &[u8],
        file_raw: &[u8],
        file_count: usize,
    ) -> (Self, Vec<u32>) {
        let (file_names, (sorted_folders, id_map)) = rayon::join(
            || Self::parse_files_chunked(file_raw, file_count),
            || Self::parse_folders_sorted(folder_raw)
        );

        let meta = Self {
            version,
            paz_file_count: paz_count,
            folder_paths: sorted_folders.into(),
            file_names: file_names.into(),
        };

        (meta, id_map)
    }

    fn parse_folders_sorted(data: &[u8]) -> (Vec<FolderNameTuple>, Vec<u32>) {
        let mut folders = Self::parse_folders_fast_seq(data);

        folders.par_sort_unstable_by(|a, b| a.folder_name.cmp(&b.folder_name));

        let mut id_map = vec![0u32; folders.len()];
        for (new_idx, folder) in folders.iter_mut().enumerate() {
            let old_id = folder.folder_index as usize;
            if old_id < id_map.len() {
                id_map[old_id] = new_idx as u32;
            }
            folder.folder_index = new_idx as u32;
        }

        (folders, id_map)
    }

    fn parse_folders_fast_seq(data: &[u8]) -> Vec<FolderNameTuple> {
        let mut folders = Vec::with_capacity(8000);
        let mut cursor = 0;
        let len = data.len();
        let mut id_counter = 0;

        let limit = len.saturating_sub(8);

        while cursor < limit {
            cursor += 8;
            let start = cursor;

            match memchr::memchr(0, &data[cursor..]) {
                Some(offset) => {
                    let end = cursor + offset;
                    let name = unsafe {
                        String::from_utf8_unchecked(data[start..end].to_vec())
                    };

                    folders.push(FolderNameTuple {
                        folder_name: name.into(),
                        folder_index: id_counter,
                    });

                    id_counter += 1;
                    cursor = end + 1;
                }
                None => break,
            }
        }
        folders
    }

    fn parse_files_chunked(data: &[u8], count_hint: usize) -> Vec<safer_ffi::String> {
        if data.is_empty() { return Vec::new(); }

        let num_threads = rayon::current_num_threads();
        if count_hint < 1000 || num_threads == 1 {
            return Self::parse_files_seq_inner(data);
        }

        let chunk_size = data.len() / num_threads;
        let mut split_indices = Vec::with_capacity(num_threads + 1);
        split_indices.push(0);

        for i in 1..num_threads {
            let mut idx = i * chunk_size;
            if let Some(offset) = memchr::memchr(0, &data[idx..]) {
                idx += offset + 1;
                if idx < data.len() {
                    split_indices.push(idx);
                }
            }
        }
        split_indices.push(data.len());

        split_indices.par_windows(2).flat_map(|window| {
            let start = window[0];
            let end = window[1];
            let slice = &data[start..end];

            Self::parse_files_seq_inner(slice)
        }).collect()
    }

    fn parse_files_seq_inner(data: &[u8]) -> Vec<safer_ffi::String> {
        let mut names = Vec::with_capacity(data.len() / 20);
        let mut cursor = 0;

        while cursor < data.len() {
            match memchr::memchr(0, &data[cursor..]) {
                Some(len) => {
                    let end = cursor + len;
                    let s = unsafe {
                        String::from_utf8_unchecked(data[cursor..end].to_vec())
                    };
                    names.push(s.into());
                    cursor = end + 1;
                }
                None => break,
            }
        }
        names
    }
}