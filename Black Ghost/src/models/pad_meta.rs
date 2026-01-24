use crate::util::folder_name_tuple::FolderNameTuple;
use safer_ffi::__::repr_c;
use safer_ffi::derive_ReprC;
use std::ops::Deref;

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
        let (sorted_folders, id_map) = Self::parse_folders_sorted(folder_raw);
        let file_names = Self::parse_files(file_raw, file_count);

        let meta = Self {
            version,
            paz_file_count: paz_count,
            folder_paths: sorted_folders.into(),
            file_names: file_names.into(),
        };

        (meta, id_map)
    }
    pub fn parse(
        version: u32,
        paz_count: u32,
        folder_raw: &[u8],
        file_raw: &[u8],
        file_count: usize,
    ) -> Self {
        let folders = Self::parse_folders(folder_raw);
        let file_names = Self::parse_files(file_raw, file_count);

        let meta = Self {
            version,
            paz_file_count: paz_count,
            folder_paths: folders.into(),
            file_names: file_names.into(),
        };

        meta
    }

    fn parse_folders_sorted(data: &[u8]) -> (Vec<FolderNameTuple>, Vec<u32>) {
        let mut folders = Self::parse_folders(data);

        let mut id_map = vec![0u32; folders.len()];

        folders.as_mut_slice().sort_by(|a, b| {
            let name_a: &str = a.folder_name.deref();
            let name_b: &str = b.folder_name.deref();
            name_a.cmp(name_b)
        });

        for (new_idx, folder) in folders.iter_mut().enumerate() {
            id_map[folder.folder_index as usize] = new_idx as u32;
            folder.folder_index = new_idx as u32;
        }

        (folders, id_map)
    }

    fn parse_folders(data: &[u8]) -> Vec<FolderNameTuple> {
        let mut folders: Vec<FolderNameTuple> = Vec::with_capacity(8000);

        let mut i = 8;

        while i < data.len() {
            let start = i;
            while i < data.len() && data[i] != 0 {
                i += 1;
            }

            if i > start {
                let name = String::from_utf8_lossy(&data[start..i]).to_string();
                let id = folders.len() as u32;
                folders.push(FolderNameTuple {
                    folder_name: name.into(),
                    folder_index: id,
                });
            }
            i += 9;
        }

        folders
    }

    fn parse_files(data: &[u8], count: usize) -> Vec<safer_ffi::String> {
        let mut names = Vec::with_capacity(count);
        let mut i = 0;
        while i < data.len() {
            let start = i;
            while i < data.len() && data[i] != 0 {
                i += 1;
            }
            if i > start {
                names.push(String::from_utf8_lossy(&data[start..i]).to_string().into());
            }
            i += 1;
        }
        names
    }
}
