use safer_ffi::{derive_ReprC, String};
#[derive_ReprC]
#[repr(C)]
#[derive(Debug)]
pub struct FolderNameTuple {
    pub folder_name: String,
    pub folder_index: u32,
}