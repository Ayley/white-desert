use safer_ffi::{derive_ReprC};

#[derive_ReprC]
#[repr(C)]
#[derive(Debug, Clone)]
pub struct FolderNameTuple {
    pub folder_name: safer_ffi::String,
    pub folder_index: u32,
}