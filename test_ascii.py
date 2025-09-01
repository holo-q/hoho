#!/usr/bin/env python3
"""Test script to preview the ASCII art"""

from hoho.ascii_art import get_ok_face

print("=== HOHO ASCII ART PREVIEW ===\n")

sizes = ["large", "medium", "small", "mini", "text", "dots"]

for size in sizes:
    print(f"--- {size.upper()} ---")
    print(get_ok_face(size))
    print()