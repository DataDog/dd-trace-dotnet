#pragma once

#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>

static inline uint8_t *read_file_contents(const char *filepath, size_t *out_size) {
  FILE *file = fopen(filepath, "rb");
  if (!file) {
    perror("Error opening file");
    return NULL;
  }

  // Get file size
  if (fseek(file, 0, SEEK_END) != 0) {
    perror("Error seeking file");
    fclose(file);
    return NULL;
  }

  long file_size = ftell(file);
  if (file_size < 0) {
    perror("Error getting file size");
    fclose(file);
    return NULL;
  }

  if (fseek(file, 0, SEEK_SET) != 0) {
    perror("Error seeking to start of file");
    fclose(file);
    return NULL;
  }

  // Allocate buffer
  uint8_t *buffer = (uint8_t *)malloc((size_t)file_size);
  if (!buffer) {
    perror("Memory allocation failed");
    fclose(file);
    return NULL;
  }

  // Read file
  size_t bytes_read = fread(buffer, 1, (size_t)file_size, file);
  fclose(file);

  if (bytes_read != (size_t)file_size) {
    perror("Error reading file");
    free(buffer);
    return NULL;
  }

  *out_size = bytes_read;
  return buffer;
}

