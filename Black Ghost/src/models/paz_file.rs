use safer_ffi::derive_ReprC;

#[derive(Clone, Copy)]
#[derive_ReprC]
#[repr(C)]
pub struct PazFile {
    pub hash: u32,
    pub folder_id: u32,
    pub file_id: u32,
    pub paz_number: u32,
    pub offset: u32,
    pub compressed_size: u32,
    pub original_size: u32,
}

impl PazFile {
    #[inline(always)]
    pub fn from_binary(chunk: &[u8]) -> Self {
        Self {
            hash: u32::from_le_bytes(chunk[0..4].try_into().unwrap()),
            folder_id: u32::from_le_bytes(chunk[4..8].try_into().unwrap()),
            file_id: u32::from_le_bytes(chunk[8..12].try_into().unwrap()),
            paz_number: u32::from_le_bytes(chunk[12..16].try_into().unwrap()),
            offset: u32::from_le_bytes(chunk[16..20].try_into().unwrap()),
            compressed_size: u32::from_le_bytes(chunk[20..24].try_into().unwrap()),
            original_size: u32::from_le_bytes(chunk[24..28].try_into().unwrap()),
        }
    }
}