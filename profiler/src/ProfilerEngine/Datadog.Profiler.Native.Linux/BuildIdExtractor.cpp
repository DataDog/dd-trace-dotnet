#include "BuildIdExtractor.h"

#include <fcntl.h>
#include <gelf.h>
#include <libelf.h>
#include <string.h>
#include <unistd.h>

#include "ScopeFinalizer.h"

Elf_Scn *find_note_section(Elf *elf, const char *section_name) {
  size_t stridx;
  if (elf_getshdrstrndx(elf, &stridx) != 0) {
    return nullptr;
  }

  Elf_Scn *section = nullptr;
  GElf_Shdr section_header;
  while ((section = elf_nextscn(elf, section)) != nullptr) {
    if (!gelf_getshdr(section, &section_header) ||
        section_header.sh_type != SHT_NOTE) {
      continue;
    }

    const char *name = elf_strptr(elf, stridx, section_header.sh_name);
    if (name && !strcmp(name, section_name)) {
      return section;
    }
  }

  return nullptr;
}

std::optional<BuildId> process_note(Elf_Data *data, Elf64_Word note_type,
                                        std::string_view note_name) {
  size_t pos = 0;
  GElf_Nhdr note_header;
  size_t name_pos;
  size_t desc_pos;
  while ((pos = gelf_getnote(data, pos, &note_header, &name_pos, &desc_pos)) >
         0) {
    const auto *buf = reinterpret_cast<const std::byte *>(data->d_buf);
    if (note_header.n_type == note_type &&
        note_header.n_namesz == note_name.size() &&
        !memcmp(buf + name_pos, note_name.data(), note_name.size())) {
      auto* begin = buf + desc_pos;
      auto* end = buf + desc_pos + note_header.n_descsz;
      return std::make_optional<BuildId>({begin, end});
    }
  }
  return {};
}

std::optional<BuildId> find_build_id(Elf* elf)
{
    Elf_Scn *scn = elf_nextscn(elf, nullptr);

    const char* node_section_name = ".note.gnu.build-id";
    constexpr std::string_view note_name = "GNU\0";
    Elf64_Word note_type = NT_GNU_BUILD_ID;
  if (scn) {
    // there is a section hdr, try it first
    Elf_Scn *note_section = find_note_section(elf, node_section_name);
    if (!note_section) {
      return {};
    }

    Elf_Data *data = elf_getdata(note_section, nullptr);
    if (data) {
      auto result = process_note(data, note_type, note_name);
      if (result.has_value()) {
        return result;
      }
    }
  }

  // if we didn't find the note in the sections, try the program headers
  size_t phnum;
  if (elf_getphdrnum(elf, &phnum) != 0) {
    return {};
  }
  for (size_t i = 0; i < phnum; ++i) {
    GElf_Phdr phdr_mem;
    GElf_Phdr *phdr = gelf_getphdr(elf, i, &phdr_mem);
    if (phdr != nullptr && phdr->p_type == PT_NOTE) {
      Elf_Data *data =
          elf_getdata_rawchunk(elf, phdr->p_offset, phdr->p_filesz,
                               (phdr->p_align == 8 ? ELF_T_NHDR8 : ELF_T_NHDR));
      if (data) {
        auto result = process_note(data, note_type, note_name);
        if (result.has_value()) {
          return result;
        }
      }
    }
  }
  return {};
}

std::optional<BuildId> BuildIdExtractor::Get(fs::path const& file)
{
    auto fd_holder = ::open(file.c_str(), O_RDONLY);
  if (!fd_holder) {
    return {};
  }
  Elf *elf = elf_begin(fd_holder, ELF_C_READ_MMAP, nullptr);
  if (elf == nullptr) {
    return {};
  }

  on_leave { elf_end(elf); ::close(fd_holder); };

  return find_build_id(elf);
}