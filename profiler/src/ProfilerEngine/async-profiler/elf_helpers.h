#pragma once

#include "ddprof_defs.hpp"

struct Elf;

struct SectionInfo {
  const char *_data;
  Offset_t _offset;
  ElfAddress_t _vaddr_sec;
};

// To adjust addresses inside the eh_frame_hdr
// If we are in different segments, we should consider
// (vaddr_eh_frame - vaddr_eh_frame_hdr)
//     + (offset_eh_frame - offset_eh_frame_hdr)
struct EhFrameInfo {
  SectionInfo _eh_frame;
  SectionInfo _eh_frame_hdr;
};

bool get_elf_offsets(Elf *elf, const char *filepath, ElfAddress_t &vaddr,
                     Offset_t &elf_offset, Offset_t &bias_offset,
                     Offset_t &text_base);

const char *get_section_data(Elf *elf, const char *section_name,
                             Offset_t &elf_offset);

bool get_section_info(Elf *elf, const char *section_name,
                      SectionInfo &section_info);

bool get_eh_frame_info(Elf *elf, EhFrameInfo &eh_frame_info);

bool process_fdes(Elf *elf);
