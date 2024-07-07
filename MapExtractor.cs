using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace _7DTDMapExtractor {
	public static class MapExtractor {
		private const string MAGIC = "map\0";
		// Just set to the size of Navezgane for now, but should be easily changeable in the future
		private const int MAP_SIZE = 6144;
		private const int CHUNKS_IN_REGION = 32;

		public static void Extract(string mapFile, string outputFile, Action<int> updateProgress, Action<int> newProgressStage, Action<string> updateStatus) {
			IntPtr imageDataPtr = IntPtr.Zero;
			try {
				/*  ---------------------  */
				/*  Image Data Allocation  */
				/*  ---------------------  */
				imageDataPtr = Marshal.AllocHGlobal(MAP_SIZE * MAP_SIZE * sizeof(ushort));
				Span<ushort> imageData;
				unsafe { imageData = new Span<ushort>(imageDataPtr.ToPointer(), MAP_SIZE * MAP_SIZE); }
				imageData.Clear();
				/*  --------  */
				/*  Map File  */
				/*  --------  */
				if (Path.GetExtension(mapFile) == ".7rm") {
					ExtractRegionMapFiles(Path.GetDirectoryName(mapFile), imageData, updateProgress, newProgressStage, updateStatus);
				} else {
					ExtractMapFile(mapFile, imageData, updateProgress, newProgressStage, updateStatus);
				}
				/*  -----------  */
				/*  Final Stuff  */
				/*  -----------  */
				updateStatus?.Invoke("Saving image");
				using (FileStream outputStream = File.Open(outputFile, FileMode.Create, FileAccess.Write, FileShare.None)) {
					using Bitmap bitmap = new Bitmap(MAP_SIZE, MAP_SIZE, MAP_SIZE * sizeof(ushort), PixelFormat.Format16bppArgb1555, imageDataPtr);
					bitmap.Save(outputStream, ImageFormat.Png);
				}
				updateStatus?.Invoke("Done!");
				newProgressStage?.Invoke(1);
			} finally {
				// Make sure to free the image data, no matter what
				// In case nothing was ever allocated, this'll just do nothing
				Marshal.FreeHGlobal(imageDataPtr);
			}
		}

		private static void PaintChunk(int cx, int cy, BinaryReader reader, Span<ushort> imageData) {
			for (int y = 0; y < 16; y++) {
				for (int x = 0; x < 16; x++) {
					ushort colour = reader.ReadUInt16();
					int index = (x + cx * 16 + MAP_SIZE / 2) + (MAP_SIZE - 1 - (y + cy * 16 + MAP_SIZE / 2)) * MAP_SIZE;
					imageData[index] = (ushort)(colour | 0x8000);
				}
			}
		}

		private static void ExtractMapFile(string mapFile, Span<ushort> imageData, Action<int> updateProgress, Action<int> newProgressStage, Action<string> updateStatus) {
			using FileStream inputStream = File.Open(mapFile, FileMode.Open, FileAccess.Read, FileShare.Read);
			using BinaryReader reader = new BinaryReader(inputStream);
			/*  ------  */
			/*  Header  */
			/*  ------  */
			updateStatus?.Invoke("Reading header");
			if (new string(reader.ReadChars(4)) != MAGIC) {
				throw new FileFormatException("Invalid magic bytes");
			}
			byte version = reader.ReadByte();
			if (version != 3) {
				throw new NotSupportedException($"Unsupported file format version ({version})");
			}
			// Unused 3 bytes
			inputStream.Seek(3, SeekOrigin.Current);
			int maxChunks = reader.ReadInt32();
			int numChunks = reader.ReadInt32();
			/*  -----------------  */
			/*  Chunk Coordinates  */
			/*  -----------------  */
			updateStatus?.Invoke("Reading chunk coordinates");
			newProgressStage?.Invoke(numChunks - 1);
			uint[] chunkCoordinates = new uint[numChunks];
			for (int i = 0; i < numChunks; i++) {
				chunkCoordinates[i] = reader.ReadUInt32();
				updateProgress?.Invoke(i);
			}
			// Unused space
			inputStream.Seek(0x10 + maxChunks * 4, SeekOrigin.Begin);
			/*  -----------  */
			/*  Colour Data  */
			/*  -----------  */
			updateStatus?.Invoke("Reading colour data and painting image");
			newProgressStage?.Invoke(numChunks - 1);
			int MAX_CHUNK = MAP_SIZE / 16 / 2;
			for (int i = 0; i < numChunks; i++) {
				int cx = (short)(chunkCoordinates[i] & 0xFFFFU);
				int cy = (short)(chunkCoordinates[i] >> 16);
				if (cx >= -MAX_CHUNK && cx < MAX_CHUNK && cy >= -MAX_CHUNK && cy < MAX_CHUNK) {
					PaintChunk(cx, cy, reader, imageData);
				} else {
					inputStream.Seek(512, SeekOrigin.Current);
				}
				updateProgress?.Invoke(i);
			}
		}

		private static void ExtractRegionMapFiles(string mapDir, Span<ushort> imageData, Action<int> updateProgress, Action<int> newProgressStage, Action<string> updateStatus) {
			List<string> files = new List<string>(Directory.EnumerateFiles(mapDir, "*.7rm"));
			updateStatus?.Invoke("Reading colour data and painting image");
			newProgressStage?.Invoke(files.Count);
			int progress = 0;
			foreach (string mapFile in files) {
				/*  -----------------  */
				/*  Chunk Coordinates  */
				/*  -----------------  */
				ReadOnlySpan<char> fileName = Path.GetFileName(mapFile.AsSpan());
				int cx, cy;
				try {
					if (!fileName.StartsWith("r.")) throw new Exception();
					ReadOnlySpan<char> nameSpan = fileName.Slice("r.".Length, fileName.Length - "r..7rm".Length);
					int separator = nameSpan.IndexOf('.');
					cx = int.Parse(nameSpan.Slice(0, separator), NumberStyles.AllowLeadingSign);
					cy = int.Parse(nameSpan.Slice(separator + 1), NumberStyles.AllowLeadingSign);
				} catch {
					throw new FileFormatException($"File name \"{fileName}\" doesn't match required pattern");
				}
				cx *= CHUNKS_IN_REGION;
				cy *= CHUNKS_IN_REGION;
				/*  -----------  */
				/*  Actual File  */
				/*  -----------  */
				using FileStream inputStream = File.Open(mapFile, FileMode.Open, FileAccess.Read, FileShare.Read);
				int version;
				using (BinaryReader versionReader = new BinaryReader(inputStream, Encoding.UTF8, true)) {
					version = versionReader.ReadInt32();
					if (version != 1) {
						throw new NotSupportedException($"Unsupported file format version ({version}) in file {fileName}");
					}
				}
				/*  -----------  */
				/*  Colour Data  */
				/*  -----------  */
				// Also uses a BufferedStream to speed up reading (ReadUInt16 used 96% CPU before)
				// 16384 happens to be one row's worth of chunk data and also a decent buffer size here
				using GZipStream gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
				using BufferedStream bufferedStream = new BufferedStream(gzipStream, 16384);
				using BinaryReader reader = new BinaryReader(bufferedStream);
				for (int y = 0; y < CHUNKS_IN_REGION; y++) {
					for (int x = 0; x < CHUNKS_IN_REGION; x++) {
						PaintChunk(cx + x, cy + y, reader, imageData);
					}
				}
				// Only bother updating after a whole region, because otherwise the program
				// spends more time updating the progress bar than actually doing stuff
				updateProgress?.Invoke(++progress);
			}
		}
	}
}
