#!/usr/bin/env python3
"""THE AUDIO IMPORT POLICY, applied to the .meta files it describes (R10-AUDIO, task 2).

Why a script and not an AssetPostprocessor
------------------------------------------
Same reason as Tools/texture-import-policy.py: Tools/build-check.ps1 compiles Assets/Editor
against the API shim in Tools/UnityShim, and the shim carries no AudioImporter or
AssetPostprocessor. A postprocessor would fail the "unity-code" step and show up as a failure the
build order defines as a regression. A script that writes the .meta files carries no such risk, and
the committed .meta files ARE the proof of the import settings, readable per folder.

The policy
----------
  Assets/Audio/Resources/sfx/**    decompress on load, ADPCM (cheap to decode, small), preload on,
                                   mono for clips of 3.0 seconds or less (WavInfo.MonoMaxSeconds in
                                   AudioCueConfig.cs is the same number), stereo above that.
  Assets/Audio/Resources/music/**  streaming, Vorbis quality 0.6, stereo, load in background,
                                   no preload. Looping is a property of the AudioSource, not of the
                                   import: AudioService sets loop on music and loop cues.

Files are only ever COPIES of the owner's files in D:\\Emberfall\\audio-in. This script reads the
copies' headers to decide mono, and writes only .meta files. It never touches a sound file.

A .meta that already exists keeps its guid (Unity may have made it); one that does not gets a guid
derived from the asset path, so a re-run on a fresh checkout writes the same file.

  python Tools/audio-import-policy.py             # report what would change
  python Tools/audio-import-policy.py --apply     # write the .meta files
"""

import argparse
import hashlib
import os
import re
import struct
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
AUDIO = os.path.join(ROOT, "Assets", "Audio")
MONO_MAX_SECONDS = 3.0

# UnityEngine.AudioClipLoadType / AudioCompressionFormat, by serialised value.
DECOMPRESS_ON_LOAD = 0
STREAMING = 2
VORBIS = 1
ADPCM = 2

SOUND_EXT = (".wav", ".ogg", ".mp3")


def wav_seconds(path):
    with open(path, "rb") as f:
        data = f.read()
    if len(data) < 44 or data[0:4] != b"RIFF" or data[8:12] != b"WAVE":
        return -1.0
    pos = 12
    byte_rate = 0
    data_bytes = -1
    while pos + 8 <= len(data):
        cid = data[pos:pos + 4]
        size = struct.unpack("<I", data[pos + 4:pos + 8])[0]
        if cid == b"fmt " and pos + 20 <= len(data):
            byte_rate = struct.unpack("<I", data[pos + 16:pos + 20])[0]
        if cid == b"data":
            data_bytes = min(size, len(data) - (pos + 8))
            break
        pos += 8 + size + (size & 1)
    if byte_rate <= 0 or data_bytes < 0:
        return -1.0
    return data_bytes / float(byte_rate)


def guid_for(rel):
    return hashlib.md5(("Emberfall.audio:" + rel).encode("utf-8")).hexdigest()


def read_guid(meta_path):
    if not os.path.isfile(meta_path):
        return None
    with open(meta_path, "r", encoding="utf-8") as f:
        m = re.search(r"^guid: ([0-9a-f]{32})\s*$", f.read(), re.M)
    return m.group(1) if m else None


def audio_meta(guid, music, mono):
    if music:
        load_type, fmt, quality, preload, background = STREAMING, VORBIS, "0.6", 0, 1
    else:
        load_type, fmt, quality, preload, background = DECOMPRESS_ON_LOAD, ADPCM, "1", 1, 0
    return (
        "fileFormatVersion: 2\n"
        "guid: %s\n"
        "AudioImporter:\n"
        "  externalObjects: {}\n"
        "  serializedVersion: 8\n"
        "  defaultSettings:\n"
        "    serializedVersion: 2\n"
        "    loadType: %d\n"
        "    sampleRateSetting: 0\n"
        "    sampleRateOverride: 44100\n"
        "    compressionFormat: %d\n"
        "    quality: %s\n"
        "    conversionMode: 0\n"
        "    preloadAudioData: %d\n"
        "  platformSettingsOverride: {}\n"
        "  forceToMono: %d\n"
        "  normalize: 1\n"
        "  loadInBackground: %d\n"
        "  ambisonic: 0\n"
        "  3D: 1\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    ) % (guid, load_type, fmt, quality, preload, 0 if music else (1 if mono else 0), background)


def folder_meta(guid):
    return (
        "fileFormatVersion: 2\n"
        "guid: %s\n"
        "folderAsset: yes\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    ) % guid


def write_if_changed(path, text, apply, counts, label):
    old = None
    if os.path.isfile(path):
        with open(path, "r", encoding="utf-8", newline="") as f:
            old = f.read()
    if old == text:
        counts["same"] += 1
        return
    counts[label] += 1
    if apply:
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    if not os.path.isdir(AUDIO):
        print("no Assets/Audio folder")
        return 1

    counts = {"same": 0, "folder": 0, "sfx": 0, "music": 0}
    per_folder = {}

    # The folder chain: Assets/Audio and everything under it (Assets itself has its own meta).
    folders = [AUDIO]
    for dirpath, dirnames, filenames in os.walk(AUDIO):
        for d in dirnames:
            folders.append(os.path.join(dirpath, d))
    for folder in folders:
        rel = os.path.relpath(folder, ROOT).replace(os.sep, "/")
        meta = folder + ".meta"
        guid = read_guid(meta) or guid_for(rel)
        write_if_changed(meta, folder_meta(guid), args.apply, counts, "folder")

    for dirpath, dirnames, filenames in os.walk(AUDIO):
        for name in sorted(filenames):
            if not name.lower().endswith(SOUND_EXT):
                continue
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, ROOT).replace(os.sep, "/")
            music = "/Resources/music/" in rel
            seconds = wav_seconds(full) if name.lower().endswith(".wav") else -1.0
            mono = 0.0 <= seconds <= MONO_MAX_SECONDS
            meta = full + ".meta"
            guid = read_guid(meta) or guid_for(rel)
            write_if_changed(meta, audio_meta(guid, music, mono), args.apply, counts, "music" if music else "sfx")
            key = os.path.relpath(dirpath, AUDIO).replace(os.sep, "/")
            row = per_folder.setdefault(key, [0, 0, 0, "music" if music else "sfx"])
            row[0] += 1
            row[1 if mono else 2] += 1

    print("%s: %d sound .meta written or changed, %d folder .meta, %d already right"
          % ("applied" if args.apply else "would change",
             counts["sfx"] + counts["music"], counts["folder"], counts["same"]))
    print("per folder: files / mono / stereo")
    for key in sorted(per_folder):
        n, mono, stereo, kind = per_folder[key]
        print("  %-62s %3d / %3d / %3d  %s" % (key, n, mono, stereo, "streaming Vorbis" if kind == "music" else "decompress on load ADPCM"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
