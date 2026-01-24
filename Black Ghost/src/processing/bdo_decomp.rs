#[derive(Debug)]
pub enum BdoDecompError {
    TruncatedData,
    CorruptedData,
    OutputBufferTooSmall,
}

pub struct BdoDecomp;

impl BdoDecomp {
    pub fn decompress(input: &[u8], output: &mut [u8]) -> Result<u32, BdoDecompError> {
        if input.is_empty() {
            return Ok(0);
        }

        let flags = input[0];
        let (target_len, comp_len, header_size) = Self::parse_file_header(input)?;

        if input.len() < comp_len {
            return Err(BdoDecompError::TruncatedData);
        }
        let input = &input[..comp_len];

        if (flags & 0x01) == 0 {
            let copy_len = target_len as usize;
            if output.len() < copy_len || input.len() < header_size + copy_len {
                return Err(BdoDecompError::OutputBufferTooSmall);
            }
            output[..copy_len].copy_from_slice(&input[header_size..header_size + copy_len]);
            return Ok(target_len);
        }

        Self::unpack_core(input, output, target_len, header_size)
    }

    fn unpack_core(
        input: &[u8],
        output: &mut [u8],
        target_len: u32,
        start_idx: usize,
    ) -> Result<u32, BdoDecompError> {
        let mut in_idx = start_idx;
        let mut out_idx = 0usize;
        let mut group_header = 1u32;
        let target = target_len as usize;
        let input_len = input.len();

        while out_idx < target && in_idx < input_len {
            if group_header == 1 {
                if in_idx + 4 > input_len {
                    break;
                }
                group_header = u32::from_le_bytes([
                    input[in_idx],
                    input[in_idx + 1],
                    input[in_idx + 2],
                    input[in_idx + 3],
                ]);
                in_idx += 4;
            }

            if (group_header & 1) != 0 {
                // MATCH / COPY FALL
                if in_idx + 4 > input_len {
                    break;
                }
                let header = u32::from_le_bytes([
                    input[in_idx],
                    input[in_idx + 1],
                    input[in_idx + 2],
                    input[in_idx + 3],
                ]);
                let (dist, len, step) = Self::parse_block_header(header);

                in_idx += step;
                Self::copy_match(output, out_idx, dist, len)?;
                out_idx += len;
                group_header >>= 1;
            } else {
                let lit_len = Self::get_literal_length(group_header);
                if out_idx + 4 > output.len() || in_idx + 4 > input_len {
                    break;
                }

                unsafe {
                    let src = input.as_ptr().add(in_idx);
                    let dst = output.as_mut_ptr().add(out_idx);
                    std::ptr::copy_nonoverlapping(src, dst, 4);
                }

                out_idx += lit_len;
                in_idx += lit_len;
                group_header >>= lit_len;
            }
        }

        Self::process_tail(input, output, out_idx, in_idx, target, group_header)
    }

    #[inline(always)]
    fn copy_match(
        output: &mut [u8],
        out_idx: usize,
        dist: usize,
        len: usize,
    ) -> Result<(), BdoDecompError> {
        if out_idx < dist || out_idx + len > output.len() {
            return Err(BdoDecompError::CorruptedData);
        }

        unsafe {
            let ptr = output.as_mut_ptr().add(out_idx);
            let mut src = ptr.offset(-(dist as isize));
            let mut dst = ptr;

            for _ in 0..len {
                *dst = *src;
                dst = dst.add(1);
                src = src.add(1);
            }
        }
        Ok(())
    }

    #[inline(always)]
    fn get_literal_length(group_header: u32) -> usize {
        static TABLE: [u8; 16] = [4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0];
        TABLE[(group_header & 0xF) as usize] as usize
    }

    fn process_tail(
        input: &[u8],
        output: &mut [u8],
        mut out_idx: usize,
        mut in_idx: usize,
        target: usize,
        mut group: u32,
    ) -> Result<u32, BdoDecompError> {
        let input_len = input.len();

        while out_idx < target {
            if group == 1 {
                if in_idx + 4 <= input_len {
                    in_idx += 4;
                }
                group = 0x80000000;
            }

            if in_idx >= input_len {
                break;
            }

            output[out_idx] = input[in_idx];

            out_idx += 1;
            in_idx += 1;

            group >>= 1;

            if out_idx >= target {
                break;
            }
        }

        Ok(out_idx as u32)
    }

    #[inline(always)]
    fn parse_block_header(h: u32) -> (usize, usize, usize) {
        if (h & 0x03) == 0x03 {
            if (h & 0x7F) == 3 {
                ((h >> 15) as usize, ((h >> 7) & 0xFF) as usize + 3, 4)
            } else {
                (
                    ((h >> 7) & 0x1FFFF) as usize,
                    ((h >> 2) & 0x1F) as usize + 2,
                    3,
                )
            }
        } else if (h & 0x03) == 0x02 {
            ((h as u16 >> 6) as usize, ((h >> 2) & 0xF) as usize + 3, 2)
        } else if (h & 0x03) == 0x01 {
            ((h as u16 >> 2) as usize, 3, 2)
        } else {
            ((h as u8 >> 2) as usize, 3, 1)
        }
    }

    #[inline(always)]
    fn parse_file_header(input: &[u8]) -> Result<(u32, usize, usize), BdoDecompError> {
        if (input[0] & 0x02) != 0 {
            if input.len() < 9 {
                return Err(BdoDecompError::TruncatedData);
            }
            let comp_len = u32::from_le_bytes([input[1], input[2], input[3], input[4]]) as usize;
            let decomp_len = u32::from_le_bytes([input[5], input[6], input[7], input[8]]);

            Ok((decomp_len, comp_len, 9))
        } else {
            if input.len() < 3 {
                return Err(BdoDecompError::TruncatedData);
            }
            let comp_len = input[1] as usize;
            let decomp_len = input[2] as u32;

            Ok((decomp_len, comp_len, 3))
        }
    }
}
